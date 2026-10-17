using Kardia.Sdk;
using Kardia.Website.Model;
using Kardia.Website.Model.Days;
using Microsoft.AspNetCore.Mvc;

namespace Kardia.Website.Controllers;

/// <summary>
/// The Stress screen: the stress monitor and the energy bank.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class StressController(KardiaClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Stress and energy for a day.</summary>
    /// <param name="date">The day (today when empty).</param>
    /// <returns>The view.</returns>
    [HttpGet("/stress")]
    public async Task<IActionResult> Index(DateOnly? date = null) =>
        View(await new DayModel(ApiClient).PageAsync(Shell(new DayPageModel(), AppSection.Stress, date), UserId));
}
