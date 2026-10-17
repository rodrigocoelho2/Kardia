using Kardia.Sdk;
using Kardia.Website.Model;
using Kardia.Website.Model.Dashboard;
using Kardia.Website.Model.Days;
using Kardia.Website.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kardia.Website.Controllers;

/// <summary>
/// Today, and the public page that explains Kardia.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="options">The sign-in settings (whether anyone may create an account).</param>
public sealed class DashboardController(KardiaClient apiClient, IOptions<WebsiteOptions> options) : WebsiteBaseController(apiClient)
{
    /// <summary>
    /// Signed in: Today. Signed out: the public page with the way in.
    /// </summary>
    /// <param name="date">The day (today when empty).</param>
    /// <returns>The view.</returns>
    [HttpGet("/")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(DateOnly? date = null)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View("Landing", new LandingModel { CanCreateAccount = options.Value.AllowRegistration });
        }

        var model = new DayModel(ApiClient);
        return View("Index", await model.PageAsync(Shell(new DayPageModel(), AppSection.Today, date), UserId));
    }

    /// <summary>
    /// The public page that explains Kardia (also reachable when signed in).
    /// </summary>
    /// <returns>The view.</returns>
    [HttpGet("/about")]
    [AllowAnonymous]
    public IActionResult Landing() => View("Landing", new LandingModel
    {
        SignedIn = User.Identity?.IsAuthenticated == true,
        CanCreateAccount = options.Value.AllowRegistration,
    });
}
