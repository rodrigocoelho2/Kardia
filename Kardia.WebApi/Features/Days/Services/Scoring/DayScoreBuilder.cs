using Kardia.Library.Enums;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Days.Services.Scoring;

/// <summary>
/// Everything Kardia worked out for one day.
/// </summary>
internal sealed class DayScore
{
    /// <summary>Gets the day.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the day's values from Apple Health.</summary>
    public required IReadOnlyDictionary<HealthMetric, double> Values { get; init; }

    /// <summary>Gets the HRV baseline (the 30 days before).</summary>
    public required Baseline HrvBaseline { get; init; }

    /// <summary>Gets the resting heart rate baseline.</summary>
    public required Baseline RestingBaseline { get; init; }

    /// <summary>Gets the respiratory rate baseline.</summary>
    public required Baseline RespiratoryBaseline { get; init; }

    /// <summary>Gets the wrist temperature baseline.</summary>
    public required Baseline TemperatureBaseline { get; init; }

    /// <summary>Gets the blood oxygen baseline.</summary>
    public Baseline OxygenBaseline { get; init; } = new(97, 1.2, 0);

    /// <summary>Gets the night that ended this morning, if any.</summary>
    public SleepNightRow? Night { get; init; }

    /// <summary>Gets what that night needed.</summary>
    public required SleepNeed Need { get; init; }

    /// <summary>Gets the minutes asleep that night.</summary>
    public int? AsleepMinutes { get; init; }

    /// <summary>Gets the sleep performance (0–100).</summary>
    public int? SleepPerformance { get; init; }

    /// <summary>Gets the sleep efficiency (0–100).</summary>
    public int? SleepEfficiency { get; init; }

    /// <summary>Gets the sleep consistency (0–100).</summary>
    public int? SleepConsistency { get; init; }

    /// <summary>Gets how far short of the need the night was, in minutes (never negative).</summary>
    public int ShortfallMinutes { get; init; }

    /// <summary>Gets the recovery.</summary>
    public required RecoveryResult Recovery { get; init; }

    /// <summary>Gets the day's load, when its heart rate was synced.</summary>
    public DayLoadRow? Load { get; init; }

    /// <summary>Gets the day's strain (0–21), when its heart rate was synced.</summary>
    public double? Strain { get; init; }

    /// <summary>Gets the strain worth aiming for, from the recovery.</summary>
    public required (double Low, double High) Target { get; init; }

    /// <summary>
    /// A value of the day.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <returns>The value, or null.</returns>
    public double? Value(HealthMetric metric) => Values.TryGetValue(metric, out var value) ? value : null;

    /// <summary>Gets a value indicating whether anything was synced for the day.</summary>
    public bool HasData => Values.Count > 0 || Night is not null || Load is not null;
}

/// <summary>
/// Works out <see cref="DayScore"/>s for a range of days from the rows in the database. It walks forward from 34 days
/// before the range, so each day's baselines (the 30 days before it) and sleep need (the strain of the day before and
/// the shortfall of the three nights before) are right. Pure, unit-tested.
/// </summary>
internal static class DayScoreBuilder
{
    /// <summary>The days of history to load before the first day of a range.</summary>
    public const int History = Baseline.Days + 4;

