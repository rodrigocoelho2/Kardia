using System.Globalization;
using Kardia.Library.Enums;

namespace Kardia.Website.Helpers;

/// <summary>
/// How numbers, times and durations read on the screens.
/// </summary>
public static class Fmt
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");

    /// <summary>"7h 40m", or "45m" under an hour.</summary>
    /// <param name="minutes">The minutes.</param>
    /// <returns>The text.</returns>
    public static string Duration(int? minutes) => minutes is not { } value ? "—" : value >= 60 ? $"{value / 60}h {value % 60:00}m" : $"{value}m";

    /// <summary>"23:48".</summary>
    /// <param name="time">The time.</param>
    /// <returns>The text.</returns>
    public static string Clock(DateTime? time) => time is { } value ? value.ToString("HH:mm", English) : "—";

    /// <summary>A whole number with a thin space between thousands ("9 842").</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Whole(double? value) => value is { } number ? Math.Round(number).ToString("#,0", English).Replace(",", " ", StringComparison.Ordinal) : "—";

    /// <summary>One decimal ("12.4").</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string One(double? value) => value is { } number ? number.ToString("0.0", English) : "—";

    /// <summary>A change with its sign ("+11%", "−3").</summary>
    /// <param name="value">The change.</param>
    /// <param name="suffix">A unit after the number.</param>
    /// <param name="decimals">Decimals.</param>
    /// <returns>The text.</returns>
    public static string Signed(double value, string suffix = "", int decimals = 0)
    {
        var rounded = Math.Round(value, decimals);
        var text = Math.Abs(rounded).ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), English);
        return (rounded > 0 ? "+" : rounded < 0 ? "−" : "±") + text + suffix;
    }

    /// <summary>"Friday, 16 October".</summary>
    /// <param name="date">The day.</param>
    /// <returns>The text.</returns>
    public static string LongDate(DateOnly date) => date.ToString("dddd, d MMMM", English);

    /// <summary>"Fri 16 Oct".</summary>
    /// <param name="date">The day.</param>
    /// <returns>The text.</returns>
    public static string ShortDate(DateOnly date) => date.ToString("ddd d MMM", English);

    /// <summary>"Fri".</summary>
    /// <param name="date">The day.</param>
    /// <returns>The text.</returns>
    public static string Weekday(DateOnly date) => date.ToString("ddd", English);

    /// <summary>yyyy-MM-dd, for links.</summary>
    /// <param name="date">The day.</param>
    /// <returns>The text.</returns>
    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>"4 min ago", "2 h ago", "3 days ago".</summary>
    /// <param name="at">The instant.</param>
    /// <returns>The text.</returns>
    public static string Ago(DateTimeOffset? at)
    {
        if (at is not { } value)
        {
            return "never";
        }

        var span = DateTimeOffset.UtcNow - value;
        return span.TotalMinutes < 1 ? "just now"
            : span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes} min ago"
            : span.TotalHours < 24 ? $"{(int)span.TotalHours} h ago"
            : $"{(int)span.TotalDays} days ago";
    }

    /// <summary>The headline of a recovery band.</summary>
    /// <param name="zone">The band.</param>
    /// <returns>The words.</returns>
    public static string ZoneHeadline(RecoveryZone zone) => zone switch
    {
        RecoveryZone.High => "Ready to push",
        RecoveryZone.Medium => "Steady day",
        RecoveryZone.Low => "Take it easy",
        _ => "Waiting for data",
    };

    /// <summary>The CSS palette of a recovery band ("rec", "mid", "low").</summary>
    /// <param name="zone">The band.</param>
    /// <returns>The palette key.</returns>
    public static string ZonePalette(RecoveryZone zone) => zone switch
    {
        RecoveryZone.Medium => "mid",
        RecoveryZone.Low => "low",
        _ => "rec",
    };

    /// <summary>The name shown for a sleep stage.</summary>
    /// <param name="stage">The stage.</param>
    /// <returns>The name.</returns>
    public static string StageName(SleepStage stage) => stage switch
    {
        SleepStage.Awake => "Awake",
        SleepStage.Rem => "REM",
        SleepStage.Core => "Light",
        SleepStage.Deep => "Deep",
        _ => stage.ToString(),
    };

    /// <summary>A shorter name for a Watch workout type.</summary>
    /// <param name="kind">The type as the Watch sends it.</param>
    /// <returns>The name.</returns>
    public static string WorkoutName(string kind) => kind
        .Replace("Traditional Strength Training", "Strength", StringComparison.OrdinalIgnoreCase)
        .Replace("Functional Strength Training", "Strength", StringComparison.OrdinalIgnoreCase)
        .Replace("Outdoor ", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("High Intensity Interval Training", "HIIT", StringComparison.OrdinalIgnoreCase);

    /// <summary>The icon key of a workout (run, ride, walk, lift, swim, other).</summary>
    /// <param name="kind">The type as the Watch sends it.</param>
    /// <returns>The key.</returns>
    public static string WorkoutIcon(string kind)
    {
        var text = kind.ToLowerInvariant();
        return text.Contains("run", StringComparison.Ordinal) ? "run"
            : text.Contains("cycl", StringComparison.Ordinal) || text.Contains("bike", StringComparison.Ordinal) ? "ride"
            : text.Contains("walk", StringComparison.Ordinal) || text.Contains("hik", StringComparison.Ordinal) ? "walk"
            : text.Contains("strength", StringComparison.Ordinal) || text.Contains("weight", StringComparison.Ordinal) ? "lift"
            : text.Contains("swim", StringComparison.Ordinal) ? "swim"
            : "other";
    }
}
