using Kardia.Library.Enums;
using Kardia.WebApi.Features.Days.Services.Scoring;

namespace Kardia.WebApi.Features.Ages.Services;

/// <summary>One habit or marker that moves your biological age.</summary>
/// <param name="Key">A stable key ("vo2max", "resting"…).</param>
/// <param name="Name">The name shown.</param>
/// <param name="Value">Your value (30-day average), or null when Apple Health has none.</param>
/// <param name="Unit">The unit of <paramref name="Value"/>.</param>
/// <param name="Typical">The typical value for your age and sex.</param>
/// <param name="Years">The years it adds (positive) or takes off (negative).</param>
internal sealed record AgeFactor(string Key, string Name, double? Value, string Unit, double? Typical, double Years);

/// <summary>A biological age.</summary>
/// <param name="Age">Your biological age, one decimal.</param>
/// <param name="ChronologicalAge">Your age by the calendar.</param>
/// <param name="Factors">What moved it, biggest effect first.</param>
/// <param name="DaysUsed">The days with data in the window.</param>
internal sealed record AgeResult(double Age, double ChronologicalAge, IReadOnlyList<AgeFactor> Factors, int DaysUsed)
{
    /// <summary>Gets the difference from your calendar age (negative = younger).</summary>
    public double Difference => Math.Round(Age - ChronologicalAge, 1);
}

/// <summary>
/// Biological age: your age by the calendar, moved by how your markers compare with what's typical for your age and sex.
/// Each marker is turned into years with a capped, linear rule drawn from the large cohort studies on fitness and
/// mortality (cardio fitness weighs most: each ml/kg/min above typical takes ~0.45 years off), the total is shrunk by
/// 15 % because the markers overlap, and it's capped at ±12 years. An estimate to follow over time, not a diagnosis.
/// Pure, unit-tested.
/// </summary>
internal static class BiologicalAge
{
    /// <summary>The days averaged.</summary>
    public const int WindowDays = 30;

    /// <summary>The fewest days with data before an age is given.</summary>
    public const int MinimumDays = 14;

    /// <summary>How far back the pace of ageing compares.</summary>
    public const int PaceDays = 90;

    /// <summary>How much of the plain sum is kept (the markers overlap).</summary>
    public const double Overlap = 0.85;

