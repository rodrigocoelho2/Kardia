using Kardia.Library.Enums;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Syncs.Services;

/// <summary>
/// The day values of HRV, respiratory rate, blood oxygen and wrist temperature, worked out from their timed readings like WHOOP does: the
/// average of the readings taken during the night that ended that morning. Readings from later in the day never move
/// them, so a sync in the afternoon can't change the morning's recovery. Without a night yet, the day's readings are
/// used until the night arrives. Pure, unit-tested.
/// </summary>
internal static class NightlyVitals
{
    /// <summary>
    /// Works out the day values.
    /// </summary>
    /// <param name="days">The days to work out.</param>
    /// <param name="nights">The nights of those days (keyed by the morning you woke up).</param>
    /// <param name="samples">Every reading from the evening before the first day to the end of the last.</param>
    /// <returns>One row per day and metric that has readings.</returns>
    public static List<DailyMetricRow> Compute(IEnumerable<DateOnly> days, IReadOnlyList<SleepNightRow> nights, IReadOnlyList<MetricSampleRow> samples)
    {
        var byNight = nights.GroupBy(static night => DateOnly.FromDateTime(night.NightDate)).ToDictionary(static group => group.Key, static group => group.First());
        var rows = new List<DailyMetricRow>();
        foreach (var day in days.Distinct().Order())
        {
            foreach (var metric in HealthPayload.SampledMetrics.Order())
            {
                var readings = samples.Where(sample => sample.Metric == (int)metric);
                List<decimal> values;
                if (byNight.TryGetValue(day, out var night))
                {
                    // During the night, with room either side: the Watch stamps some readings (wrist temperature) with
                    // the start of the sleep session, which can be a little before the first sleep stage.
                    var from = night.BedStart.AddMinutes(-60);
                    var to = night.BedEnd.AddMinutes(30);
                    values = readings.Where(sample => sample.SampleAt >= from && sample.SampleAt <= to).Select(static sample => sample.Value).ToList();
                }
                else
                {
                    // No night yet: the day's readings, with those from the evening counted towards the next morning.
                    values = readings.Where(sample => MorningOf(sample.SampleAt) == day).Select(static sample => sample.Value).ToList();
                }

                if (values.Count > 0)
                {
                    rows.Add(new DailyMetricRow { DayDate = day.ToDateTime(TimeOnly.MinValue), Metric = (int)metric, Value = Math.Round(values.Average(), 3) });
                }
            }
        }

        return rows;
    }

    /// <summary>
    /// The day totals (steps, energy, exercise, daylight) from every timed reading received for those days.
    /// </summary>
    /// <param name="days">The days.</param>
    /// <param name="samples">The readings of those days.</param>
    /// <returns>One row per day and metric with readings.</returns>
    public static List<DailyMetricRow> Totals(IEnumerable<DateOnly> days, IReadOnlyList<MetricSampleRow> samples)
    {
        var wanted = days.ToHashSet();
        return samples
            .Where(sample => HealthPayload.SummedMetrics.Contains((HealthMetric)sample.Metric) && wanted.Contains(DateOnly.FromDateTime(sample.SampleAt)))
            .GroupBy(static sample => (Day: sample.SampleAt.Date, sample.Metric))
            .Select(static group => new DailyMetricRow { DayDate = group.Key.Day, Metric = group.Key.Metric, Value = Math.Round(group.Sum(static s => s.Value), 3) })
            .OrderBy(static row => row.DayDate).ThenBy(static row => row.Metric)
            .ToList();
    }

    /// <summary>The morning a reading belongs to: from 18:00 on, the next day's.</summary>
    /// <param name="at">When it was measured.</param>
    /// <returns>The day.</returns>
    public static DateOnly MorningOf(DateTime at) => DateOnly.FromDateTime(at.Hour >= 18 ? at.AddDays(1) : at);
}
