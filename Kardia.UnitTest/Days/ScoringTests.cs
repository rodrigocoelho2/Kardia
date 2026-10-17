using Kardia.Library.Enums;
using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.UnitTest.Days;

public sealed class ScoringTests
{
    private static RecoveryInputs Inputs(double hrv, double resting, double sleepPerformance) => new(
        hrv, Baseline.Of(Enumerable.Repeat(60.0, 20).Select((v, i) => v + (i % 5) - 2), 60, 12),
        resting, Baseline.Of(Enumerable.Repeat(54.0, 20).Select((v, i) => v + (i % 3) - 1), 54, 3),
        14.6, Baseline.Of(Enumerable.Repeat(14.6, 20), 14.6, 0.6),
        null, Baseline.Of([], 0, 0.3),
        sleepPerformance);

    [Fact]
    public void RecoveryRisesWithHrvAndFallsWithRestingHeartRate()
    {
        var normal = RecoveryScore.Calculate(Inputs(60, 54, 88)).Score!.Value;
        var better = RecoveryScore.Calculate(Inputs(72, 51, 95)).Score!.Value;
        var worse = RecoveryScore.Calculate(Inputs(45, 60, 70)).Score!.Value;

        Assert.True(better > normal, $"{better} > {normal}");
        Assert.True(worse < normal, $"{worse} < {normal}");
        Assert.InRange(better, 1, 99);
        Assert.InRange(worse, 1, 99);
    }

    [Fact]
    public void RecoveryNeedsHeartData()
    {
        var result = RecoveryScore.Calculate(Inputs(60, 54, 88) with { Hrv = null, RestingHeartRate = null });

        Assert.Null(result.Score);
        Assert.Equal(RecoveryZone.None, result.Zone);
    }

    [Theory]
    [InlineData(99, RecoveryZone.High)]
    [InlineData(67, RecoveryZone.High)]
    [InlineData(66, RecoveryZone.Medium)]
    [InlineData(34, RecoveryZone.Medium)]
    [InlineData(33, RecoveryZone.Low)]
    [InlineData(1, RecoveryZone.Low)]
    public void ZonesFollowTheScore(int score, RecoveryZone zone) => Assert.Equal(zone, RecoveryScore.ZoneOf(score));

    [Fact]
    public void TheStrainTargetFollowsRecovery()
    {
        var low = RecoveryScore.StrainTarget(20);
        var high = RecoveryScore.StrainTarget(90);

        Assert.True(high.Low > low.Low);
        Assert.Equal(2.0, high.High - high.Low, 1);
        Assert.InRange(high.High, 0, 21);
    }

    [Fact]
    public void StrainGrowsWithLoadButNeverPasses21()
    {
        Assert.Equal(0, StrainScore.FromTrimp(0));
        Assert.True(StrainScore.FromTrimp(100) > StrainScore.FromTrimp(50));
        Assert.True(StrainScore.FromTrimp(5000) <= 21);
    }

    [Fact]
    public void AnHourOfRunningLandsInTheHigherZones()
    {
        var start = new DateTime(2026, 10, 16, 8, 0, 0);
        var minutes = Enumerable.Range(0, 60).Select(i => new HeartRateRow { MinuteAt = start.AddMinutes(i), Bpm = 160 }).ToList();

        var load = StrainScore.Load(minutes, 52, 190, BiologicalSex.Male);

        Assert.True(load.Trimp > 50, $"TRIMP {load.Trimp}");
        Assert.True(load.ZoneSeconds[3] >= 59 * 60, "zone 4 (80–90% of 190 bpm)");
        Assert.InRange(StrainScore.FromTrimp(load.Trimp), 8, 18);
    }

    [Fact]
    public void RestingAllDayBarelyStrains()
    {
        var start = new DateTime(2026, 10, 16);
        var minutes = Enumerable.Range(0, 1440).Select(i => new HeartRateRow { MinuteAt = start.AddMinutes(i), Bpm = 58 }).ToList();

        var load = StrainScore.Load(minutes, 52, 190, BiologicalSex.Female);

        Assert.True(StrainScore.FromTrimp(load.Trimp) < 4);
        Assert.Equal(0, load.ZoneSeconds.Sum());
    }

    [Fact]
    public void SleepNeedAddsStrainAndDebt()
    {
        var rested = SleepScore.Need(450, 6, [0, 0, 0]);
        var tired = SleepScore.Need(450, 14, [60, 90, 30]);

        Assert.Equal(450, rested.TotalMinutes);
        Assert.Equal(24, tired.StrainMinutes);
        Assert.True(tired.DebtMinutes > 0 && tired.DebtMinutes <= 45);
        Assert.True(tired.TotalMinutes > rested.TotalMinutes);
    }

    [Fact]
    public void SleepPerformanceIsCappedAt100()
    {
        Assert.Equal(100, SleepScore.Performance(600, 450));
        Assert.Equal(50, SleepScore.Performance(225, 450));
    }
}
