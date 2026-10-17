using Kardia.Sdk;
using Kardia.Website.Model;
using Kardia.Website.Model.Days;
using Microsoft.AspNetCore.Mvc;

namespace Kardia.Website.Controllers;

/// <summary>
/// The Sleep screen.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class SleepController(KardiaClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>The night that ended on a day.</summary>
    /// <param name="date">The day (today when empty).</param>
    /// <returns>The view.</returns>
    [HttpGet("/sleep")]
    public async Task<IActionResult> Index(DateOnly? date = null) =>
        View(await new DayModel(ApiClient).PageAsync(Shell(new DayPageModel(), AppSection.Sleep, date), UserId));
}
