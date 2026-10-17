using Kardia.Sdk;
using Kardia.Website.Helpers;
using Kardia.Website.Model;
using Kardia.Website.Security;
using Microsoft.AspNetCore.Mvc;

namespace Kardia.Website.Controllers;

/// <summary>
/// Base of every signed-in Website controller. Controllers are thin: fill in the shell, create the model, call it, return its result.
/// </summary>
/// <param name="apiClient">The Kardia.WebApi client handed to the models.</param>
[Microsoft.AspNetCore.Authorization.Authorize]
public abstract class WebsiteBaseController(KardiaClient apiClient) : Controller
{
    /// <summary>Gets the API client.</summary>
    protected KardiaClient ApiClient { get; } = apiClient;

    /// <summary>Gets the signed-in account's id.</summary>
    protected string UserId => KardiaAuthentication.UserId(User);

    /// <summary>
    /// Fills in the shell of a page: who's signed in, the tab and the day.
    /// </summary>
    /// <typeparam name="T">The page model.</typeparam>
    /// <param name="page">The page.</param>
    /// <param name="section">The tab.</param>
    /// <param name="date">The day asked for (today when null or in the future).</param>
    /// <returns>The page.</returns>
    protected T Shell<T>(T page, AppSection section, DateOnly? date = null)
        where T : AppPageModel
    {
        var today = Clock.Today;
        page.UserName = User.Identity?.Name ?? string.Empty;
        page.IsAdmin = User.IsInRole("Admin");
        page.Section = section;
        page.Today = today;
        page.Date = date is { } day && day <= today ? day : today;
        return page;
    }
}
