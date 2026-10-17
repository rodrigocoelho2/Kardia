using System.Text.Json;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Syncs.Services;

namespace Kardia.UnitTest.Syncs;

public sealed class HealthPayloadTests
{
    private static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");
    private static readonly DateTime Now = new(2026, 10, 16, 18, 40, 0);

    private const string HealthAutoExport = """
        {
          "data": {
            "metrics": [
              { "name": "heart_rate_variability", "units": "ms", "data": [ { "date": "2026-10-16 06:58:00 +0100", "qty": 68 } ] },
              { "name": "resting_heart_rate", "units": "count/min", "data": [ { "date": "2026-10-16 00:00:00 +0100", "qty": 52 } ] },
              { "name": "step_count", "units": "count", "data": [ { "date": "2026-10-16 00:00:00 +0100", "qty": 9842 } ] },
              { "name": "active_energy", "units": "kJ", "data": [ { "date": "2026-10-16 00:00:00 +0100", "qty": 2092 } ] },
              { "name": "heart_rate", "units": "count/min", "data": [
                { "date": "2026-10-16 08:30:00 +0100", "Min": 148, "Avg": 152, "Max": 157 },
                { "date": "2026-10-16 08:31:00 +0100", "Min": 151, "Avg": 156, "Max": 160 }
              ] },
              { "name": "sleep_analysis", "units": "hr", "data": [
                { "startDate": "2026-10-15 23:48:00 +0100", "endDate": "2026-10-16 00:20:00 +0100", "value": "Core" },
                { "startDate": "2026-10-16 00:20:00 +0100", "endDate": "2026-10-16 01:05:00 +0100", "value": "Deep" },
                { "startDate": "2026-10-16 01:05:00 +0100", "endDate": "2026-10-16 01:10:00 +0100", "value": "Awake" },
                { "startDate": "2026-10-16 01:10:00 +0100", "endDate": "2026-10-16 07:52:00 +0100", "value": "REM" }
              ] }
            ],
            "workouts": [
              { "name": "Outdoor Run", "start": "2026-10-16 08:30:00 +0100", "end": "2026-10-16 09:08:00 +0100",
                "activeEnergyBurned": { "qty": 512, "units": "kcal" }, "distance": { "qty": 7.2, "units": "km" },
                "heartRate": { "min": 98, "avg": 156, "max": 182 } }
            ]
          }
        }
        """;

    private static HealthPayloadData Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return HealthPayload.Parse(document.RootElement.Clone(), Lisbon, Now);
    }

    [Fact]
    public void ReadsHealthAutoExportMetrics()
    {
        var data = Parse(HealthAutoExport);

        Assert.Null(data.Error);
        var hrv = Assert.Single(data.Samples);
        Assert.Equal(((int)HealthMetric.Hrv, new DateTime(2026, 10, 16, 6, 58, 0), 68m), (hrv.Metric, hrv.SampleAt, hrv.Value));
        Assert.DoesNotContain(data.Metrics, m => m.Metric == (int)HealthMetric.Hrv);
        Assert.Equal(52m, data.Metrics.Single(m => m.Metric == (int)HealthMetric.RestingHeartRate).Value);
        Assert.Equal(9842m, data.Metrics.Single(m => m.Metric == (int)HealthMetric.Steps).Value);
    }

    [Fact]
    public void ConvertsKilojoulesToKilocalories()
    {
        var data = Parse(HealthAutoExport);

        Assert.InRange((double)data.Metrics.Single(m => m.Metric == (int)HealthMetric.ActiveEnergy).Value, 499.0, 501.0);
    }

    [Fact]
    public void ReadsHeartRatePerMinuteInLocalTime()
    {
        var data = Parse(HealthAutoExport);

        Assert.Equal(2, data.HeartRates.Count);
        Assert.Equal(new DateTime(2026, 10, 16, 8, 30, 0), data.HeartRates[0].MinuteAt);
        Assert.Equal((short)152, data.HeartRates[0].Bpm);
    }

    [Fact]
    public void BuildsTheNightFromSleepSegments()
    {
        var night = Assert.Single(Parse(HealthAutoExport).Sleeps);

        Assert.Equal(new DateTime(2026, 10, 16), night.NightDate);
        Assert.Equal(new DateTime(2026, 10, 15, 23, 48, 0), night.BedStart);
        Assert.Equal(new DateTime(2026, 10, 16, 7, 52, 0), night.BedEnd);
        Assert.Equal((short)32, night.CoreMinutes);
        Assert.Equal((short)45, night.DeepMinutes);
        Assert.Equal((short)5, night.AwakeMinutes);
        Assert.Equal((short)402, night.RemMinutes);
        Assert.NotEqual(string.Empty, night.Stages);
    }

    [Fact]
    public void ReadsWorkouts()
    {
        var workout = Assert.Single(Parse(HealthAutoExport).Workouts);

        Assert.Equal("Outdoor Run", workout.Kind);
        Assert.Equal(new DateTime(2026, 10, 16, 8, 30, 0), workout.StartedAt);
        Assert.Equal(512m, workout.ActiveKcal);
        Assert.Equal(7.2m, workout.DistanceKm);
        Assert.Equal((short)182, workout.MaxBpm);
    }

    [Fact]
    public void ReadsTheShortcutsLooseNumbers()
    {
        var data = Parse("""{ "date": "2026-10-16", "hrv": "68 ms", "restingHeartRate": 52, "respiratoryRate": "14,8", "steps": 9842 }""");

        Assert.Null(data.Error);
        Assert.Empty(data.Samples);   // the Shortcut sends day values, not timed readings
        Assert.Equal(68m, data.Metrics.Single(m => m.Metric == (int)HealthMetric.Hrv).Value);
        Assert.Equal(14.8m, data.Metrics.Single(m => m.Metric == (int)HealthMetric.RespiratoryRate).Value);
        Assert.All(data.Metrics, m => Assert.Equal(new DateTime(2026, 10, 16), m.DayDate));
    }

    [Fact]
    public void AnEmptyExportImportsNothing()
    {
        var data = Parse("""{ "data": { "metrics": [], "workouts": [] } }""");

        Assert.True(data.IsEmpty);
    }
}
