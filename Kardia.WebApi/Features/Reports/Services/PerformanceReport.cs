using System.Globalization;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Days.Models.DTOs;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Reports.Models.DTOs;

namespace Kardia.WebApi.Features.Reports.Services;

/// <summary>
/// The weekly and monthly performance assessment (WHOOP's Weekly / Monthly Performance Assessment): did your strain
/// match your recovery, did you sleep what you needed, how regular were you, and how the period compares with the one
/// before. Pure, unit-tested.
/// </summary>
internal static class PerformanceReport
{
    /// <summary>Strain points either side of the target that still count as on target.</summary>
    public const double TargetSlack = 1.0;

    /// <summary>The days a period covers.</summary>
    /// <param name="period">Week or month.</param>
    /// <param name="date">A day inside it.</param>
    /// <returns>The first and last day, and those of the period before.</returns>
    public static (DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo) Range(ReportPeriod period, DateOnly date)
    {
        if (period == ReportPeriod.Month)
        {
            var first = new DateOnly(date.Year, date.Month, 1);
            return (first, first.AddMonths(1).AddDays(-1), first.AddMonths(-1), first.AddDays(-1));
        }

        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        return (monday, monday.AddDays(6), monday.AddDays(-7), monday.AddDays(-1));
    }

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="period">Week or month.</param>
    /// <param name="date">A day inside it.</param>
    /// <param name="scores">Scored days, oldest first, from 28 days before the previous period to the end (or today).</param>
    /// <param name="today">Today (later days are not counted).</param>
    /// <returns>The report.</returns>
    public static ReportDTO Build(ReportPeriod period, DateOnly date, IReadOnlyList<DayScore> scores, DateOnly today)
    {
        var (from, to, previousFrom, previousTo) = Range(period, date);
        var current = scores.Where(score => score.Date >= from && score.Date <= to && score.Date <= today).ToList();
        var previous = scores.Where(score => score.Date >= previousFrom && score.Date <= previousTo).ToList();
        var withRecovery = current.Where(static score => score.Recovery.Score is not null).ToList();
        // Today's strain is still growing: only finished days count for strain against target.
        var finished = withRecovery.Where(score => score.Date < today).ToList();
        var onTarget = finished.Count(static score => score.Strain is { } strain && strain >= score.Target.Low - TargetSlack && strain <= score.Target.High + TargetSlack);
        var sleepAverage = Average(current, static score => score.SleepPerformance);
        var consistency = Average(current, static score => score.SleepConsistency);

        int? reportScore = null;
        if (finished.Count >= 3 && sleepAverage is { } sleepPerformance)
        {
            reportScore = (int)Math.Round(40.0 * onTarget / finished.Count + 0.4 * sleepPerformance + 0.2 * (consistency ?? sleepPerformance));
        }

        var upTo = scores.Where(score => score.Date <= to && score.Date <= today).ToList();
        var cardio = upTo.Count > 0 ? CardioLoad.Of(upTo) : null;
        var metrics = Metrics(current, previous);
        var report = new ReportDTO
        {
            Period = period,
            Title = period == ReportPeriod.Month
                ? from.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
                : $"Week of {from.ToString("d MMM", CultureInfo.InvariantCulture)}",
            From = from,
            To = to,
            PreviousFrom = previousFrom,
            PreviousTo = previousTo,
            InProgress = to >= today,
            DaysWithData = current.Count(static score => score.HasData),
            Score = reportScore,
            Assessment = reportScore switch
            {
                null => string.Empty,
                >= 85 => "Peak",
                >= 70 => "Strong",
                >= 55 => "Fair",
                _ => "Off track",
            },
            StrainOnTargetDays = onTarget,
            StrainDays = finished.Count,
            RecoveryDays = withRecovery.Count,
            HighDays = withRecovery.Count(static score => score.Recovery.Zone == RecoveryZone.High),
            MediumDays = withRecovery.Count(static score => score.Recovery.Zone == RecoveryZone.Medium),
            LowDays = withRecovery.Count(static score => score.Recovery.Zone == RecoveryZone.Low),
            HealthFlaggedDays = current.Count(static score => HealthMonitor.Outside(HealthMonitor.Of(score)) >= 2),
            CardioRatio = cardio?.Ratio,
            CardioStatus = cardio?.Status ?? CardioLoadStatus.None,
            Metrics = metrics,
            Days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
                .Select(offset => from.AddDays(offset))
                .Select(day => scores.FirstOrDefault(score => score.Date == day) is { } score ? DayService.Summary(score) : new DaySummaryDTO { Date = day })
                .ToList(),
        };
        report.Highlights = Highlights(report, current, period);
        return report;
    }

