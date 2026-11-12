namespace Kardia.Website.Security;

/// <summary>
/// Sign-in settings, bound from the "Website" section (in Docker: Website__SetupKey, Website__AllowRegistration,
/// Website__KeysDirectory, Website__PublicUrl).
/// </summary>
public sealed class WebsiteOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Website";

    /// <summary>
    /// Gets or sets the setup key (KARDIA_SETUP_KEY): needed to create the FIRST account, so nobody else on the network
    /// can claim an empty install. Empty lets anyone who reaches the page create the first account.
    /// </summary>
    public string SetupKey { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether anyone may create an account after the first one.</summary>
    public bool AllowRegistration { get; set; } = true;

    /// <summary>
    /// Gets or sets where the cookie encryption keys are kept. Empty keeps them in memory, so every restart signs everyone out.
    /// </summary>
    public string KeysDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets how long "Keep me signed in" keeps a device signed in.</summary>
    public int RememberDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the address Health Auto Export reaches Kardia at (e.g. https://kardia.prometheus-nas.com), shown in
    /// Settings › Apple Watch. Empty uses the address the page was opened at.
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the time zone the days are counted in; keep it the same as the WebApi's Kardia:TimeZone.</summary>
    public string TimeZone { get; set; } = "Europe/Lisbon";

    /// <summary>Gets a value indicating whether the first account needs the setup key.</summary>
    public bool RequiresSetupKey => !string.IsNullOrEmpty(SetupKey);

    /// <summary>Gets or sets the Discord application's client id (DISCORD_CLIENT_ID; empty: no Discord sign-in).</summary>
    public string DiscordClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Discord application's client secret (DISCORD_CLIENT_SECRET).</summary>
    public string DiscordClientSecret { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether "Sign in with Discord" is on (both Discord settings are set).</summary>
    public bool DiscordEnabled => !string.IsNullOrWhiteSpace(DiscordClientId) && !string.IsNullOrWhiteSpace(DiscordClientSecret);
}
