using System.Text.Json;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Syncs.Models.Entities;
using Kardia.WebApi.Features.Syncs.Services;

namespace Kardia.UnitTest.Days;

/// <summary>The stress monitor, the energy bank, heart-rate recovery and time in daylight.</summary>
public sealed class StressTests
{
    private static readonly DateOnly Day = new(2026, 10, 16);
    private static readonly DateTime Wake = new(2026, 10, 16, 7, 0, 0);

    /// <summary>Heart-rate minutes from <paramref name="from"/>, one per minute, at a fixed rate.</summary>
    private static IEnumerable<HeartRateRow> Minutes(DateTime from, int count, int bpm) =>
        Enumerable.Range(0, count).Select(i => new HeartRateRow { MinuteAt = from.AddMinutes(i), Bpm = (short)bpm });

    private static DayStressResult Compute(IEnumerable<HeartRateRow> samples, IReadOnlyList<MetricSampleRow>? steps = null, IReadOnlyList<WorkoutRow>? workouts = null, int? recovery = 70) =>
        DayStress.Compute(Day, samples.OrderBy(static s => s.MinuteAt).ToList(), steps ?? [], workouts ?? [], Wake, 50, 190, recovery, new DateTime(2026, 10, 16, 13, 0, 0));

    [Fact]
    public void SittingNearRestingHeartRate_IsLowStress()
    {
        var result = Compute(Minutes(Wake, 360, 58));

        Assert.True(result.Average < 1);
        Assert.Equal(360, result.Minutes().Low);
        Assert.Equal(0, result.Minutes().High);
    }

    [Fact]
    public void HeartRateUpWhileStill_IsHighStress()
    {
        var result = Compute(Minutes(Wake, 120, 58).Concat(Minutes(Wake.AddHours(2), 60, 95)).Concat(Minutes(Wake.AddHours(3), 180, 60)));

        Assert.Equal(60, result.Minutes().High);
        Assert.True(result.Judged.Where(static b => b.Level >= 2).All(static b => b.At.Hour == 9));
    }

    [Fact]
    public void AWorkout_IsMovementNotStress_AndTheBlockAfterIsSkipped()
    {
        var run = new WorkoutRow { StartedAt = Wake.AddHours(2), EndedAt = Wake.AddHours(2).AddMinutes(40), Kind = "Outdoor Run" };
        var result = Compute(Minutes(Wake, 120, 58).Concat(Minutes(run.StartedAt, 40, 120)).Concat(Minutes(run.EndedAt, 200, 60)), workouts: [run]);

        Assert.Equal(4, result.Blocks.Count(static b => b.Active));
        var after = result.Blocks.Single(b => b.At == run.EndedAt);
        Assert.Null(after.Level);
        Assert.Equal(0, result.Minutes().High);
    }

    [Fact]
    public void Steps_MarkABlockAsMovement()
    {
        var walk = new DateTime(2026, 10, 16, 10, 0, 0);
        var result = Compute(Minutes(Wake, 360, 75), steps: [new MetricSampleRow { Metric = (int)HealthMetric.Steps, SampleAt = walk.AddMinutes(3), Value = 900 }]);

        Assert.True(result.Blocks.Single(b => b.At == walk).Active);
        Assert.False(result.Blocks.Single(b => b.At == walk.AddMinutes(30)).Active);
    }

    [Fact]
    public void TheEnergyBank_StartsAtRecovery_AndSpendsMoreUnderStress()
    {
        var calm = Compute(Minutes(Wake, 360, 58), recovery: 80);
        var tense = Compute(Minutes(Wake, 360, 92), recovery: 80);

        Assert.Equal(80, calm.EnergyStart);
        Assert.True(calm.EnergyNow < 80);
        Assert.True(tense.EnergyNow < calm.EnergyNow);
        Assert.True(tense.StressDrain > calm.StressDrain);
    }

    [Fact]
    public void NoHeartRate_NoStressOrEnergy()
    {
        var result = Compute([]);

        Assert.Empty(result.Blocks);
        Assert.Null(result.Average);
        Assert.Null(result.EnergyNow);
    }

