using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Plans.Services;
using Kardia.WebApi.Features.Reports.Services;
using Kardia.WebApi.Features.Settings.Models.DTOs;

namespace Kardia.UnitTest.Insights;

/// <summary>Health Monitor, cardio load, the weekly plan, the assessments and the goals, on the demo data.</summary>
public sealed class InsightTests
{
    [Theory]
    [InlineData(0.5, CardioLoadStatus.Detraining)]
    [InlineData(0.9, CardioLoadStatus.Maintaining)]
    [InlineData(1.0, CardioLoadStatus.Productive)]
    [InlineData(1.3, CardioLoadStatus.Productive)]
    [InlineData(1.4, CardioLoadStatus.Overreaching)]
    [InlineData(1.8, CardioLoadStatus.Overtraining)]
    public void CardioLoadBands_FollowTheRatio(double ratio, CardioLoadStatus expected) =>
        Assert.Equal(expected, CardioLoad.StatusOf(ratio));

    [Fact]
    public async Task TheDemoDay_HasAHealthMonitorAndACardioLoad()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync();

        var day = (await app.DayService.GetAsync(user, null)).Result!;

        Assert.True(day.Health.Ready);
        Assert.Equal(["hrv", "resting", "respiratory", "oxygen", "temperature"], day.Health.Signs.Select(static s => s.Key));
        Assert.All(day.Health.Signs.Where(static s => s.Status != HealthSignStatus.None), static s => Assert.True(s.Low <= s.Typical && s.Typical <= s.High));
        Assert.True(day.CardioLoad.Ready);
        Assert.InRange(day.CardioLoad.Ratio!.Value, 0.3, 2.5);
        Assert.True(day.CardioLoad.StrainLow < day.CardioLoad.StrainHigh);
        Assert.InRange(day.CardioLoad.History.Count, 50, 57);
    }

    [Fact]
    public async Task TheHealthReport_CoversThirtyOrHundredEightyDays()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync();

        var month = (await app.DayService.HealthReportAsync(user, 30)).Result!;
        var half = (await app.DayService.HealthReportAsync(user, 999)).Result!;

        Assert.Equal(TestSupport.Today.AddDays(-29), month.From);
        Assert.All(month.Signs, static s => Assert.Equal(30, s.Days.Count));
        Assert.Equal(TestSupport.Today.AddDays(-179), half.From);
        Assert.True(month.DaysMeasured > 20);
    }

    [Fact]
    public async Task ARangeOfDays_CarriesTheNewNumbers()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync();

        var days = (await app.DayService.RangeAsync(user, TestSupport.Today.AddDays(-6), TestSupport.Today)).Result!;

        Assert.Contains(days, static d => d.ActiveMinutes > 0);
        Assert.Contains(days, static d => d.Steps > 0);
        Assert.Contains(days, static d => d.BedStart is not null && d.NeedMinutes > 0);
    }

    [Fact]
    public async Task TheWeeklyPlan_RunsMondayToSunday_WithFourGoals()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync();

        var plan = (await app.PlanService.GetAsync(user)).Result!;

        Assert.Equal(new DateOnly(2026, 10, 12), plan.WeekStart);
        Assert.Equal(new DateOnly(2026, 10, 18), plan.WeekEnd);
        Assert.Equal(3, plan.DaysLeft);
        Assert.Equal(["sleep", "bedtime", "active", "steps"], plan.Goals.Select(static g => g.Key));
        Assert.All(plan.Goals, static g => Assert.Equal(7, g.Days.Count));
        Assert.All(plan.Goals, static g => Assert.InRange(g.Percent, 0, 100));
        Assert.Equal(2, plan.Goals[0].Days.Count(static d => d.Future));
        Assert.InRange(plan.Goals.Single(static g => g.Key == "steps").Suggested, 4000, 20000);
        Assert.NotNull(plan.UsualBedtime);
        Assert.Equal(4, plan.LastWeek.Count);
        Assert.Equal(new DateOnly(2026, 10, 12), WeeklyPlan.Monday(new DateOnly(2026, 10, 18)));
    }

    [Fact]
    public async Task TheWeeklyReport_ComparesWithTheWeekBefore()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync();

        var report = (await app.ReportService.GetAsync(user, ReportPeriod.Week, null)).Result!;

        Assert.Equal(new DateOnly(2026, 10, 12), report.From);
        Assert.Equal(new DateOnly(2026, 10, 5), report.PreviousFrom);
        Assert.True(report.InProgress);
        Assert.Equal(7, report.Days.Count);
        Assert.Equal(5, report.DaysWithData);
        Assert.NotNull(report.Score);
        Assert.Contains(report.Assessment, new[] { "Peak", "Strong", "Fair", "Off track" });
        Assert.Contains(report.Metrics, static m => m.Key == "hrv" && m.Value is not null && m.Previous is not null);
        Assert.NotEmpty(report.Highlights);
        Assert.Equal(report.RecoveryDays, report.HighDays + report.MediumDays + report.LowDays);
    }

    [Fact]
    public async Task TheMonthlyReport_CoversTheCalendarMonth()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync();

        var report = (await app.ReportService.GetAsync(user, ReportPeriod.Month, new DateOnly(2026, 9, 10))).Result!;

        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)), (report.From, report.To));
        Assert.Equal("September 2026", report.Title);
        Assert.False(report.InProgress);
        Assert.Equal(30, report.Days.Count);
    }

    [Fact]
    public async Task Goals_HaveDefaults_AndAreChecked()
    {
        var app = new TestApp();
        var user = await app.CreateUserAsync();

        var defaults = (await app.SettingService.GetAsync(user)).Result!;
        var bad = await app.SettingService.SaveAsync(new SettingDTO { UserId = user, SleepGoalPercent = 90, StepsDay = -1 });

        Assert.Equal((100, 150, 8000, true, false), (defaults.SleepGoalPercent, defaults.ActiveMinutesWeek, defaults.StepsDay, defaults.NotifyMorning, defaults.NotifyStrain));
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, bad.Status);
        Assert.Equal(2, bad.Problems!.Errors.Count);
    }

    [Fact]
    public async Task ALowerSleepGoal_MovesTonightsBedtimeLater()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync();
        var peak = (await app.DayService.GetAsync(user, null)).Result!.Sleep;

        var setting = (await app.SettingService.GetAsync(user)).Result!;
        setting.SleepGoalPercent = 85;
        Assert.True((await app.SettingService.SaveAsync(setting)).IsSuccess);
        var perform = (await app.DayService.GetAsync(user, null)).Result!.Sleep;

        Assert.Equal(100, peak.GoalPercent);
        Assert.Equal(peak.TonightNeedMinutes, peak.TonightGoalMinutes);
        Assert.Equal(85, perform.GoalPercent);
        Assert.Equal(SleepScore.Goal(perform.TonightNeedMinutes, 85), perform.TonightGoalMinutes);
        Assert.True(perform.Bedtime > peak.Bedtime);
        Assert.Equal(peak.WakeAt, perform.WakeAt);
    }

    [Fact]
    public void PeriodRanges_StartOnMondayOrTheFirst()
    {
        Assert.Equal(new DateOnly(2026, 10, 12), PerformanceReport.Range(ReportPeriod.Week, new DateOnly(2026, 10, 16)).From);
        Assert.Equal(new DateOnly(2026, 10, 12), PerformanceReport.Range(ReportPeriod.Week, new DateOnly(2026, 10, 12)).From);
        Assert.Equal(new DateOnly(2026, 2, 28), PerformanceReport.Range(ReportPeriod.Month, new DateOnly(2026, 2, 3)).To);
    }
}
