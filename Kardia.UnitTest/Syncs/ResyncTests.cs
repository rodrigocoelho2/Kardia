using System.Text.Json;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Syncs.Models.Entities;
using Kardia.WebApi.Features.Syncs.Services;

namespace Kardia.UnitTest.Syncs;

/// <summary>
/// A night that's over stays as it was: a later sync (a partial night, readings from the afternoon) can't lower its
/// sleep or move its recovery.
/// </summary>
public sealed class ResyncTests
{
    private const string Night = """
        { "data": { "metrics": [
          { "name": "sleep_analysis", "units": "hr", "data": [
            { "startDate": "2026-10-15 23:30:00 +0100", "endDate": "2026-10-16 01:00:00 +0100", "value": "Core" },
            { "startDate": "2026-10-16 01:00:00 +0100", "endDate": "2026-10-16 02:00:00 +0100", "value": "Deep" },
            { "startDate": "2026-10-16 02:00:00 +0100", "endDate": "2026-10-16 05:00:00 +0100", "value": "Core" },
            { "startDate": "2026-10-16 05:00:00 +0100", "endDate": "2026-10-16 07:00:00 +0100", "value": "REM" }
          ] },
          { "name": "heart_rate_variability", "units": "ms", "data": [
            { "date": "2026-10-16 01:30:00 +0100", "qty": 62 }, { "date": "2026-10-16 04:10:00 +0100", "qty": 58 }
          ] },
          { "name": "respiratory_rate", "units": "count/min", "data": [ { "date": "2026-10-16 03:00:00 +0100", "qty": 14.6 } ] }
        ] } }
        """;

    [Fact]
    public async Task AnAfternoonSync_DoesNotMoveLastNightsHrv()
    {
        var (app, key, id) = await SetUpAsync();
        await Sync(app, key, Night);
        var before = await Hrv(app, id);

        await Sync(app, key, """
            { "data": { "metrics": [ { "name": "heart_rate_variability", "units": "ms", "data": [
              { "date": "2026-10-16 14:20:00 +0100", "qty": 31 }, { "date": "2026-10-16 16:05:00 +0100", "qty": 28 } ] } ] } }
            """);

        Assert.Equal(60m, before);
        Assert.Equal(60m, await Hrv(app, id));
    }

    [Fact]
    public async Task APartialNight_DoesNotShortenTheNight()
    {
        var (app, key, id) = await SetUpAsync();
        await Sync(app, key, Night);

        // "Since last sync" bringing only the last hour of the night.
        await Sync(app, key, """
            { "data": { "metrics": [ { "name": "sleep_analysis", "units": "hr", "data": [
              { "startDate": "2026-10-16 06:00:00 +0100", "endDate": "2026-10-16 07:00:00 +0100", "value": "REM" } ] } ] } }
            """);

        var night = Assert.Single(await app.Health.SleepsListAsync(id, TestSupport.Today, TestSupport.Today));
        Assert.Equal(new DateTime(2026, 10, 15, 23, 30, 0), night.BedStart);
        Assert.Equal(new DateTime(2026, 10, 16, 7, 0, 0), night.BedEnd);
        Assert.Equal(450, night.CoreMinutes + night.DeepMinutes + night.RemMinutes);
    }

    [Fact]
    public async Task TheRestOfANight_IsAddedToIt()
    {
        var (app, key, id) = await SetUpAsync();
        await Sync(app, key, """
            { "data": { "metrics": [ { "name": "sleep_analysis", "units": "hr", "data": [
              { "startDate": "2026-10-15 23:30:00 +0100", "endDate": "2026-10-16 03:00:00 +0100", "value": "Core" } ] } ] } }
            """);
        await Sync(app, key, """
            { "data": { "metrics": [ { "name": "sleep_analysis", "units": "hr", "data": [
              { "startDate": "2026-10-16 03:00:00 +0100", "endDate": "2026-10-16 07:00:00 +0100", "value": "Deep" } ] } ] } }
            """);

        var night = Assert.Single(await app.Health.SleepsListAsync(id, TestSupport.Today, TestSupport.Today));
        Assert.Equal((210, 240), (night.CoreMinutes, night.DeepMinutes));
    }

    [Fact]
    public async Task ReadingsBeforeTheNightArrives_AreRedoneWithTheNight()
    {
        var (app, key, id) = await SetUpAsync();

        // The Health metrics automation runs first, with readings from the night and from the morning.
        await Sync(app, key, """
            { "data": { "metrics": [ { "name": "heart_rate_variability", "units": "ms", "data": [
              { "date": "2026-10-16 03:00:00 +0100", "qty": 66 }, { "date": "2026-10-16 11:00:00 +0100", "qty": 30 } ] } ] } }
            """);
        Assert.Equal(48m, await Hrv(app, id));

        // Then the sleep automation: the day's HRV becomes the night's.
        await Sync(app, key, """
            { "data": { "metrics": [ { "name": "sleep_analysis", "units": "hr", "data": [
              { "startDate": "2026-10-15 23:30:00 +0100", "endDate": "2026-10-16 07:00:00 +0100", "value": "Core" } ] } ] } }
            """);
        Assert.Equal(66m, await Hrv(app, id));
    }

    [Fact]
    public void NightlyVitals_UseTheReadingsOfTheNight()
    {
        var night = new SleepNightRow { NightDate = new DateTime(2026, 10, 16), BedStart = new DateTime(2026, 10, 15, 23, 0, 0), BedEnd = new DateTime(2026, 10, 16, 7, 0, 0) };
        var samples = new List<MetricSampleRow>
        {
            new() { Metric = (int)HealthMetric.Hrv, SampleAt = new DateTime(2026, 10, 15, 23, 40, 0), Value = 70 },
            new() { Metric = (int)HealthMetric.Hrv, SampleAt = new DateTime(2026, 10, 16, 6, 50, 0), Value = 50 },
            new() { Metric = (int)HealthMetric.Hrv, SampleAt = new DateTime(2026, 10, 16, 15, 0, 0), Value = 20 },
            new() { Metric = (int)HealthMetric.BloodOxygen, SampleAt = new DateTime(2026, 10, 15, 13, 0, 0), Value = 99 },
        };

        var rows = NightlyVitals.Compute([new DateOnly(2026, 10, 16)], [night], samples);

        var hrv = Assert.Single(rows);
        Assert.Equal(((int)HealthMetric.Hrv, 60m), (hrv.Metric, hrv.Value));
    }

    private static async Task<(TestApp App, string Key, Guid Id)> SetUpAsync()
    {
        var app = new TestApp();
        var user = await app.CreateUserAsync();
        var key = (await app.SyncService.KeyCreateAsync(user)).Result!.Key;
        return (app, key, Guid.Parse(user));
    }

    private static async Task Sync(TestApp app, string key, string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = await app.SyncService.IngestAsync(key, document.RootElement.Clone());
        Assert.True(result.IsSuccess, result.Problems?.Title);
    }

    private static async Task<decimal?> Hrv(TestApp app, Guid id) =>
        (await app.Health.MetricsListAsync(id, TestSupport.Today, TestSupport.Today)).FirstOrDefault(static m => m.Metric == (int)HealthMetric.Hrv)?.Value;
}