    [Fact]
    public void HeartRateRecovery_IsTheFallOneAndTwoMinutesAfterTheEnd()
    {
        var run = new WorkoutRow { StartedAt = new DateTime(2026, 10, 16, 8, 30, 0), EndedAt = new DateTime(2026, 10, 16, 9, 8, 20), Kind = "Outdoor Run" };
        var samples = new List<HeartRateRow>
        {
            new() { MinuteAt = new DateTime(2026, 10, 16, 9, 7, 0), Bpm = 165 },
            new() { MinuteAt = new DateTime(2026, 10, 16, 9, 8, 0), Bpm = 160 },
            new() { MinuteAt = new DateTime(2026, 10, 16, 9, 9, 0), Bpm = 136 },
            new() { MinuteAt = new DateTime(2026, 10, 16, 9, 10, 0), Bpm = 118 },
        };

        Assert.Equal((29, 47), ((int, int))(DayStress.Recovery(samples, run) is var (one, two) ? (one!.Value, two!.Value) : default));
        Assert.Equal((null, null), DayStress.Recovery(samples.Take(2).ToList(), run));
    }

    [Fact]
    public void HeartRateRecovery_SkipsEasyWorkouts()
    {
        var walk = new WorkoutRow { StartedAt = new DateTime(2026, 10, 16, 13, 0, 0), EndedAt = new DateTime(2026, 10, 16, 13, 30, 0), Kind = "Outdoor Walk" };
        var samples = Minutes(new DateTime(2026, 10, 16, 13, 28, 0), 5, 98).ToList();

        Assert.Equal((null, null), DayStress.Recovery(samples, walk));
    }

    [Fact]
    public void Daylight_AndTimedSteps_AreSummedPerDay()
    {
        using var document = JsonDocument.Parse("""
            { "data": { "metrics": [
              { "name": "time_in_daylight", "units": "min", "data": [ { "date": "2026-10-16 08:00:00 +0100", "qty": 12 }, { "date": "2026-10-16 13:00:00 +0100", "qty": 25 } ] },
              { "name": "step_count", "units": "count", "data": [ { "date": "2026-10-16 10:00:00 +0100", "qty": 800 }, { "date": "2026-10-16 10:05:00 +0100", "qty": 400 } ] }
            ] } }
            """);
        var data = HealthPayload.Parse(document.RootElement.Clone(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon"), new DateTime(2026, 10, 16, 18, 0, 0));

        Assert.Equal(37m, data.Metrics.Single(static m => m.Metric == (int)HealthMetric.Daylight).Value);
        Assert.Equal(2, data.Samples.Count(static s => s.Metric == (int)HealthMetric.Steps));
        var totals = NightlyVitals.Totals([Day], data.Samples);
        Assert.Equal(1200m, totals.Single(static t => t.Metric == (int)HealthMetric.Steps).Value);
        Assert.Equal(37m, totals.Single(static t => t.Metric == (int)HealthMetric.Daylight).Value);
    }

    [Fact]
    public void AWorkoutsRecoveryCurve_FeedsTheHeartRateMinutes()
    {
        using var document = JsonDocument.Parse("""
            { "data": { "workouts": [ { "name": "Outdoor Run", "start": "2026-10-16 08:30:00 +0100", "end": "2026-10-16 09:00:00 +0100",
              "heartRateData": [ { "date": "2026-10-16 08:59:00 +0100", "Avg": 162 } ],
              "heartRateRecovery": [ { "date": "2026-10-16 09:01:00 +0100", "Avg": 131 }, { "date": "2026-10-16 09:02:00 +0100", "Avg": 117 } ] } ] } }
            """);
        var data = HealthPayload.Parse(document.RootElement.Clone(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon"), new DateTime(2026, 10, 16, 18, 0, 0));

        Assert.Equal(3, data.HeartRates.Count);
        Assert.Contains(data.HeartRates, static h => h.MinuteAt == new DateTime(2026, 10, 16, 9, 2, 0) && h.Bpm == 117);
    }

    [Fact]
    public async Task ASync_WorksOutTheWorkoutsHeartRateRecovery()
    {
        var app = new TestApp();
        var user = Guid.Parse(await app.CreateUserAsync());
        var run = new WorkoutRow { StartedAt = new DateTime(2026, 10, 16, 8, 30, 0), EndedAt = new DateTime(2026, 10, 16, 9, 0, 0), Kind = "Outdoor Run" };
        await app.Health.WorkoutsSaveAsync(user, [run]);
        await app.Health.HeartRatesSaveAsync(user, [.. Minutes(new DateTime(2026, 10, 16, 8, 30, 0), 31, 160), new HeartRateRow { MinuteAt = new DateTime(2026, 10, 16, 9, 1, 0), Bpm = 135 }, new HeartRateRow { MinuteAt = new DateTime(2026, 10, 16, 9, 2, 0), Bpm = 120 }]);

        await app.DayLoads.RecomputeAsync(user, [Day]);

        var saved = Assert.Single(await app.Health.WorkoutsListAsync(user, Day, Day));
        Assert.Equal((short)25, saved.Hrr1);
        Assert.Equal((short)40, saved.Hrr2);
    }
}
