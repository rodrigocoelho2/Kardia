using Kardia.Library.Enums;
using Kardia.Sdk;
using Kardia.Sdk.Models;

namespace Kardia.Website.Model.Journals;

/// <summary>
/// The journal screen.
/// </summary>
public sealed class JournalPageModel : AppPageModel
{
    /// <summary>Gets or sets the day's journal.</summary>
    public JournalDayDTO Journal { get; set; } = new();
}

/// <summary>
/// Loads the journal and marks habits.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class JournalModel(KardiaClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>
    /// Builds the screen.
    /// </summary>
    /// <param name="page">The page with the shell filled in.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<JournalPageModel> PageAsync(JournalPageModel page, string userId)
    {
        var journalCall = CallAsync(() => ApiClient.JournalGetAsync(userId, page.Date));
        var syncCall = CallAsync(() => ApiClient.SyncKeyGetAsync(userId));
        var journal = await journalCall;
        page.LastSyncAt = (await syncCall).Data?.LastUsedAt;
        if (!journal.IsValid || journal.Data is null)
        {
            page.Unavailable = journal.ValidationErrors.FirstOrDefault()?.Message ?? ApiUnavailableMessage;
            return page;
        }

        page.Journal = journal.Data;
        page.Date = journal.Data.Date;
        return page;
    }

    /// <summary>
    /// Marks or unmarks a habit.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The day.</param>
    /// <param name="tag">The habit.</param>
    /// <param name="on">Marked or not.</param>
    /// <returns>The day's journal.</returns>
    public Task<AjaxDataResult<JournalDayDTO>> SetAsync(string userId, DateOnly date, JournalTag tag, bool on) =>
        CallAsync(() => ApiClient.JournalSetAsync(new JournalSetRequest { UserId = userId, Date = date, Tag = tag, On = on }));
}
