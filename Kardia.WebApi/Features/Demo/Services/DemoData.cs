using System.Globalization;
using System.Text;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Demo.Services;

/// <summary>Generated Apple Health data for a demo account.</summary>
/// <param name="Metrics">Daily values.</param>
/// <param name="HeartRates">Heart rate per minute.</param>
/// <param name="Sleeps">Nights.</param>
/// <param name="Workouts">Workouts.</param>
/// <param name="Journal">Journal entries (day, tag).</param>
internal sealed record DemoDataSet(
    List<DailyMetricRow> Metrics,
    List<HeartRateRow> HeartRates,
    List<SleepNightRow> Sleeps,
    List<WorkoutRow> Workouts,
    List<(DateOnly Day, JournalTag Tag)> Journal);

/// <summary>
/// Generates believable Apple Watch data: a fit 30-year-old getting fitter over four months (VO2 max 45 → 48,
/// resting heart rate 56 → 52), a weekly plan of runs, rides and strength sessions, nights in 90-minute cycles,
/// and habits that show up the next morning (alcohol lowers HRV and deep sleep, a late bedtime costs sleep).
/// Deterministic for a seed, so screenshots are repeatable. Only used in demo mode.
/// </summary>
internal static class DemoData
{
    /// <summary>The days generated, ending today.</summary>
    public const int Days = 120;

    private sealed record Session(string Kind, int StartMinute, int Minutes, int Bpm, double? Km, int KcalPerMinute, int Steps);

