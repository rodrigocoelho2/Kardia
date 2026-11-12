using System.Security.Cryptography;
using System.Text;
using Kardia.Library.Enums;
using Kardia.Sdk;
using Kardia.Sdk.Models;
using Kardia.Website.Model.Account;
using Kardia.Website.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kardia.Website.Controllers;

/// <summary>
/// Sign in, create an account and sign out. Accounts are kept by the WebApi; passwords never reach the logs.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="options">Sign-in settings.</param>
/// <param name="throttle">Slows down password guessing.</param>
/// <param name="logger">Logger.</param>
[AllowAnonymous]
public sealed class AccountController(
    KardiaClient apiClient,
    IOptions<WebsiteOptions> options,
    LoginThrottle throttle,
    ILogger<AccountController> logger) : Controller
{
    private const string Unreachable = "Can't reach Kardia's engine right now. Check that the kardia-webapi container is running, then try again.";
    private static readonly TimeSpan FailureDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>The sign-in form (the create-account form on the very first visit).</summary>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>The view or a redirect.</returns>
    /// <param name="discord">What went wrong with Discord, for a note (not-linked, failed).</param>
    [HttpGet("/sign-in")]
    public async Task<IActionResult> SignIn(string? returnUrl = null, string? discord = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        var model = await PageModelAsync(returnUrl);
        if (discord == "not-linked")
        {
            model.Errors = ["No account here is linked to that Discord account yet. Sign in with your password, then link Discord in Settings."];
        }
        else if (discord == "failed")
        {
            model.Errors = ["Signing in with Discord didn't finish. Try again, or use your password."];
        }

        if (model.IsFirstAccount)
        {
            return RedirectToAction(nameof(CreateAccount), new { returnUrl = model.ReturnUrl });
        }

        return View("SignIn", model);
    }

    /// <summary>Checks the user name and password and signs the browser in.</summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    /// <param name="remember">Keep the session on this device.</param>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>A redirect, or the form again with the error.</returns>
    [HttpPost("/sign-in")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignInAsync([FromForm] string? userName, [FromForm] string? password, [FromForm] bool remember, [FromForm] string? returnUrl)
    {
        var model = await PageModelAsync(returnUrl);
        model.UserName = (userName ?? string.Empty).Trim();
        model.Remember = remember;

        if (Throttled(model))
        {
            return View("SignIn", model);
        }

        try
        {
            var user = await apiClient.UserSignInAsync(new UserSignInRequest { UserName = model.UserName, Password = password });
            throttle.Succeeded(Address);
            await KardiaAuthentication.SignInAsync(HttpContext, user, remember, options.Value.RememberDays);
            return LocalRedirect(model.ReturnUrl);
        }
        catch (KardiaApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            throttle.Failed(Address);
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Failed sign-in for {UserName} from {Address}", model.UserName, Address);

            await Task.Delay(FailureDelay);
            model.Errors = ["Wrong user name or password."];
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return View("SignIn", model);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or KardiaApiException)
        {
            model.Errors = [Unreachable];
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("SignIn", model);
        }
    }

    /// <summary>Sends the browser to Discord to sign in (to the account that Discord account is linked to).</summary>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>A redirect to Discord.</returns>
    [HttpGet("/sign-in/discord")]
    public IActionResult DiscordSignIn(string? returnUrl = null)
    {
        if (!options.Value.DiscordEnabled)
        {
            return NotFound();
        }

        var properties = new AuthenticationProperties { RedirectUri = $"/sign-in/discord/done?returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}" };
        properties.Items[DiscordAuthentication.ModeKey] = "signin";
        return Challenge(properties, DiscordAuthentication.Scheme);
    }

    /// <summary>
    /// Back from Discord: signs in to the account the Discord account is linked to, or (from Settings) links it to the
    /// signed-in account. A Discord account that isn't linked goes on to pick a user name for a new account when new
    /// accounts are allowed; otherwise it doesn't get in.
    /// </summary>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>A redirect.</returns>
    [HttpGet("/sign-in/discord/done")]
    public async Task<IActionResult> DiscordDone(string? returnUrl = null)
    {
        if (!options.Value.DiscordEnabled)
        {
            return NotFound();
        }

        var account = await DiscordAuthentication.PeekAsync(HttpContext);
        if (account is null)
        {
            return Redirect($"{KardiaAuthentication.SignInPath}?discord=failed");
        }

        if (account.Mode == "link")
        {
            await DiscordAuthentication.ForgetAsync(HttpContext);
            var me = KardiaAuthentication.UserId(User);
            if (User.Identity?.IsAuthenticated != true || me.Length == 0)
            {
                return Redirect(KardiaAuthentication.SignInPath);
            }

            try
            {
                await apiClient.UserDiscordLinkAsync(new UserDiscordLinkRequest { Id = me, DiscordId = account.Id, DiscordName = account.Name });
                return Redirect("/settings?discord=linked#discord");
            }
            catch (KardiaApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return Redirect("/settings?discord=taken#discord");
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or KardiaApiException)
            {
                return Redirect("/settings?discord=failed#discord");
            }
        }

        try
        {
            var user = await apiClient.UserDiscordSignInAsync(new UserDiscordSignInRequest { DiscordId = account.Id });
            await DiscordAuthentication.ForgetAsync(HttpContext);
            await KardiaAuthentication.SignInAsync(HttpContext, user, true, options.Value.RememberDays);
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }
        catch (KardiaApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Someone new: pick a user name for a new account (Discord's answer stays in its cookie for a few minutes).
            var model = await PageModelAsync(returnUrl);
            if (model.Unavailable is null && model.CanCreateAccount && !model.IsFirstAccount)
            {
                return Redirect($"/create-account/discord?returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}");
            }

            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Discord sign-in with an unlinked Discord account from {Address}", Address);

            await DiscordAuthentication.ForgetAsync(HttpContext);
            return Redirect($"{KardiaAuthentication.SignInPath}?discord=not-linked");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or KardiaApiException)
        {
            await DiscordAuthentication.ForgetAsync(HttpContext);
            return Redirect($"{KardiaAuthentication.SignInPath}?discord=failed");
        }
    }

    /// <summary>A new account with Discord: the user name to pick (Discord's handle is suggested).</summary>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>The view or a redirect.</returns>
    [HttpGet("/create-account/discord")]
    public async Task<IActionResult> DiscordCreate(string? returnUrl = null)
    {
        var account = options.Value.DiscordEnabled ? await DiscordAuthentication.PeekAsync(HttpContext) : null;
        if (account is null || account.Mode == "link")
        {
            return Redirect($"{KardiaAuthentication.SignInPath}?discord=failed");
        }

        var model = await PageModelAsync(returnUrl);
        if (model.Unavailable is null && (!model.CanCreateAccount || model.IsFirstAccount))
        {
            await DiscordAuthentication.ForgetAsync(HttpContext);
            return Redirect($"{KardiaAuthentication.SignInPath}?discord=not-linked");
        }

        model.UserName = DiscordAuthentication.SuggestUserName(account.Handle, account.Name);
        model.DiscordName = account.Name;
        return View("DiscordCreate", model);
    }

    /// <summary>Creates the account with Discord (no password) and signs the browser in.</summary>
    /// <param name="userName">The user name.</param>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>A redirect, or the form again with the errors.</returns>
    [HttpPost("/create-account/discord")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DiscordCreateAsync([FromForm] string? userName, [FromForm] string? returnUrl)
    {
        var account = options.Value.DiscordEnabled ? await DiscordAuthentication.PeekAsync(HttpContext) : null;
        if (account is null || account.Mode == "link")
        {
            return Redirect($"{KardiaAuthentication.SignInPath}?discord=failed");
        }

        var model = await PageModelAsync(returnUrl);
        model.UserName = (userName ?? string.Empty).Trim();
        model.DiscordName = account.Name;
        if (model.Unavailable is not null)
        {
            model.Errors = [model.Unavailable];
            return View("DiscordCreate", model);
        }

        if (!model.CanCreateAccount || model.IsFirstAccount)
        {
            await DiscordAuthentication.ForgetAsync(HttpContext);
            return Redirect($"{KardiaAuthentication.SignInPath}?discord=not-linked");
        }

        try
        {
            var user = await apiClient.UserDiscordCreateAsync(new UserDiscordCreateRequest { UserName = model.UserName, DiscordId = account.Id, DiscordName = account.Name });
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Account {UserName} created with Discord", user.UserName);

            await DiscordAuthentication.ForgetAsync(HttpContext);
            await KardiaAuthentication.SignInAsync(HttpContext, user, true, options.Value.RememberDays);
            return LocalRedirect(model.ReturnUrl == "/" ? "/settings#profile" : model.ReturnUrl);
        }
        catch (KardiaApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            model.Errors = [.. exception.ToValidationErrors().Select(static error => error.Message)];
            return View("DiscordCreate", model);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or KardiaApiException)
        {
            model.Errors = [Unreachable];
            return View("DiscordCreate", model);
        }
    }

    /// <summary>The create-account form (always on the first visit; later only when registration is allowed).</summary>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>The view or a redirect.</returns>
    [HttpGet("/create-account")]
    public async Task<IActionResult> CreateAccount(string? returnUrl = null)
    {
        var model = await PageModelAsync(returnUrl);
        if (!model.CanCreateAccount && model.Unavailable is null)
        {
            return RedirectToAction(nameof(SignIn), new { returnUrl = model.ReturnUrl });
        }

        return View("CreateAccount", model);
    }

    /// <summary>Creates the account (with what biological age needs) and signs the browser in.</summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    /// <param name="confirmPassword">The password again.</param>
    /// <param name="birthDate">The date of birth (optional).</param>
    /// <param name="sex">The sex for the reference values (optional).</param>
    /// <param name="setupKey">KARDIA_SETUP_KEY, for the first account.</param>
    /// <param name="remember">Keep the session on this device.</param>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>A redirect, or the form again with the errors.</returns>
    [HttpPost("/create-account")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAccountAsync(
        [FromForm] string? userName, [FromForm] string? password, [FromForm] string? confirmPassword, [FromForm] DateOnly? birthDate,
        [FromForm] BiologicalSex sex, [FromForm] string? setupKey, [FromForm] bool remember, [FromForm] string? returnUrl)
    {
        var model = await PageModelAsync(returnUrl);
        model.UserName = (userName ?? string.Empty).Trim();
        model.BirthDate = birthDate;
        model.Sex = sex;
        model.Remember = remember;
        if (model.Unavailable is not null)
        {
            model.Errors = [model.Unavailable];
            return View("CreateAccount", model);
        }

        if (!model.CanCreateAccount)
        {
            return RedirectToAction(nameof(SignIn), new { returnUrl = model.ReturnUrl });
        }

        if (Throttled(model))
        {
            return View("CreateAccount", model);
        }

        if (model.NeedsSetupKey && !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(setupKey ?? string.Empty), Encoding.UTF8.GetBytes(options.Value.SetupKey)))
        {
            throttle.Failed(Address);
            await Task.Delay(FailureDelay);
            model.Errors = ["The setup key is wrong. It is KARDIA_SETUP_KEY, in the .env file next to docker-compose.yml."];
            return View("CreateAccount", model);
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            model.Errors = ["The two passwords are different."];
            return View("CreateAccount", model);
        }

        try
        {
            var user = await apiClient.UserCreateAsync(new UserCreateRequest { UserName = model.UserName, Password = password, BirthDate = birthDate, Sex = sex });
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Account {UserName} created ({Role})", user.UserName, user.Role);

            await KardiaAuthentication.SignInAsync(HttpContext, user, remember, options.Value.RememberDays);
            return LocalRedirect(model.ReturnUrl == "/" ? "/settings#apple-watch" : model.ReturnUrl);
        }
        catch (KardiaApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            model.Errors = [.. exception.ToValidationErrors().Select(static error => error.Message)];
            return View("CreateAccount", model);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or KardiaApiException)
        {
            model.Errors = [Unreachable];
            return View("CreateAccount", model);
        }
    }

    /// <summary>Signs the browser out and shows the public page.</summary>
    /// <returns>A redirect to the start.</returns>
    [HttpPost("/sign-out")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/");
    }

    private string Address => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private bool Throttled(AccountPageModel model)
    {
        var wait = throttle.RetryAfter(Address);
        if (wait <= TimeSpan.Zero)
        {
            return false;
        }

        model.Errors = [$"Too many wrong attempts. Try again in {Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes))} min."];
        Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return true;
    }

    private async Task<AccountPageModel> PageModelAsync(string? returnUrl)
    {
        var settings = options.Value;
        var model = new AccountPageModel { ReturnUrl = SafeReturnUrl(returnUrl), RememberDays = settings.RememberDays, DiscordEnabled = settings.DiscordEnabled };
        try
        {
            var status = await apiClient.UserStatusAsync();
            model.IsFirstAccount = status.UserCount == 0;
            model.CanCreateAccount = model.IsFirstAccount || settings.AllowRegistration;
            model.NeedsSetupKey = model.IsFirstAccount && settings.RequiresSetupKey;
            model.DemoUserName = status.DemoUserName;
            model.DemoPassword = status.DemoPassword;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or KardiaApiException)
        {
            model.Unavailable = Unreachable;
        }

        return model;
    }

    private string SafeReturnUrl(string? url) =>
        !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) && !url.StartsWith("/sign-", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("/create-account", StringComparison.OrdinalIgnoreCase) ? url : "/";
}