    /// <summary>
    /// Scores a range of days.
    /// </summary>
    /// <param name="from">First day to return.</param>
    /// <param name="to">Last day to return.</param>
    /// <param name="facts">The profile.</param>
    /// <param name="metrics">Daily values from <c>from − History</c> to <paramref name="to"/>.</param>
    /// <param name="nights">Nights in the same range.</param>
    /// <param name="loads">Day loads in the same range.</param>
    /// <returns>One score per day, in order.</returns>
    public static List<DayScore> Build(
        DateOnly from,
        DateOnly to,
        ProfileFacts facts,
        IEnumerable<DailyMetricRow> metrics,
        IEnumerable<SleepNightRow> nights,
        IEnumerable<DayLoadRow> loads)
    {
        var values = new Dictionary<DateOnly, Dictionary<HealthMetric, double>>();
        foreach (var row in metrics)
        {
            var day = DateOnly.FromDateTime(row.DayDate);
            if (!values.TryGetValue(day, out var dayValues))
            {
                dayValues = [];
                values[day] = dayValues;
            }

            dayValues[(HealthMetric)row.Metric] = (double)row.Value;
        }

        var nightsByDay = nights.GroupBy(static night => DateOnly.FromDateTime(night.NightDate)).ToDictionary(static group => group.Key, static group => group.Last());
        var loadsByDay = loads.GroupBy(static load => DateOnly.FromDateTime(load.DayDate)).ToDictionary(static group => group.Key, static group => group.Last());

        double? Metric(DateOnly day, HealthMetric metric) =>
            values.TryGetValue(day, out var dayValues) && dayValues.TryGetValue(metric, out var value) ? value : null;

        IEnumerable<double> Previous(DateOnly day, HealthMetric metric)
        {
            for (var back = 1; back <= Baseline.Days; back++)
            {
                if (Metric(day.AddDays(-back), metric) is { } value)
                {
                    yield return value;
                }
            }
        }

        var scores = new List<DayScore>();
        var shortfalls = new Dictionary<DateOnly, int>();
        var strains = new Dictionary<DateOnly, double>();
        var recentNights = new List<SleepNightRow>();
        for (var day = from.AddDays(-History); day <= to; day = day.AddDays(1))
        {
            loadsByDay.TryGetValue(day, out var load);
            double? strain = load is null ? null : StrainScore.FromTrimp((double)load.Trimp);
            if (strain is { } dayStrain)
            {
                strains[day] = dayStrain;
            }

            var need = SleepScore.Need(
                facts.SleepNeedMinutes,
                strains.TryGetValue(day.AddDays(-1), out var before) ? before : null,
                Enumerable.Range(1, 3).Select(back => shortfalls.TryGetValue(day.AddDays(-back), out var minutes) ? minutes : 0));

            nightsByDay.TryGetValue(day, out var night);
            int? asleep = night is null ? null : SleepScore.AsleepMinutes(night);
            int? performance = asleep is { } minutes ? SleepScore.Performance(minutes, need.TotalMinutes) : null;
            if (asleep is { } slept)
            {
                shortfalls[day] = Math.Max(0, need.TotalMinutes - slept);
                recentNights.Add(night!);
            }

            if (day < from)
            {
                continue;
            }

            var hrvBaseline = Baseline.Of(Previous(day, HealthMetric.Hrv), 50, 12);
            var restingBaseline = Baseline.Of(Previous(day, HealthMetric.RestingHeartRate), 60, 4);
            var respiratoryBaseline = Baseline.Of(Previous(day, HealthMetric.RespiratoryRate), 15, 0.8);
            var temperatureBaseline = Baseline.Of(Previous(day, HealthMetric.WristTemperature), 36.3, 0.3);
            var oxygenBaseline = Baseline.Of(Previous(day, HealthMetric.BloodOxygen), 97, 1.2);
            var recovery = RecoveryScore.Calculate(new RecoveryInputs(
                Metric(day, HealthMetric.Hrv),
                hrvBaseline,
                Metric(day, HealthMetric.RestingHeartRate),
                restingBaseline,
                Metric(day, HealthMetric.RespiratoryRate),
                respiratoryBaseline,
                temperatureBaseline.Count >= 4 ? Metric(day, HealthMetric.WristTemperature) : null,
                temperatureBaseline,
                performance));

            scores.Add(new DayScore
            {
                Date = day,
                Values = values.TryGetValue(day, out var dayValues) ? dayValues : new Dictionary<HealthMetric, double>(),
                HrvBaseline = hrvBaseline,
                RestingBaseline = restingBaseline,
                RespiratoryBaseline = respiratoryBaseline,
                TemperatureBaseline = temperatureBaseline,
                OxygenBaseline = oxygenBaseline,
                Night = night,
                Need = need,
                AsleepMinutes = asleep,
                SleepPerformance = performance,
                SleepEfficiency = night is null ? null : SleepScore.Efficiency(night),
                SleepConsistency = night is null ? null : SleepScore.Consistency(recentNights),
                ShortfallMinutes = shortfalls.TryGetValue(day, out var shortfall) ? shortfall : 0,
                Recovery = recovery,
                Load = load,
                Strain = strain,
                Target = RecoveryScore.StrainTarget(recovery.Score),
            });
        }

        return scores;
    }
}
