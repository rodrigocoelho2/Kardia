using Kardia.Sdk;
using Kardia.Website.Model;
using Kardia.Website.Model.Ages;
using Microsoft.AspNetCore.Mvc;

namespace Kardia.Website.Controllers;

/// <summary>
/// The biological age screen.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class AgeController(KardiaClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Your biological age.</summary>
    /// <returns>The view.</returns>
    [HttpGet("/age")]
    public async Task<IActionResult> Index() =>
        View(await new AgeModel(ApiClient).PageAsync(Shell(new AgePageModel(), AppSection.Age), UserId));
}
