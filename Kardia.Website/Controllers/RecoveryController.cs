using Kardia.Sdk;
using Kardia.Website.Model;
using Kardia.Website.Model.Days;
using Microsoft.AspNetCore.Mvc;

namespace Kardia.Website.Controllers;

/// <summary>
/// The Recovery screen.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class RecoveryController(KardiaClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Recovery for a day.</summary>
    /// <param name="date">The day (today when empty).</param>
    /// <returns>The view.</returns>
    [HttpGet("/recovery")]
    public async Task<IActionResult> Index(DateOnly? date = null) =>
        View(await new DayModel(ApiClient).PageAsync(Shell(new DayPageModel(), AppSection.Recovery, date), UserId));
}