    private static List<ReportMetricDTO> Metrics(List<DayScore> current, List<DayScore> previous)
    {
        ReportMetricDTO Metric(string key, string name, string unit, int decimals, bool higherIsBetter, double steady, Func<List<DayScore>, double?> value)
        {
            var now = value(current) is { } a ? Math.Round(a, decimals) : (double?)null;
            var before = value(previous) is { } b ? Math.Round(b, decimals) : (double?)null;
            var change = now is { } x && before is { } y ? Math.Round(x - y, decimals) : (double?)null;
            return new ReportMetricDTO
            {
                Key = key,
                Name = name,
                Unit = unit,
                Decimals = decimals,
                Value = now,
                Previous = before,
                Change = change,
                HigherIsBetter = higherIsBetter,
                Trend = change is not { } delta ? string.Empty
                    : Math.Abs(delta) <= steady ? "steady"
                    : (delta > 0) == higherIsBetter ? "better" : "worse",
            };
        }

        return
        [
            Metric("recovery", "Recovery", "%", 0, true, 2, static days => Average(days, static d => d.Recovery.Score)),
            Metric("hrv", "HRV", "ms", 0, true, 1, static days => Average(days, static d => d.Value(HealthMetric.Hrv))),
            Metric("resting", "Resting heart rate", "bpm", 0, false, 0.5, static days => Average(days, static d => d.Value(HealthMetric.RestingHeartRate))),
            Metric("asleep", "Time asleep", "min", 0, true, 5, static days => Average(days, static d => d.AsleepMinutes)),
            Metric("sleep", "Sleep performance", "%", 0, true, 2, static days => Average(days, static d => d.SleepPerformance)),
            Metric("consistency", "Sleep consistency", "%", 0, true, 2, static days => Average(days, static d => d.SleepConsistency)),
            Metric("strain", "Strain", string.Empty, 1, true, 0.3, static days => Average(days, static d => d.Strain)),
            Metric("active", "Zone 2+ minutes a day", "min", 0, true, 3, static days => days.Count(static d => d.Load is not null) == 0 ? null : days.Where(static d => d.Load is not null).Average(static d => (double)DayService.ActiveMinutes(d))),
            Metric("steps", "Steps a day", "steps", 0, true, 250, static days => Average(days, static d => d.Value(HealthMetric.Steps))),
            Metric("vo2max", "VO₂ max", "ml/kg/min", 1, true, 0.2, static days => days.LastOrDefault(static d => d.Value(HealthMetric.Vo2Max) is not null)?.Value(HealthMetric.Vo2Max)),
        ];
    }

    private static List<string> Highlights(ReportDTO report, List<DayScore> current, ReportPeriod period)
    {
        var lines = new List<string>();
        var noun = period == ReportPeriod.Month ? "month" : "week";
        if (report.DaysWithData == 0)
        {
            return [$"No Apple Watch data this {noun} yet."];
        }

        ReportMetricDTO M(string key) => report.Metrics.First(metric => metric.Key == key);

        if (report.StrainDays > 0)
        {
            lines.Add($"Strain matched your recovery on {report.StrainOnTargetDays} of {report.StrainDays} days ({report.HighDays} green, {report.MediumDays} yellow, {report.LowDays} red).");
        }

        if (M("asleep").Value is { } asleep && M("sleep").Value is { } performance)
        {
            var need = Average(current, static d => d.AsleepMinutes is null ? (int?)null : d.Need.TotalMinutes);
            lines.Add($"You slept {Duration(asleep)} a night, {performance:0}% of what you needed{(need is { } n ? $" ({Duration(n)})" : string.Empty)}.");
        }

        if (M("hrv") is { Value: { } hrv, Previous: { } before } && before > 0)
        {
            var change = (hrv - before) / before * 100;
            lines.Add(Math.Abs(change) < 3
                ? $"HRV held steady at {hrv:0} ms."
                : $"HRV averaged {hrv:0} ms, {Math.Abs(change):0}% {(change > 0 ? "above" : "below")} the {noun} before.");
        }

        if (M("resting") is { Value: { } rhr, Change: { } rhrChange } && Math.Abs(rhrChange) >= 2)
        {
            lines.Add($"Resting heart rate {(rhrChange > 0 ? "rose" : "fell")} {Math.Abs(rhrChange):0} bpm to {rhr:0}.");
        }

        if (report.CardioRatio is { } ratio)
        {
            lines.Add(report.CardioStatus switch
            {
                CardioLoadStatus.Detraining => $"Cardio load is low ({ratio:0.00}): you're doing less than your body is used to.",
                CardioLoadStatus.Maintaining => $"Cardio load is maintaining ({ratio:0.00}): enough to hold your fitness.",
                CardioLoadStatus.Productive => $"Cardio load is productive ({ratio:0.00}): fitness is building.",
                CardioLoadStatus.Overreaching => $"Cardio load is high ({ratio:0.00}): good for a block, but plan easier days.",
                _ => $"Cardio load jumped ({ratio:0.00}): back off for a few days to absorb it.",
            });
        }

        if (report.HealthFlaggedDays > 0)
        {
            lines.Add($"Health Monitor flagged {report.HealthFlaggedDays} {(report.HealthFlaggedDays == 1 ? "day" : "days")} with two or more signs outside your range.");
        }

        return lines;
    }

    private static double? Average<T>(List<DayScore> days, Func<DayScore, T?> value)
        where T : struct, IConvertible
    {
        var values = days.Select(value).Where(static v => v is not null).Select(static v => Convert.ToDouble(v!.Value, CultureInfo.InvariantCulture)).ToList();
        return values.Count == 0 ? null : values.Average();
    }

    private static string Duration(double minutes)
    {
        var total = (int)Math.Round(minutes);
        return $"{total / 60}h {total % 60:00}m";
    }
}
