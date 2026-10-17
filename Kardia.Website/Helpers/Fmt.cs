using Kardia.Sdk.Models;
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

    /// <summary>A number with a fixed number of decimals ("—" when empty).</summary>
    /// <param name="value">The value.</param>
    /// <param name="decimals">0 or 1.</param>
    /// <returns>The text.</returns>
    public static string Number(double? value, int decimals) => value is { } number ? number.ToString(decimals == 0 ? "0" : "0.0", English) : "—";

    /// <summary>A Health Monitor value with its unit (temperature as a signed change).</summary>
    /// <param name="key">The sign key.</param>
    /// <param name="value">The value.</param>
    /// <param name="unit">The unit.</param>
    /// <param name="decimals">The decimals.</param>
    /// <returns>The text.</returns>
    public static string SignValue(string key, double? value, string unit, int decimals) => value is not { } number ? "—"
        : key == "temperature" ? $"{(number >= 0 ? "+" : "−")}{Math.Abs(number).ToString("0.0", English)} {unit}"
        : $"{Number(number, decimals)} {unit}";

    /// <summary>A typical range ("48–62 ms", "−0.3 to +0.4 °C").</summary>
    /// <param name="key">The sign key.</param>
    /// <param name="low">Bottom.</param>
    /// <param name="high">Top.</param>
    /// <param name="unit">The unit.</param>
    /// <param name="decimals">The decimals.</param>
    /// <returns>The text.</returns>
    public static string SignRange(string key, double low, double high, string unit, int decimals) => key == "temperature"
        ? $"{SignValue(key, low, string.Empty, 1).Trim()} to {SignValue(key, high, unit, 1)}"
        : $"{Number(low, decimals)}–{Number(high, decimals)} {unit}";

    /// <summary>The CSS status class of a Health Monitor sign: in, out, far or none.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The class.</returns>
    public static string SignClass(HealthSignStatus status) => status switch
    {
        HealthSignStatus.InRange => "in",
        HealthSignStatus.Outside => "out",
        HealthSignStatus.FarOutside => "far",
        _ => "none",
    };

    /// <summary>Where a Health Monitor sign sits, in words.</summary>
    /// <param name="sign">The sign.</param>
    /// <returns>"In range", "Higher than usual"…</returns>
    public static string SignWords(HealthSignDTO sign) => sign.Status switch
    {
        HealthSignStatus.InRange => "In range",
        HealthSignStatus.None => sign.Value is null ? "Not measured" : "Learning your range",
        _ => (sign.Value ?? 0) > sign.Typical
            ? (sign.Status == HealthSignStatus.FarOutside ? "Much higher than usual" : "Higher than usual")
            : (sign.Status == HealthSignStatus.FarOutside ? "Much lower than usual" : "Lower than usual"),
    };

    /// <summary>The icon of a Health Monitor sign.</summary>
    /// <param name="key">The sign key.</param>
    /// <returns>The icon key.</returns>
    public static string SignIcon(string key) => key switch
    {
        "hrv" => "recovery",
        "resting" => "heart",
        "respiratory" => "lungs",
        "oxygen" => "drop",
        _ => "temp",
    };

    /// <summary>The words of a cardio load band.</summary>
    /// <param name="status">The band.</param>
    /// <returns>The name.</returns>
    public static string CardioName(CardioLoadStatus status) => status switch
    {
        CardioLoadStatus.Detraining => "Detraining",
        CardioLoadStatus.Maintaining => "Maintaining",
        CardioLoadStatus.Productive => "Productive",
        CardioLoadStatus.Overreaching => "Overreaching",
        CardioLoadStatus.Overtraining => "Overtraining",
        _ => "Learning",
    };

    /// <summary>What a cardio load band means.</summary>
    /// <param name="status">The band.</param>
    /// <returns>A sentence.</returns>
    public static string CardioText(CardioLoadStatus status) => status switch
    {
        CardioLoadStatus.Detraining => "This week's heart load is well below what your body is used to. Fine for a rest week; for longer, fitness starts to slip.",
        CardioLoadStatus.Maintaining => "You're doing a bit less than your last four weeks: enough to hold your fitness, not to build it.",
        CardioLoadStatus.Productive => "The sweet spot: a little more than your body is used to, so fitness builds without piling up fatigue.",
        CardioLoadStatus.Overreaching => "Load climbed fast. Fine for a short block, but plan easier days and watch your recovery.",
        CardioLoadStatus.Overtraining => "This week's load jumped far above your usual. That's when injuries and illness creep in: back off for a few days.",
        _ => "Cardio load needs three weeks of heart rate to compare this week with what your body is used to.",
    };

    /// <summary>The CSS colour of a cardio load band.</summary>
    /// <param name="status">The band.</param>
    /// <returns>A CSS colour.</returns>
    public static string CardioColor(CardioLoadStatus status) => status switch
    {
        CardioLoadStatus.Detraining => "var(--sky)",
        CardioLoadStatus.Maintaining => "var(--slp)",
        CardioLoadStatus.Productive => "var(--rec)",
        CardioLoadStatus.Overreaching => "var(--mid)",
        CardioLoadStatus.Overtraining => "var(--low)",
        _ => "var(--muted)",
    };

    /// <summary>The name WHOOP gives a sleep goal.</summary>
    /// <param name="percent">100, 85 or 70.</param>
    /// <returns>Peak, Perform or Get by.</returns>
    public static string GoalName(int percent) => percent >= 100 ? "Peak" : percent >= 85 ? "Perform" : "Get by";

    /// <summary>A day range ("12–18 Oct", "28 Sep – 4 Oct").</summary>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day.</param>
    /// <returns>The text.</returns>
    public static string Range(DateOnly from, DateOnly to) => from.Month == to.Month
        ? $"{from.Day}–{to.ToString("d MMM", English)}"
        : $"{from.ToString("d MMM", English)} – {to.ToString("d MMM", English)}";

    /// <summary>The word for a one-minute heart-rate recovery.</summary>
    /// <param name="bpm">The fall in bpm.</param>
    /// <returns>Excellent, good, fair or low.</returns>
    public static string RecoveryWord(int? bpm) => bpm switch
    {
        null => string.Empty,
        >= 30 => "Excellent",
        >= 20 => "Good",
        >= 12 => "Fair",
        _ => "Low",
    };

    /// <summary>The word for a stress level.</summary>
    /// <param name="level">0–3.</param>
    /// <returns>Low, medium or high.</returns>
    public static string StressWord(double? level) => level switch
    {
        null => "—",
        >= 2 => "High",
        >= 1 => "Medium",
        _ => "Low",
    };

    /// <summary>The CSS class of a stress level.</summary>
    /// <param name="level">0–3.</param>
    /// <returns>st-low, st-mid or st-high.</returns>
    public static string StressClass(double? level) => level switch
    {
        null => "st-none",
        >= 2 => "st-high",
        >= 1 => "st-mid",
        _ => "st-low",
    };
}