    /// <summary>
    /// Works out a biological age.
    /// </summary>
    /// <param name="age">Calendar age in years.</param>
    /// <param name="sex">For the reference values.</param>
    /// <param name="window">The days to average (usually the last 30).</param>
    /// <param name="latestVo2Max">The latest cardio fitness reading, when none falls in the window (the Watch measures it every few days).</param>
    /// <param name="heightCm">Height, for BMI.</param>
    /// <param name="weightKg">Weight, for BMI (a weight from Apple Health in the window wins).</param>
    /// <returns>The age, or null with fewer than <see cref="MinimumDays"/> days of data.</returns>
    public static AgeResult? Calculate(double age, BiologicalSex sex, IReadOnlyList<DayScore> window, double? latestVo2Max, double? heightCm, double? weightKg)
    {
        var days = window.Where(static day => day.HasData).ToList();
        if (days.Count < MinimumDays)
        {
            return null;
        }

        double? Average(Func<DayScore, double?> pick)
        {
            var values = days.Select(pick).Where(static value => value is not null).Select(static value => value!.Value).ToList();
            return values.Count == 0 ? null : values.Average();
        }

        var factors = new List<AgeFactor>();

        // Cardio fitness.
        var vo2 = Average(static day => day.Value(HealthMetric.Vo2Max)) ?? latestVo2Max;
        var typicalVo2 = Blend(sex, 47.0 - 0.38 * (age - 25), 39.0 - 0.32 * (age - 25));
        factors.Add(new AgeFactor("vo2max", "Cardio fitness", Round(vo2), "VO₂ max", Math.Round(typicalVo2, 1),
            vo2 is { } v ? Math.Clamp(-(v - typicalVo2) * 0.45, -5, 5) : 0));

        // Resting heart rate.
        var resting = Average(static day => day.Value(HealthMetric.RestingHeartRate));
        var typicalResting = Blend(sex, 62, 66);
        factors.Add(new AgeFactor("resting", "Resting heart rate", Round(resting), "bpm", typicalResting,
            resting is { } r ? Math.Clamp((r - typicalResting) * 0.18, -3, 3) : 0));

        // HRV (SDNN falls with age).
        var hrv = Average(static day => day.Value(HealthMetric.Hrv));
        var typicalHrv = Math.Max(20, 62 - 0.55 * (age - 25));
        factors.Add(new AgeFactor("hrv", "Heart rate variability", Round(hrv), "ms", Math.Round(typicalHrv, 1),
            hrv is { } h ? Math.Clamp(-(h - typicalHrv) / typicalHrv * 4, -2.5, 2.5) : 0));

        // Sleep: 7–9 hours is the sweet spot.
        var sleepHours = Average(static day => day.AsleepMinutes / 60.0);
        factors.Add(new AgeFactor("sleep", "Sleep", Round(sleepHours), "h a night", 7.5,
            sleepHours is { } s ? Math.Clamp(s < 7 ? (7 - s) * 1.2 : s > 9 ? (s - 9) * 0.6 : -(Math.Min(s, 8) - 7) * 0.4, -0.5, 3) : 0));

        // Regular bed and wake times.
        var consistency = Average(static day => day.SleepConsistency);
        factors.Add(new AgeFactor("consistency", "Sleep consistency", Round(consistency, 0), "%", 75,
            consistency is { } c ? Math.Clamp((75 - c) * 0.03, -0.75, 1.5) : 0));

        // Steps.
        var steps = Average(static day => day.Value(HealthMetric.Steps));
        factors.Add(new AgeFactor("steps", "Daily steps", Round(steps, 0), "steps", 7500,
            steps is { } st ? Math.Clamp(-(st - 7500) / 1000 * 0.3, -1.5, 2) : 0));

        // Time with the heart rate in zone 2 or higher, per week.
        var zoneMinutes = days.Where(static day => day.Load is not null)
            .Sum(static day => day.Load!.ZoneSeconds.Split(',').Skip(1).Sum(static part => int.TryParse(part, out var seconds) ? seconds : 0)) / 60.0;
        var loadDays = days.Count(static day => day.Load is not null);
        double? weekly = loadDays >= 7 ? zoneMinutes / loadDays * 7 : null;
        factors.Add(new AgeFactor("training", "Time in zone 2+", Round(weekly, 0), "min a week", 150,
            weekly is { } w ? Math.Clamp(-(w - 150) / 60 * 0.4, -1.5, 2) : 0));

        // Body mass index, when height and weight are known.
        var weight = Average(static day => day.Value(HealthMetric.BodyMass)) ?? weightKg;
        if (weight is { } kg && heightCm is { } cm && cm > 0)
        {
            var bmi = kg / Math.Pow(cm / 100, 2);
            var over = Math.Max(0, Math.Abs(bmi - 22.5) - 2.5);
            factors.Add(new AgeFactor("bmi", "Body mass index", Math.Round(bmi, 1), "BMI", 22.5, Math.Clamp(over * 0.35, 0, 2.5)));
        }

        var total = Math.Clamp(factors.Sum(static factor => factor.Years) * Overlap, -12, 12);
        var ordered = factors
            .Select(static factor => factor with { Years = Math.Round(factor.Years * Overlap, 1) })
            .OrderByDescending(static factor => Math.Abs(factor.Years))
            .ToList();
        return new AgeResult(Math.Round(age + total, 1), Math.Round(age, 1), ordered, days.Count);
    }

    /// <summary>
    /// The pace of ageing: how fast your biological age moves against the calendar, from now and <see cref="PaceDays"/>
    /// days ago. 1.0× = a year per year; 0.7× = you've been gaining on the calendar. Damped (each year gained or lost over
    /// the three months moves it by 0.25) and kept between 0.2 and 2.5, because three months is a short window.
    /// </summary>
    /// <param name="now">The latest age.</param>
    /// <param name="earlier">The age worked out <see cref="PaceDays"/> days earlier.</param>
    /// <returns>The pace, one decimal.</returns>
    public static double Pace(AgeResult now, AgeResult earlier)
    {
        var change = (now.Age - now.ChronologicalAge) - (earlier.Age - earlier.ChronologicalAge);
        return Math.Round(Math.Clamp(1 + Math.Clamp(change, -3, 3) * 0.25, 0.2, 2.5), 1);
    }

    private static double Blend(BiologicalSex sex, double male, double female) => sex switch
    {
        BiologicalSex.Male => male,
        BiologicalSex.Female => female,
        _ => (male + female) / 2,
    };

    private static double? Round(double? value, int digits = 1) => value is { } number ? Math.Round(number, digits) : null;
}
