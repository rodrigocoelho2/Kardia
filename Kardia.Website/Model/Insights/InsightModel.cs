using Kardia.Library.Enums;
using Kardia.Sdk;
using Kardia.Sdk.Models;

namespace Kardia.Website.Model.Insights;

/// <summary>The Health Monitor screen: the day's five signs and the last 30 days of each.</summary>
public sealed class HealthPageModel : AppPageModel
{
    /// <summary>Gets or sets the day.</summary>
    public DayDTO Day { get; set; } = new();

    /// <summary>Gets or sets the last 30 days (for the small charts).</summary>
    public HealthReportDTO History { get; set; } = new();
}

/// <summary>The printable Health Monitor report.</summary>
public sealed class HealthReportPageModel : AppPageModel
{
    /// <summary>Gets or sets the report.</summary>
    public HealthReportDTO Report { get; set; } = new();

    /// <summary>Gets or sets the days covered (30 or 180).</summary>
    public int Days { get; set; } = 30;
}

/// <summary>The trends screen: 30, 90 or 365 days of every number.</summary>
public sealed class TrendsPageModel : AppPageModel
{
    /// <summary>Gets or sets the days shown (30, 90 or 365).</summary>
    public int Days { get; set; } = 30;

    /// <summary>Gets or sets one summary per day, oldest first.</summary>
    public List<DaySummaryDTO> Summaries { get; set; } = [];
}

/// <summary>The recovery calendar.</summary>
public sealed class CalendarPageModel : AppPageModel
{
    /// <summary>Gets or sets the first day of the month shown.</summary>
    public DateOnly Month { get; set; }

    /// <summary>Gets or sets one summary per day of the month (up to today), oldest first.</summary>
    public List<DaySummaryDTO> Summaries { get; set; } = [];
}

/// <summary>The weekly or monthly assessment.</summary>
public sealed class ReportPageModel : AppPageModel
{
    /// <summary>Gets or sets the report.</summary>
    public ReportDTO Report { get; set; } = new();
}

/// <summary>The weekly plan.</summary>
public sealed class PlanPageModel : AppPageModel
{
    /// <summary>Gets or sets the plan.</summary>
    public PlanDTO Plan { get; set; } = new();

    /// <summary>Gets or sets a message after a save.</summary>
    public string? Notice { get; set; }
}

/// <summary>The AI coach screen.</summary>
public sealed class AiCoachPageModel : AppPageModel
{
    /// <summary>Gets or sets the state and the last report.</summary>
    public AiCoachDTO Coach { get; set; } = new();
}