    /// <summary>
    /// Generates the data.
    /// </summary>
    /// <param name="today">Today's date.</param>
    /// <param name="now">The local wall clock (today stops here).</param>
    /// <param name="seed">The random seed.</param>
    /// <returns>The data.</returns>
    public static DemoDataSet Generate(DateOnly today, DateTime now, int seed)
    {
        var random = new Random(seed);
        double Noise(double spread) => (random.NextDouble() * 2 - 1) * spread;

        var metrics = new List<DailyMetricRow>();
        var heartRates = new List<HeartRateRow>();
        var sleeps = new List<SleepNightRow>();
        var workouts = new List<WorkoutRow>();
        var journal = new List<(DateOnly, JournalTag)>();

        var first = today.AddDays(-(Days - 1));
        var hardDayBefore = false;
        var alcoholBefore = false;
        var lateBefore = false;
        for (var index = 0; index < Days; index++)
        {
            var day = first.AddDays(index);
            var progress = index / (double)(Days - 1);
            var isToday = day == today;
            var back = today.DayNumber - day.DayNumber;
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            // Habits of the day (they show up in the night that follows).
            var alcohol = day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? random.NextDouble() < 0.35 : random.NextDouble() < 0.06;
            var lateCaffeine = random.NextDouble() < 0.25;
            var screens = random.NextDouble() < 0.4;
            var stretching = random.NextDouble() < 0.35;
            var meditation = random.NextDouble() < 0.2;
            var magnesium = random.NextDouble() < 0.3;
            if (isToday)
            {
                (alcohol, lateCaffeine, screens, stretching, meditation, magnesium) = (false, true, false, true, false, false);
            }

            void Mark(bool on, JournalTag tag)
            {
                if (on && back <= 90)
                {
                    journal.Add((day, tag));
                }
            }

            Mark(alcohol, JournalTag.Alcohol);
            Mark(lateCaffeine, JournalTag.LateCaffeine);
            Mark(screens, JournalTag.ScreensInBed);
            Mark(stretching, JournalTag.Stretching);
            Mark(meditation, JournalTag.Meditation);
            Mark(magnesium, JournalTag.Magnesium);

            // Last night (it ended this morning): later on weekends, worse after alcohol the evening before.
            var bed = day.ToDateTime(TimeOnly.MinValue).AddDays(-1).AddMinutes(23 * 60 + 45 + Noise(30) + (lateBefore ? 45 : 0));
            var wake = day.ToDateTime(TimeOnly.MinValue).AddMinutes((weekend ? 8 * 60 + 25 : 7 * 60 + 35) + Noise(18));
            if (isToday)
            {
                bed = day.ToDateTime(TimeOnly.MinValue).AddDays(-1).AddMinutes(23 * 60 + 48);
                wake = day.ToDateTime(TimeOnly.MinValue).AddMinutes(7 * 60 + 52);
            }

            var night = Night(bed, wake, alcoholBefore, random);
            sleeps.Add(night);

            // The morning's markers.
            var asleep = night.CoreMinutes + night.DeepMinutes + night.RemMinutes;
            var hrvBase = 55 + 9 * progress;
            var hrv = hrvBase * (1 + Noise(0.08)) * (alcoholBefore ? 0.91 : 1) * (hardDayBefore ? 0.94 : 1) * (asleep < 420 ? 0.95 : 1);
            var resting = 56 - 4 * progress + Noise(1.2) + (alcoholBefore ? 2 : 0) + (hardDayBefore ? 1 : 0);
            if (isToday)
            {
                hrv = 68;
                resting = 52;
            }

            var sessions = Plan(back, day, random).ToList();
            var steps = 5_500 + random.Next(0, 4_000) + sessions.Sum(static session => session.Steps);
            var active = 330 + random.Next(0, 220) + sessions.Sum(static session => session.Minutes * session.KcalPerMinute);
            var fraction = isToday ? Math.Clamp(now.TimeOfDay.TotalHours / 24, 0.05, 1) : 1;

            void Metric(HealthMetric metric, double value) =>
                metrics.Add(new DailyMetricRow { DayDate = day.ToDateTime(TimeOnly.MinValue), Metric = (int)metric, Value = Math.Round((decimal)value, 2) });

            Metric(HealthMetric.Hrv, hrv);
            Metric(HealthMetric.RestingHeartRate, resting);
            Metric(HealthMetric.RespiratoryRate, 14.6 + Noise(0.3) + (alcoholBefore ? 0.4 : 0));
            Metric(HealthMetric.BloodOxygen, 97 + Noise(1));
            Metric(HealthMetric.WristTemperature, 36.35 + Noise(0.1) + (alcoholBefore ? 0.25 : 0));
            Metric(HealthMetric.Steps, Math.Round(steps * (isToday ? Math.Min(1, fraction * 1.35) : 1)));
            Metric(HealthMetric.ActiveEnergy, Math.Round(active * (isToday ? Math.Min(1, fraction * 1.25) : 1)));
            Metric(HealthMetric.BasalEnergy, Math.Round((1_860 + Noise(25)) * fraction));
            Metric(HealthMetric.WalkingHeartRate, 98 - 4 * progress + Noise(3));
            Metric(HealthMetric.ExerciseMinutes, sessions.Sum(static session => session.Minutes) + random.Next(5, 20));
            if (index % 3 == 0 || isToday)
            {
                Metric(HealthMetric.Vo2Max, 45.4 + 2.8 * progress + Noise(0.3));
            }

            if (index % 7 == 0)
            {
                Metric(HealthMetric.BodyMass, 79.6 - 1.0 * progress + Noise(0.3));
            }

            // Heart rate, minute by minute (today until now).
            var end = isToday ? now : day.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var tonightBed = day.ToDateTime(TimeOnly.MinValue).AddMinutes(23 * 60 + 30 + Noise(25) + (day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? 45 : 0));
            // Errands, stairs and the walk to lunch: two to four stretches of 10–25 minutes with the heart rate up.
            var errands = Enumerable.Range(0, random.Next(2, 5)).Select(_ => (Start: random.Next(9 * 60, 20 * 60), Minutes: random.Next(10, 26), Lift: random.Next(30, 46))).ToList();
            for (var minute = day.ToDateTime(TimeOnly.MinValue); minute < end; minute = minute.AddMinutes(1))
            {
                double bpm;
                var clock = minute.TimeOfDay.TotalMinutes;
                var session = sessions.FirstOrDefault(s => clock >= s.StartMinute && clock < s.StartMinute + s.Minutes);
                if (minute < wake || minute >= tonightBed)
                {
                    // Asleep: lowest in the middle of the night.
                    bpm = resting - 3 + 4 * Math.Cos(clock / 1440 * 2 * Math.PI) + Noise(2);
                }
                else if (session is not null)
                {
                    var into = clock - session.StartMinute;
                    var ramp = Math.Min(1, into / 6);
                    var wave = session.Kind.Contains("Interval", StringComparison.Ordinal) ? 14 * Math.Sin(into / 3) : 4 * Math.Sin(into / 7);
                    bpm = resting + (session.Bpm - resting) * ramp + wave + Noise(4);
                }
                else
                {
                    var after = sessions.Where(s => clock >= s.StartMinute + s.Minutes).Select(s => clock - (s.StartMinute + s.Minutes)).DefaultIfEmpty(999).Min();
                    var cooldown = after < 30 ? 25 * Math.Exp(-after / 8) : 0;
                    var errand = errands.FirstOrDefault(e => clock >= e.Start && clock < e.Start + e.Minutes);
                    var walking = errand.Minutes > 0 ? errand.Lift : 0;
                    bpm = resting + 16 + 8 * Math.Sin(clock / 47) + cooldown + walking + Noise(5);
                }

                heartRates.Add(new HeartRateRow { MinuteAt = minute, Bpm = (short)Math.Clamp(Math.Round(bpm), 38, 200) });
            }

            foreach (var session in sessions)
            {
                var start = day.ToDateTime(TimeOnly.MinValue).AddMinutes(session.StartMinute);
                if (start.AddMinutes(session.Minutes) > end)
                {
                    continue;
                }

                var inside = heartRates.Where(row => row.MinuteAt >= start && row.MinuteAt < start.AddMinutes(session.Minutes)).ToList();
                workouts.Add(new WorkoutRow
                {
                    StartedAt = start,
                    EndedAt = start.AddMinutes(session.Minutes),
                    Kind = session.Kind,
                    ActiveKcal = session.Minutes * session.KcalPerMinute,
                    DistanceKm = session.Km is { } km ? Math.Round((decimal)km, 2) : null,
                    AvgBpm = (short)Math.Round(inside.Average(static row => row.Bpm)),
                    MaxBpm = inside.Max(static row => row.Bpm),
                });
            }

            hardDayBefore = sessions.Any(static session => session.Bpm >= 160 || session.Minutes >= 70);
            alcoholBefore = alcohol;
            lateBefore = day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
        }

        return new DemoDataSet(metrics, heartRates, sleeps, workouts, journal);
    }

