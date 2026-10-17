using Kardia.Library.Enums;
using Kardia.Sdk;
using Kardia.Website.Model;
using Kardia.Website.Model.Insights;
using Microsoft.AspNetCore.Mvc;

namespace Kardia.Website.Controllers;

/// <summary>
/// Health Monitor, trends, the weekly and monthly assessments, the weekly plan, and the phone's "More" menu.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class InsightController(KardiaClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>The Health Monitor.</summary>
    /// <param name="date">The day (today when empty).</param>
    /// <returns>The view.</returns>
    [HttpGet("/health")]
    public async Task<IActionResult> Health(DateOnly? date = null) =>
        View("Health", await new InsightModel(ApiClient).HealthAsync(Shell(new HealthPageModel(), AppSection.Health, date), UserId));

    /// <summary>The Health Monitor report to print or save as PDF.</summary>
    /// <param name="days">30 or 180.</param>
    /// <returns>The view.</returns>
    [HttpGet("/health/report")]
    public async Task<IActionResult> HealthReport(int days = 30) =>
        View("HealthReport", await new InsightModel(ApiClient).HealthReportAsync(Shell(new HealthReportPageModel { Days = days }, AppSection.Health), UserId));

    /// <summary>Trends over 30, 90 or 365 days.</summary>
    /// <param name="days">30, 90 or 365.</param>
    /// <returns>The view.</returns>
    [HttpGet("/trends")]
    public async Task<IActionResult> Trends(int days = 30) =>
        View("Trends", await new InsightModel(ApiClient).TrendsAsync(Shell(new TrendsPageModel { Days = days }, AppSection.Trends), UserId));

    /// <summary>The weekly or monthly assessment.</summary>
    /// <param name="period">week or month.</param>
    /// <param name="date">A day inside it.</param>
    /// <returns>The view.</returns>
    [HttpGet("/report")]
    public async Task<IActionResult> Report(string? period = null, DateOnly? date = null) =>
        View("Report", await new InsightModel(ApiClient).ReportAsync(Shell(new ReportPageModel(), AppSection.Report, date), UserId,
            string.Equals(period, "month", StringComparison.OrdinalIgnoreCase) ? ReportPeriod.Month : ReportPeriod.Week));

    /// <summary>The weekly plan.</summary>
    /// <param name="saved">Set after the targets were changed.</param>
    /// <returns>The view.</returns>
    [HttpGet("/plan")]
    public async Task<IActionResult> Plan(string? saved = null)
    {
        var page = await new InsightModel(ApiClient).PlanAsync(Shell(new PlanPageModel(), AppSection.Plan), UserId);
        page.Notice = saved == "targets" ? "New targets saved. They count from this week." : null;
        return View("Plan", page);
    }

    /// <summary>Takes the targets the last four weeks suggest.</summary>
    /// <returns>Back to the plan.</returns>
    [HttpPost("/plan/suggested")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UseSuggested()
    {
        await new InsightModel(ApiClient).UseSuggestedAsync(UserId);
        return Redirect("/plan?saved=targets");
    }

    /// <summary>The recovery calendar: a month of days, coloured by recovery.</summary>
    /// <param name="month">A day in the month (this month when empty).</param>
    /// <returns>The view.</returns>
    [HttpGet("/calendar")]
    public async Task<IActionResult> Calendar(DateOnly? month = null) =>
        View("Calendar", await new InsightModel(ApiClient).CalendarAsync(Shell(new CalendarPageModel(), AppSection.Calendar, month), UserId));

    /// <summary>Guided breathing.</summary>
    /// <returns>The view.</returns>
    [HttpGet("/breathe")]
    public IActionResult Breathe() => View("Breathe", Shell(new AppPageModel(), AppSection.Breathe));

    /// <summary>The AI coach.</summary>
    /// <returns>The view.</returns>
    [HttpGet("/coach")]
    public async Task<IActionResult> AiCoach() =>
        View("AiCoach", await new InsightModel(ApiClient).AiCoachAsync(Shell(new AiCoachPageModel(), AppSection.AiCoach), UserId));

    /// <summary>The AI coach's state for a screen, for the card waiting on a report.</summary>
    /// <param name="topic">What the report looks at (overview, recovery, sleep…).</param>
    /// <param name="date">The day.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet("/coach/state")]
    public async Task<IActionResult> AiCoachState(AiCoachTopic topic = AiCoachTopic.Overview, DateOnly? date = null) =>
        Json(await new InsightModel(ApiClient).AiCoachStateAsync(UserId, topic, date));

    /// <summary>Starts an analysis for a screen.</summary>
    /// <param name="topic">What the report looks at.</param>
    /// <param name="date">The day.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost("/coach/analyse")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AiCoachAnalyse([FromForm] AiCoachTopic topic = AiCoachTopic.Overview, [FromForm] DateOnly? date = null) =>
        Json(await new InsightModel(ApiClient).AiCoachAnalyseAsync(UserId, topic, date));

    /// <summary>Everything that isn't in the phone's tab bar.</summary>
    /// <returns>The view.</returns>
    [HttpGet("/more")]
    public IActionResult More() => View("More", Shell(new AppPageModel(), AppSection.More));
}
