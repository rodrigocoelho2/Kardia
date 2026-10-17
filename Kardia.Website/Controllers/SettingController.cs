using Kardia.Library.Enums;
using Kardia.Sdk.Models;
using Kardia.Website.Helpers;
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
            "goals" => "Goals saved. The weekly plan and tonight's bedtime use them now.",
            "notifications" => "Notification choices saved.",
            "theme" => "Appearance saved for this browser.",
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

    /// <summary>Saves the goals: sleep goal and the weekly plan targets.</summary>
    /// <param name="sleepGoalPercent">100, 85 or 70.</param>
    /// <param name="activeMinutesWeek">Zone 2+ minutes a week.</param>
    /// <param name="stepsDay">Steps a day.</param>
    /// <param name="sleepNightsWeek">Nights a week on the sleep goal.</param>
    /// <param name="bedtimeNightsWeek">Nights a week in bed on time.</param>
    /// <returns>A redirect, or the page with the errors.</returns>
    [HttpPost("/settings/goals")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoalsAsync([FromForm] int sleepGoalPercent, [FromForm] int activeMinutesWeek, [FromForm] int stepsDay, [FromForm] int sleepNightsWeek, [FromForm] int bedtimeNightsWeek)
    {
        var model = new SettingModel(ApiClient);
        var current = await model.GetSettingAsync(UserId);
        var item = current.Data ?? new SettingDTO { UserId = UserId };
        item.SleepGoalPercent = sleepGoalPercent;
        item.ActiveMinutesWeek = activeMinutesWeek;
        item.StepsDay = stepsDay;
        item.SleepNightsWeek = sleepNightsWeek;
        item.BedtimeNightsWeek = bedtimeNightsWeek;
        return await SaveSettingAsync(model, item, "goals");
    }

    /// <summary>Saves which notifications to send.</summary>
    /// <param name="notifyMorning">Morning recovery.</param>
    /// <param name="notifyBedtime">Bedtime reminder.</param>
    /// <param name="bedtimeLeadMinutes">Minutes before bedtime.</param>
    /// <param name="notifyHealth">Health Monitor alert.</param>
    /// <param name="notifyStrain">Strain target.</param>
    /// <param name="notifyReports">Weekly and monthly assessments.</param>
    /// <returns>A redirect, or the page with the errors.</returns>
    [HttpPost("/settings/notifications")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotificationsAsync([FromForm] bool notifyMorning, [FromForm] bool notifyBedtime, [FromForm] int bedtimeLeadMinutes,
        [FromForm] bool notifyHealth, [FromForm] bool notifyStrain, [FromForm] bool notifyReports)
    {
        var model = new SettingModel(ApiClient);
        var current = await model.GetSettingAsync(UserId);
        var item = current.Data ?? new SettingDTO { UserId = UserId };
        item.NotifyMorning = notifyMorning;
        item.NotifyBedtime = notifyBedtime;
        item.BedtimeLeadMinutes = bedtimeLeadMinutes;
        item.NotifyHealth = notifyHealth;
        item.NotifyStrain = notifyStrain;
        item.NotifyReports = notifyReports;
        return await SaveSettingAsync(model, item, "notifications");
    }

    /// <summary>Sets tonight's sleep goal from the Sleep screen.</summary>
    /// <param name="sleepGoalPercent">100, 85 or 70.</param>
    /// <returns>Back to the Sleep screen.</returns>
    [HttpPost("/settings/sleep-goal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SleepGoalAsync([FromForm] int sleepGoalPercent)
    {
        var model = new SettingModel(ApiClient);
        var current = await model.GetSettingAsync(UserId);
        if (current.Data is { } item)
        {
            item.SleepGoalPercent = sleepGoalPercent;
            await model.SaveSettingAsync(item);
        }

        return Redirect("/sleep#tonight");
    }

    /// <summary>Saves the appearance for this browser.</summary>
    /// <param name="theme">system, dark or light.</param>
    /// <returns>Back to the settings.</returns>
    [HttpPost("/settings/theme")]
    [ValidateAntiForgeryToken]
    public IActionResult ThemeAsync([FromForm] string? theme)
    {
        Theme.Save(HttpContext, theme);
        return Redirect("/settings?saved=theme#appearance");
    }

    /// <summary>The notification state of the account (JSON for push.js).</summary>
    /// <returns>JSON <c>AjaxDataResult</c> with the devices and the key.</returns>
    [HttpGet("/settings/push")]
    public async Task<IActionResult> PushAsync() => Json(await new SettingModel(ApiClient).PushAsync(UserId));

    /// <summary>Turns notifications on for this browser.</summary>
    /// <param name="item">The browser's subscription.</param>
    /// <returns>JSON <c>AjaxDataResult</c> with the state.</returns>
    [HttpPost("/settings/push/subscribe")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PushSubscribeAsync([FromBody] PushSubscribeRequest item)
    {
        item.UserId = UserId;
        return Json(await new SettingModel(ApiClient).PushSubscribeAsync(item));
    }

    /// <summary>Turns notifications off for a device.</summary>
    /// <param name="item">The device.</param>
    /// <returns>JSON <c>AjaxDataResult</c> with the state.</returns>
    [HttpPost("/settings/push/unsubscribe")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PushUnsubscribeAsync([FromBody] PushUnsubscribeRequest item)
    {
        item.UserId = UserId;
        return Json(await new SettingModel(ApiClient).PushUnsubscribeAsync(item));
    }

    /// <summary>Sends a test notification to the account's devices.</summary>
    /// <returns>JSON <c>AjaxDataResult</c> with how many it reached.</returns>
    [HttpPost("/settings/push/test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PushTestAsync() => Json(await new SettingModel(ApiClient).PushTestAsync(UserId));

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

    private async Task<IActionResult> SaveSettingAsync(SettingModel model, SettingDTO item, string form)
    {
        item.UserId = UserId;
        var result = await model.SaveSettingAsync(item);
        if (result.IsValid)
        {
            return Redirect($"/settings?saved={form}#{form}");
        }

        var page = await PageAsync();
        page.Errors = [.. result.ValidationErrors.Select(static error => error.Message)];
        page.ErrorsFor = form;
        return View("Index", page);
    }

    private async Task<SettingPageModel> PageAsync()
    {
        var page = await new SettingModel(ApiClient).PageAsync(Shell(new SettingPageModel(), AppSection.Settings), UserId);
        page.Theme = Theme.Of(HttpContext);
        var origin = string.IsNullOrWhiteSpace(options.Value.PublicUrl) ? $"{Request.Scheme}://{Request.Host}" : options.Value.PublicUrl.TrimEnd('/');
        page.SyncUrl = origin + "/api/sync";
        return page;
    }
}