    /// <summary>The week's plan, by days before today (today is a run day).</summary>
    private static IEnumerable<Session> Plan(int back, DateOnly day, Random random)
    {
        if (back == 0)
        {
            yield return new Session("Outdoor Run", 8 * 60 + 30, 38, 156, 7.2, 13, 6_200);
            yield return new Session("Outdoor Walk", 13 * 60 + 20, 24, 104, 2.1, 5, 2_900);
            yield break;
        }

        if (random.NextDouble() < 0.12)
        {
            yield break;   // a missed session
        }

        switch (back % 7)
        {
            case 1:
                yield return new Session("Traditional Strength Training", 18 * 60 + 40, 62, 128, null, 7, 600);
                break;
            case 2:
                yield return new Session("Outdoor Run · Intervals", 7 * 60 + 15, 48, 164, 9.4, 15, 8_100);
                break;
            case 3:
                yield return new Session("Outdoor Cycling", 8 * 60 + 5, 75, 141, 32.4, 10, 0);
                break;
            case 4:
                yield return new Session("Outdoor Walk", 18 * 60, 45, 102, 3.8, 5, 5_200);
                break;
            case 5:
                yield return new Session(day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "Outdoor Run · Long" : "Outdoor Run", 9 * 60 + 10, 72, 150, 13.1, 12, 11_500);
                break;
            case 6:
                yield return new Session("Traditional Strength Training", 19 * 60, 55, 124, null, 7, 500);
                break;
        }
    }

    /// <summary>A night in ~90-minute cycles: more deep sleep early, more REM late, a few short awakenings.</summary>
    private static SleepNightRow Night(DateTime bed, DateTime wake, bool alcohol, Random random)
    {
        var total = (int)(wake - bed).TotalMinutes;
        var parts = new List<(SleepStage Stage, int Minutes)> { (SleepStage.Awake, random.Next(6, 14)) };
        var used = parts[0].Minutes;
        var cycle = 0;
        while (used < total - 10)
        {
            var deep = (int)Math.Max(0, (cycle switch { 0 => 42, 1 => 32, 2 => 16, _ => 6 }) * (alcohol ? 0.7 : 1) + random.Next(-6, 7));
            var rem = cycle switch { 0 => 12, 1 => 20, 2 => 28, _ => 34 } + random.Next(-5, 6);
            var plan = new List<(SleepStage, int)>
            {
                (SleepStage.Core, 18 + random.Next(0, 12)),
                (SleepStage.Deep, deep),
                (SleepStage.Core, 16 + random.Next(0, 14)),
                (SleepStage.Rem, rem),
            };
            if (random.NextDouble() < (alcohol ? 0.6 : 0.35))
            {
                plan.Add((SleepStage.Awake, random.Next(2, alcohol ? 9 : 5)));
            }

            foreach (var (stage, minutes) in plan)
            {
                var length = Math.Min(minutes, total - used);
                if (length <= 0)
                {
                    break;
                }

                parts.Add((stage, length));
                used += length;
            }

            cycle++;
        }

        if (used < total)
        {
            parts.Add((SleepStage.Awake, total - used));
        }

        var stages = new StringBuilder();
        var counts = new int[4];
        var at = 0;
        foreach (var (stage, minutes) in parts.Where(static part => part.Minutes > 0))
        {
            counts[(int)stage] += minutes;
            stages.Append(stages.Length > 0 ? ";" : string.Empty).Append(CultureInfo.InvariantCulture, $"{(int)stage},{at},{at + minutes}");
            at += minutes;
        }

        return new SleepNightRow
        {
            NightDate = wake.Date,
            BedStart = bed,
            BedEnd = wake,
            AwakeMinutes = (short)counts[(int)SleepStage.Awake],
            RemMinutes = (short)counts[(int)SleepStage.Rem],
            CoreMinutes = (short)counts[(int)SleepStage.Core],
            DeepMinutes = (short)counts[(int)SleepStage.Deep],
            Stages = stages.ToString(),
        };
    }
}
