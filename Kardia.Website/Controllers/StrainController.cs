using Kardia.Sdk;
using Kardia.Website.Model;
using Kardia.Website.Model.Days;
using Microsoft.AspNetCore.Mvc;

namespace Kardia.Website.Controllers;

/// <summary>
/// The Strain screen.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class StrainController(KardiaClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Strain for a day.</summary>
    /// <param name="date">The day (today when empty).</param>
    /// <returns>The view.</returns>
    [HttpGet("/strain")]
    public async Task<IActionResult> Index(DateOnly? date = null) =>
        View(await new DayModel(ApiClient).PageAsync(Shell(new DayPageModel(), AppSection.Strain, date), UserId));
}
