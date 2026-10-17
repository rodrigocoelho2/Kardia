using Kardia.Sdk;
using Kardia.Sdk.Models;

namespace Kardia.Website.Model.Days;

/// <summary>
/// The Today, Recovery, Sleep and Strain screens: one day from the WebApi (and, on Today, the biological age and the
/// journal for their cards).
/// </summary>
public sealed class DayPageModel : AppPageModel
{
    /// <summary>Gets or sets the day.</summary>
    public DayDTO Day { get; set; } = new();

    /// <summary>Gets or sets the biological age (Today only).</summary>
    public AgeDTO? Age { get; set; }

    /// <summary>Gets or sets the day's journal (Today only).</summary>
    public JournalDayDTO? Journal { get; set; }

    /// <summary>Gets or sets the AI coach (Today only: its button and last headline).</summary>
    public AiCoachDTO? AiCoach { get; set; }

    /// <summary>Gets or sets this week's plan (Today only).</summary>
    public PlanDTO? Plan { get; set; }
}

/// <summary>
/// Loads the day screens.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class DayModel(KardiaClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>
    /// Builds a day screen.
    /// </summary>
    /// <param name="page">The page with the shell filled in.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page (with <see cref="AppPageModel.Unavailable"/> set when the WebApi can't be reached).</returns>
    public async Task<DayPageModel> PageAsync(DayPageModel page, string userId)
    {
        var day = await CallAsync(() => ApiClient.DayGetAsync(userId, page.Date));
        if (!day.IsValid || day.Data is null)
        {
            page.Unavailable = day.ValidationErrors.FirstOrDefault()?.Message ?? ApiUnavailableMessage;
            return page;
        }

        page.Day = day.Data;
        page.LastSyncAt = day.Data.LastSyncAt;
        page.Date = day.Data.Date;
        if (page.Section == AppSection.Today)
        {
            // The cards of Today, fetched together.
            var age = CallAsync(() => ApiClient.AgeGetAsync(userId));
            var journal = CallAsync(() => ApiClient.JournalGetAsync(userId, page.Date));
            var plan = CallAsync(() => ApiClient.PlanGetAsync(userId));
            var coach = CallAsync(() => ApiClient.AiCoachGetAsync(userId));
            await Task.WhenAll(age, journal, plan, coach);
            page.Age = age.Result.Data;
            page.Journal = journal.Result.Data;
            page.Plan = plan.Result.Data;
            page.AiCoach = coach.Result.Data;
        }

        return page;
    }
}
