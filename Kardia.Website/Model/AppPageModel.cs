namespace Kardia.Website.Model;

/// <summary>The screens of the app (picks the highlighted tab).</summary>
public enum AppSection
{
    /// <summary>Today.</summary>
    Today,

    /// <summary>Recovery.</summary>
    Recovery,

    /// <summary>Sleep.</summary>
    Sleep,

    /// <summary>Strain.</summary>
    Strain,

    /// <summary>Biological age.</summary>
    Age,

    /// <summary>Journal.</summary>
    Journal,

    /// <summary>Settings.</summary>
    Settings,

    /// <summary>Health Monitor.</summary>
    Health,

    /// <summary>Trends.</summary>
    Trends,

    /// <summary>Weekly and monthly assessments.</summary>
    Report,

    /// <summary>Weekly plan.</summary>
    Plan,

    /// <summary>The AI coach.</summary>
    AiCoach,

    /// <summary>Everything else (the phone's menu).</summary>
    More,
}

/// <summary>
/// What every signed-in screen needs for the shell: who's signed in, which tab is on, the day shown.
/// </summary>
public class AppPageModel
{
    /// <summary>The product name.</summary>
    public const string ProductName = "Kardia";

    /// <summary>The version shown in the app.</summary>
    public const string CurrentVersion = "v1.1";

    /// <summary>Gets or sets the signed-in user name.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the signed-in account is an admin.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Gets or sets the screen.</summary>
    public AppSection Section { get; set; }

    /// <summary>Gets or sets the day shown.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets today's date (in the app's time zone).</summary>
    public DateOnly Today { get; set; }

    /// <summary>Gets or sets a message when the WebApi can't be reached (the screen shows it instead of data).</summary>
    public string? Unavailable { get; set; }

    /// <summary>Gets or sets when the Apple Watch last synced (shown in the sidebar; null when it never did).</summary>
    public DateTimeOffset? LastSyncAt { get; set; }

    /// <summary>Gets a value indicating whether the day shown is today.</summary>
    public bool IsToday => Date == Today;

    /// <summary>Gets the initials shown in the avatar.</summary>
    public string Initials => UserName.Length == 0 ? "K" : UserName.Length == 1 ? UserName.ToUpperInvariant() : UserName[..2].ToUpperInvariant();

    /// <summary>Gets the greeting for the time of day.</summary>
    public string Greeting => Kardia.Website.Helpers.Clock.Now.Hour switch
    {
        < 5 => "Good night",
        < 12 => "Good morning",
        < 18 => "Good afternoon",
        _ => "Good evening",
    };
}
