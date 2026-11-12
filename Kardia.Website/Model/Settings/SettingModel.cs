using Kardia.Sdk;
using Kardia.Sdk.Models;

namespace Kardia.Website.Model.Settings;

/// <summary>
/// The settings screen: profile, Apple Watch connection, account and (for admins) the accounts.
/// </summary>
public sealed class SettingPageModel : AppPageModel
{
    /// <summary>Gets or sets the profile.</summary>
    public ProfileDTO Profile { get; set; } = new();

    /// <summary>Gets or sets the Apple Watch connection.</summary>
    public SyncKeyDTO Sync { get; set; } = new();

    /// <summary>Gets or sets every account (admins only).</summary>
    public List<UserDTO> Users { get; set; } = [];

    /// <summary>Gets or sets the address Health Auto Export sends to.</summary>
    public string SyncUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets a message after a save ("Profile saved.").</summary>
    public string? Notice { get; set; }

    /// <summary>Gets or sets the errors of the last form sent.</summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>Gets or sets which form the errors belong to ("profile" or "password").</summary>
    public string? ErrorsFor { get; set; }

    /// <summary>Gets or sets the goals and notification choices.</summary>
    public SettingDTO Setting { get; set; } = new();

    /// <summary>Gets or sets the appearance: system, dark or light.</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>Gets or sets the signed-in account's id.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether Discord sign-in is set up on this Kardia.</summary>
    public bool DiscordEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether this account has a Discord account linked.</summary>
    public bool DiscordLinked { get; set; }

    /// <summary>Gets or sets the linked Discord account's name.</summary>
    public string? DiscordName { get; set; }

    /// <summary>Gets or sets a value indicating whether the account has a password (one made with Discord may not).</summary>
    public bool HasPassword { get; set; } = true;

    /// <summary>Gets or sets what just happened with Discord (linked, unlinked, taken, failed, needs-password), for a note.</summary>
    public string? DiscordNote { get; set; }
}

/// <summary>
/// Loads and saves the settings.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class SettingModel(KardiaClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>
    /// Builds the screen.
    /// </summary>
    /// <param name="page">The page with the shell filled in.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<SettingPageModel> PageAsync(SettingPageModel page, string userId)
    {
        var profile = await CallAsync(() => ApiClient.ProfileGetAsync(userId));
        if (!profile.IsValid || profile.Data is null)
        {
            page.Unavailable = profile.ValidationErrors.FirstOrDefault()?.Message ?? ApiUnavailableMessage;
            return page;
        }

        page.UserId = userId;
        page.Profile = profile.Data;
        var syncCall = CallAsync(() => ApiClient.SyncKeyGetAsync(userId));
        var settingCall = CallAsync(() => ApiClient.SettingGetAsync(userId));
        var userCall = CallAsync(() => ApiClient.UserGetAsync(userId));
        page.Sync = (await syncCall).Data ?? new SyncKeyDTO();
        if ((await userCall).Data is { } me)
        {
            page.HasPassword = me.HasPassword;
            page.DiscordLinked = me.DiscordLinked;
            page.DiscordName = me.DiscordName;
        }

        page.LastSyncAt = page.Sync.LastUsedAt;
        page.Setting = (await settingCall).Data ?? new SettingDTO { UserId = userId };
        if (page.IsAdmin)
        {
            page.Users = (await CallAsync(() => ApiClient.UserListAsync())).Data ?? [];
        }

        return page;
    }

    /// <summary>Saves the profile.</summary>
    /// <param name="item">The profile.</param>
    /// <returns>The saved profile, or the errors.</returns>
    public Task<AjaxDataResult<ProfileDTO>> SaveProfileAsync(ProfileSaveRequest item) => CallAsync(() => ApiClient.ProfileSaveAsync(item));

    /// <summary>Gets the goals and notification choices.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The settings.</returns>
    public Task<AjaxDataResult<SettingDTO>> GetSettingAsync(string userId) => CallAsync(() => ApiClient.SettingGetAsync(userId));

    /// <summary>Saves the goals and notification choices.</summary>
    /// <param name="item">The settings.</param>
    /// <returns>The saved settings, or the errors.</returns>
    public Task<AjaxDataResult<SettingDTO>> SaveSettingAsync(SettingDTO item) => CallAsync(() => ApiClient.SettingSaveAsync(item));

    /// <summary>Gets the notification state (devices, key).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    public Task<AjaxDataResult<PushDTO>> PushAsync(string userId) => CallAsync(() => ApiClient.PushGetAsync(userId));

    /// <summary>Turns notifications on for this browser.</summary>
    /// <param name="item">The subscription.</param>
    /// <returns>The state.</returns>
    public Task<AjaxDataResult<PushDTO>> PushSubscribeAsync(PushSubscribeRequest item) => CallAsync(() => ApiClient.PushSubscribeAsync(item));

    /// <summary>Turns notifications off for a device.</summary>
    /// <param name="item">The device.</param>
    /// <returns>The state.</returns>
    public Task<AjaxDataResult<PushDTO>> PushUnsubscribeAsync(PushUnsubscribeRequest item) => CallAsync(() => ApiClient.PushUnsubscribeAsync(item));

    /// <summary>Sends a test notification.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>How many devices it reached.</returns>
    public Task<AjaxDataResult<PushTestResultDTO>> PushTestAsync(string userId) => CallAsync(() => ApiClient.PushTestAsync(userId));

    /// <summary>Changes the password.</summary>
    /// <param name="item">The account, current and new password.</param>
    /// <returns>The account (with its new stamp), or the errors.</returns>
    public Task<AjaxDataResult<UserDTO>> ChangePasswordAsync(UserPasswordRequest item) => CallAsync(() => ApiClient.UserPasswordAsync(item));

    /// <summary>Makes a new sync key.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The key (shown once).</returns>
    public Task<AjaxDataResult<SyncKeyCreatedDTO>> CreateKeyAsync(string userId) => CallAsync(() => ApiClient.SyncKeyCreateAsync(userId));

    /// <summary>Revokes the sync key.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> RevokeKeyAsync(string userId) => CallAsync(() => ApiClient.SyncKeyRevokeAsync(userId));

    /// <summary>Deletes an account (admins).</summary>
    /// <param name="identifier">The account.</param>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> DeleteUserAsync(string identifier) => CallAsync(() => ApiClient.UserDeleteAsync(identifier));
}
