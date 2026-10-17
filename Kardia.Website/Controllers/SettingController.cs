using Kardia.Library.Enums;
using Kardia.Sdk.Models;
using Kardia.Website.Model;
using Kardia.Website.Model.Settings;
using Kardia.Website.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kardia.Website.Controllers;

/// <summary>
/// Settings: profile, Apple Watch connection, password and (admins) accounts.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="options">Website settings (the public address for the sync).</param>
public sealed class SettingController(Kardia.Sdk.KardiaClient apiClient, IOptions<WebsiteOptions> options) : WebsiteBaseController(apiClient)
{
    /// <summary>The settings.</summary>
    /// <param name="saved">Which form was just saved, for the notice.</param>
    /// <returns>The view.</returns>
    [HttpGet("/settings")]
    public async Task<IActionResult> Index(string? saved = null)
    {
        var page = await PageAsync();
        page.Notice = saved switch
        {
            "profile" => "Profile saved. Your scores use it from now on.",
            "password" => "Password changed. Other devices have been signed out.",
            _ => null,
        };
        return View(page);
    }

    /// <summary>Saves the profile.</summary>
    /// <param name="birthDate">Date of birth.</param>
    /// <param name="sex">Sex for the reference values.</param>
    /// <param name="heightCm">Height.</param>
    /// <param name="weightKg">Weight.</param>
    /// <param name="maxHeartRate">Measured maximum heart rate.</param>
    /// <param name="sleepNeedHours">Base sleep need, in hours (e.g. 7.5).</param>
    /// <returns>A redirect, or the page with the errors.</returns>
    [HttpPost("/settings/profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfileAsync(
        [FromForm] DateOnly? birthDate, [FromForm] BiologicalSex sex, [FromForm] decimal? heightCm, [FromForm] decimal? weightKg,
        [FromForm] int? maxHeartRate, [FromForm] decimal? sleepNeedHours)
    {
        var result = await new SettingModel(ApiClient).SaveProfileAsync(new ProfileSaveRequest
        {
            UserId = UserId,
            BirthDate = birthDate,
            Sex = sex,
            HeightCm = heightCm,
            WeightKg = weightKg,
            MaxHeartRate = maxHeartRate,
            SleepNeedMinutes = sleepNeedHours is { } hours ? (int)Math.Round(hours * 60) : null,
        });
        if (result.IsValid)
        {
            return Redirect("/settings?saved=profile#profile");
        }

        var page = await PageAsync();
        page.Errors = [.. result.ValidationErrors.Select(static error => error.Message)];
        page.ErrorsFor = "profile";
        return View("Index", page);
    }

    /// <summary>Changes the password (and keeps this device signed in).</summary>
    /// <param name="currentPassword">The current password.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="confirmPassword">The new password again.</param>
    /// <returns>A redirect, or the page with the errors.</returns>
    [HttpPost("/settings/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasswordAsync([FromForm] string? currentPassword, [FromForm] string? newPassword, [FromForm] string? confirmPassword)
    {
        List<string> errors;
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            errors = ["The two new passwords are different."];
        }
        else
        {
            var result = await new SettingModel(ApiClient).ChangePasswordAsync(new UserPasswordRequest { Id = UserId, CurrentPassword = currentPassword, NewPassword = newPassword });
            if (result.IsValid && result.Data is not null)
            {
                await KardiaAuthentication.SignInAsync(HttpContext, result.Data, remember: true, options.Value.RememberDays);
                return Redirect("/settings?saved=password#account");
            }

            errors = [.. result.ValidationErrors.Select(static error => error.Message)];
        }

        var page = await PageAsync();
        page.Errors = errors;
        page.ErrorsFor = "password";
        return View("Index", page);
    }

    /// <summary>Makes a new Apple Watch key (shown once).</summary>
    /// <returns>JSON <c>AjaxDataResult</c> with the key.</returns>
    [HttpPost("/settings/sync-key")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateKeyAsync() => Json(await new SettingModel(ApiClient).CreateKeyAsync(UserId));

    /// <summary>Revokes the Apple Watch key.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost("/settings/sync-key/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeKeyAsync() => Json(await new SettingModel(ApiClient).RevokeKeyAsync(UserId));

    /// <summary>Deletes an account (admins; never their own from here).</summary>
    /// <param name="identifier">The account.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost("/settings/users/delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUserAsync([FromForm] string identifier)
    {
        if (string.Equals(identifier, UserId, StringComparison.OrdinalIgnoreCase))
        {
            return Json(new AjaxResult { IsValid = false, ValidationErrors = [new Kardia.Library.Validation.ApiValidationError(string.Empty, "You can't delete the account you're signed in with.")] });
        }

        return Json(await new SettingModel(ApiClient).DeleteUserAsync(identifier));
    }

    private async Task<SettingPageModel> PageAsync()
    {
        var page = await new SettingModel(ApiClient).PageAsync(Shell(new SettingPageModel(), AppSection.Settings), UserId);
        var origin = string.IsNullOrWhiteSpace(options.Value.PublicUrl) ? $"{Request.Scheme}://{Request.Host}" : options.Value.PublicUrl.TrimEnd('/');
        page.SyncUrl = origin + "/api/sync";
        return page;
    }
}
