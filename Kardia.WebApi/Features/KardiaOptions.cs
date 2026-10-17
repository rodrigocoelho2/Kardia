namespace Kardia.WebApi.Features;

/// <summary>
/// Module options, bound from the "Kardia" configuration section (in Docker: Kardia__ApiKey, Kardia__TimeZone…).
/// </summary>
public sealed class KardiaOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Kardia";

    /// <summary>Gets or sets the key the Website must send in the x-api-key header. Never commit a real value.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time zone days are counted in (IANA name). Every time Kardia keeps is a wall-clock time in this zone,
    /// so "today", "last night" and the heart-rate chart line up with the clock on your wall.
    /// </summary>
    public string TimeZone { get; set; } = "Europe/Lisbon";

    /// <summary>
    /// Gets or sets a value indicating whether Kardia runs on sample data kept in memory instead of SQL Server: a "demo"
    /// account with 120 days of generated Apple Watch data, and every new account gets the same. Nothing is saved.
    /// For trying Kardia on a PC and for the README screenshots.
    /// </summary>
    public bool DemoMode { get; set; }

    /// <summary>Gets or sets the password of the "demo" account in demo mode.</summary>
    public string DemoPassword { get; set; } = "kardia-demo";
}
