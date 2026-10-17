using Kardia.Library.Enums;
using Kardia.Sdk;
using Kardia.Website.Model;
using Kardia.Website.Model.Journals;
using Microsoft.AspNetCore.Mvc;

namespace Kardia.Website.Controllers;

/// <summary>
/// The journal: habits per day and what they do to recovery.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class JournalController(KardiaClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>The journal of a day.</summary>
    /// <param name="date">The day (today when empty).</param>
    /// <returns>The view.</returns>
    [HttpGet("/journal")]
    public async Task<IActionResult> Index(DateOnly? date = null) =>
        View(await new JournalModel(ApiClient).PageAsync(Shell(new JournalPageModel(), AppSection.Journal, date), UserId));

    /// <summary>Marks or unmarks a habit.</summary>
    /// <param name="date">The day.</param>
    /// <param name="tag">The habit.</param>
    /// <param name="on">Marked or not.</param>
    /// <returns>JSON <c>AjaxDataResult</c> with the day's journal.</returns>
    [HttpPost("/journal/set")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAsync([FromForm] DateOnly date, [FromForm] JournalTag tag, [FromForm] bool on) =>
        Json(await new JournalModel(ApiClient).SetAsync(UserId, date, tag, on));
}