/// <summary>Loads the Health Monitor, trends, reports and the weekly plan.</summary>
/// <param name="apiClient">The API client.</param>
public sealed class InsightModel(KardiaClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Builds the Health Monitor screen.</summary>
    /// <param name="page">The page with the shell filled in.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<HealthPageModel> HealthAsync(HealthPageModel page, string userId)
    {
        var dayCall = CallAsync(() => ApiClient.DayGetAsync(userId, page.Date));
        var historyCall = CallAsync(() => ApiClient.DayHealthReportAsync(userId, 30));
        var day = await dayCall;
        page.LastSyncAt = day.Data?.LastSyncAt;
        if (!await OkAsync(page, day, userId))
        {
            return page;
        }

        page.Day = day.Data!;
        page.Date = day.Data!.Date;
        page.History = (await historyCall).Data ?? new HealthReportDTO();
        return page;
    }

    /// <summary>Builds the printable report.</summary>
    /// <param name="page">The page with the shell filled in.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<HealthReportPageModel> HealthReportAsync(HealthReportPageModel page, string userId)
    {
        page.Days = page.Days > 90 ? 180 : 30;
        var report = await CallAsync(() => ApiClient.DayHealthReportAsync(userId, page.Days));
        if (await OkAsync(page, report, userId))
        {
            page.Report = report.Data!;
        }

        return page;
    }

    /// <summary>Builds the trends screen.</summary>
    /// <param name="page">The page with the shell filled in.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<TrendsPageModel> TrendsAsync(TrendsPageModel page, string userId)
    {
        page.Days = page.Days switch { >= 180 => 365, >= 60 => 90, _ => 30 };
        var range = await CallAsync(() => ApiClient.DayRangeAsync(userId, page.Today.AddDays(-(page.Days - 1)), page.Today));
        if (await OkAsync(page, range, userId))
        {
            page.Summaries = range.Data!;
        }

        return page;
    }

    /// <summary>Builds the recovery calendar.</summary>
    /// <param name="page">The page with the shell filled in (its date is a day in the month).</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<CalendarPageModel> CalendarAsync(CalendarPageModel page, string userId)
    {
        page.Month = new DateOnly(page.Date.Year, page.Date.Month, 1);
        var last = page.Month.AddMonths(1).AddDays(-1);
        var range = await CallAsync(() => ApiClient.DayRangeAsync(userId, page.Month, last < page.Today ? last : page.Today));
        if (await OkAsync(page, range, userId))
        {
            page.Summaries = range.Data!;
        }

        return page;
    }

    /// <summary>Builds the assessment screen.</summary>
    /// <param name="page">The page with the shell filled in.</param>
    /// <param name="userId">The account.</param>
    /// <param name="period">Week or month.</param>
    /// <returns>The page.</returns>
    public async Task<ReportPageModel> ReportAsync(ReportPageModel page, string userId, ReportPeriod period)
    {
        var report = await CallAsync(() => ApiClient.ReportGetAsync(userId, period, page.Date));
        if (await OkAsync(page, report, userId))
        {
            page.Report = report.Data!;
        }

        return page;
    }

    /// <summary>Builds the weekly plan.</summary>
    /// <param name="page">The page with the shell filled in.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<PlanPageModel> PlanAsync(PlanPageModel page, string userId)
    {
        var plan = await CallAsync(() => ApiClient.PlanGetAsync(userId));
        if (await OkAsync(page, plan, userId))
        {
            page.Plan = plan.Data!;
        }

        return page;
    }

    /// <summary>Takes the targets the last four weeks suggest.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The saved settings, or the errors.</returns>
    public async Task<AjaxDataResult<SettingDTO>> UseSuggestedAsync(string userId)
    {
        var plan = await CallAsync(() => ApiClient.PlanGetAsync(userId));
        var setting = await CallAsync(() => ApiClient.SettingGetAsync(userId));
        if (!plan.IsValid || !setting.IsValid || plan.Data is null || setting.Data is null)
        {
            return new AjaxDataResult<SettingDTO> { IsValid = false, ValidationErrors = [.. plan.ValidationErrors, .. setting.ValidationErrors] };
        }

        double Suggested(string key) => plan.Data.Goals.FirstOrDefault(goal => goal.Key == key)?.Suggested ?? 0;
        var item = setting.Data;
        item.SleepNightsWeek = (int)Suggested("sleep");
        item.BedtimeNightsWeek = (int)Suggested("bedtime");
        item.ActiveMinutesWeek = (int)Suggested("active");
        item.StepsDay = (int)Suggested("steps");
        return await CallAsync(() => ApiClient.SettingSaveAsync(item));
    }

    /// <summary>Builds the AI coach screen.</summary>
    /// <param name="page">The page with the shell filled in.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<AiCoachPageModel> AiCoachAsync(AiCoachPageModel page, string userId)
    {
        var coach = await CallAsync(() => ApiClient.AiCoachGetAsync(userId));
        if (await OkAsync(page, coach, userId))
        {
            page.Coach = coach.Data!;
        }

        return page;
    }

    /// <summary>The AI coach's state (for the page that waits for the report).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    /// <param name="topic">What the report looks at.</param>
    /// <param name="date">The day.</param>
    public Task<AjaxDataResult<AiCoachDTO>> AiCoachStateAsync(string userId, AiCoachTopic topic = AiCoachTopic.Overview, DateOnly? date = null) =>
        CallAsync(() => ApiClient.AiCoachGetAsync(userId, topic, date));

    /// <summary>Starts an analysis.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    /// <param name="topic">What the report looks at.</param>
    /// <param name="date">The day.</param>
    public Task<AjaxDataResult<AiCoachDTO>> AiCoachAnalyseAsync(string userId, AiCoachTopic topic = AiCoachTopic.Overview, DateOnly? date = null) =>
        CallAsync(() => ApiClient.AiCoachAnalyseAsync(userId, topic, date));

    private async Task<bool> OkAsync<T>(AppPageModel page, AjaxDataResult<T> result, string userId)
    {
        if (result.IsValid && result.Data is not null)
        {
            // The sidebar shows when the Apple Watch last synced.
            page.LastSyncAt ??= (await CallAsync(() => ApiClient.SyncKeyGetAsync(userId))).Data?.LastUsedAt;
            return true;
        }

        page.Unavailable = result.ValidationErrors.FirstOrDefault()?.Message ?? ApiUnavailableMessage;
        return false;
    }
}
