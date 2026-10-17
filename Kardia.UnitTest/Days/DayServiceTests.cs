using Kardia.Library.Enums;

namespace Kardia.UnitTest.Days;

/// <summary>The Today screen on four months of demo data (deterministic, built around <see cref="TestSupport.Now"/>).</summary>
public sealed class DayServiceTests
{
    [Fact]
    public async Task TodayHasRecoverySleepAndStrain()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync();

        var response = await app.DayService.GetAsync(userId, null);

        Assert.True(response.IsSuccess);
        var day = response.Result!;
        Assert.Equal(TestSupport.Today, day.Date);
        Assert.True(day.HasData);
        Assert.InRange(day.Recovery.Score!.Value, 1, 99);
        Assert.NotEqual(RecoveryZone.None, day.Recovery.Zone);
        Assert.True(day.Sleep.HasNight);
        Assert.InRange(day.Sleep.Performance!.Value, 1, 100);
        Assert.NotEmpty(day.Sleep.Segments);
        Assert.True(day.Strain.Score > 0);
        Assert.InRange(day.Strain.Score!.Value, 0.1, 21);
        Assert.NotEmpty(day.Strain.Workouts);
        Assert.NotEmpty(day.HeartRates);
        Assert.Equal(14, day.Recent.Count);
        Assert.NotEmpty(day.Coach);
    }

    [Fact]
    public async Task TonightsNeedAddsUp()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync();

        var sleep = (await app.DayService.GetAsync(userId, null)).Result!.Sleep;

        Assert.Equal(sleep.TonightNeedMinutes, 450 + sleep.TonightStrainMinutes + sleep.TonightDebtMinutes);
        Assert.NotNull(sleep.Bedtime);
    }

    [Fact]
    public async Task ADayWithoutDataSaysSo()
    {
        var app = new TestApp();
        var userId = await app.CreateUserAsync();

        var day = (await app.DayService.GetAsync(userId, null)).Result!;

        Assert.False(day.HasData);
        Assert.Null(day.Recovery.Score);
    }

    [Fact]
    public async Task TheRangeScoresEveryDay()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync();

        var days = (await app.DayService.RangeAsync(userId, TestSupport.Today.AddDays(-29), TestSupport.Today)).Result!;

        Assert.Equal(30, days.Count);
        Assert.All(days, d => Assert.InRange(d.Recovery!.Value, 1, 99));
        Assert.Contains(days, d => d.Zone == RecoveryZone.High);
        Assert.Contains(days, d => d.Zone != RecoveryZone.High);
    }
}
