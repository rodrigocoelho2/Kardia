using System.Text.Json;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.UnitTest.Days;

/// <summary>When Apple's resting heart rate hasn't arrived, last night's heart rate stands in for it.</summary>
public sealed class RestingEstimateTests
{
    private static readonly DateOnly Day = new(2026, 10, 16);

    [Fact]
    public void ADayWithoutRestingHeartRate_GetsItFromTheNight_OnYourOwnScale()
    {
        var values = new Dictionary<DateOnly, Dictionary<HealthMetric, double>>();
        for (var back = 1; back <= 5; back++)
        {
            values[Day.AddDays(-back)] = new() { [HealthMetric.RestingHeartRate] = 55, [HealthMetric.SleepingHeartRate] = 50 };
        }

        values[Day] = new() { [HealthMetric.SleepingHeartRate] = 52 };

        var estimated = DayScoreBuilder.EstimateResting(values);

        Assert.Equal(57, values[Day][HealthMetric.RestingHeartRate]);
        Assert.True(estimated.Contains(Day));
        Assert.Single(estimated);
    }

    [Fact]
    public void WithoutHistory_TheUsualGapIsUsed_AndAppleWins()
    {
        var values = new Dictionary<DateOnly, Dictionary<HealthMetric, double>>
        {
            [Day] = new() { [HealthMetric.SleepingHeartRate] = 50 },
            [Day.AddDays(-1)] = new() { [HealthMetric.SleepingHeartRate] = 49, [HealthMetric.RestingHeartRate] = 60 },
        };

        var estimated = DayScoreBuilder.EstimateResting(values);

        Assert.Equal(50 + DayScoreBuilder.DefaultRestingOffset, values[Day][HealthMetric.RestingHeartRate]);
        Assert.Equal(60, values[Day.AddDays(-1)][HealthMetric.RestingHeartRate]);
        Assert.False(estimated.Contains(Day.AddDays(-1)));
    }

    [Fact]
    public void TheHeartRateAsleep_IsTheLowestFifth()
    {
        var night = Enumerable.Range(0, 100).Select(i => new HeartRateRow { MinuteAt = new DateTime(2026, 10, 16, 1, 0, 0).AddMinutes(i), Bpm = (short)(50 + i % 20) }).ToList();

        Assert.Equal(51.5, DayLoadService.SleepingHeartRate(night));
        Assert.Null(DayLoadService.SleepingHeartRate(night.Take(30).ToList()));
    }

    [Fact]
    public async Task ASyncWithoutRestingHeartRate_StillFillsTheHealthMonitor()
    {
        var app = new TestApp();
        var user = await app.CreateUserAsync();
        var key = (await app.SyncService.KeyCreateAsync(user)).Result!.Key;
        var heart = string.Join(",", Enumerable.Range(0, 400).Select(i => $$"""{ "date": "{{new DateTime(2026, 10, 16, 0, 0, 0).AddMinutes(i):yyyy-MM-dd HH:mm}}:00 +0100", "Avg": {{52 + i % 9}} }"""));
        using var document = JsonDocument.Parse($$"""
            { "data": { "metrics": [
              { "name": "sleep_analysis", "units": "hr", "data": [
                { "startDate": "2026-10-15 23:50:00 +0100", "endDate": "2026-10-16 03:00:00 +0100", "value": "Core" },
                { "startDate": "2026-10-16 03:00:00 +0100", "endDate": "2026-10-16 06:40:00 +0100", "value": "REM" } ] },
              { "name": "heart_rate", "units": "count/min", "data": [ {{heart}} ] }
            ] } }
            """);
        Assert.True((await app.SyncService.IngestAsync(key, document.RootElement.Clone())).IsSuccess);

        var day = (await app.DayService.GetAsync(user, null)).Result!;
        var resting = day.Health.Signs.Single(static s => s.Key == "resting");

        Assert.NotNull(resting.Value);
        Assert.True(resting.Estimated);
        Assert.True(day.Recovery.RestingEstimated);
    }
}
