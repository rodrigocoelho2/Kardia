using Kardia.Library.Enums;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Syncs.Services;

/// <summary>
/// The day values of HRV, respiratory rate and blood oxygen, worked out from their timed readings like WHOOP does: the
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
                    // During the night, with a little room either side for readings taken as you fell asleep or woke.
                    var from = night.BedStart.AddMinutes(-15);
                    var to = night.BedEnd.AddMinutes(15);
                    values = readings.Where(sample => sample.SampleAt >= from && sample.SampleAt <= to).Select(static sample => sample.Value).ToList();
                }
                else
                {
                    values = readings.Where(sample => DateOnly.FromDateTime(sample.SampleAt) == day).Select(static sample => sample.Value).ToList();
                }

                if (values.Count > 0)
                {
                    rows.Add(new DailyMetricRow { DayDate = day.ToDateTime(TimeOnly.MinValue), Metric = (int)metric, Value = Math.Round(values.Average(), 3) });
                }
            }
        }

        return rows;
    }
}
