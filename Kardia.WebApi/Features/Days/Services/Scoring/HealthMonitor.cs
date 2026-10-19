using Kardia.Library.Enums;

namespace Kardia.WebApi.Features.Days.Services.Scoring;

/// <summary>One Health Monitor sign for a day.</summary>
/// <param name="Key">hrv, resting, respiratory, oxygen or temperature.</param>
/// <param name="Name">The name shown.</param>
/// <param name="Value">The day's value (for temperature: the change from your normal, °C).</param>
/// <param name="Unit">The unit.</param>
/// <param name="Typical">Your normal.</param>
/// <param name="Low">Bottom of your typical range.</param>
/// <param name="High">Top of your typical range.</param>
/// <param name="Status">Where the value sits.</param>
/// <param name="Decimals">Decimals to show.</param>
/// <param name="Estimated">Whether the value was worked out by Kardia (resting heart rate from last night's heart rate).</param>
internal sealed record HealthSign(string Key, string Name, double? Value, string Unit, double Typical, double Low, double High, HealthSignStatus Status, int Decimals, bool Estimated = false);

/// <summary>
/// The Health Monitor: last night's HRV, resting heart rate, respiratory rate, blood oxygen and wrist temperature, each
/// against your own typical range (your 30-day mean ± 1.5 spreads). A sign needs 7 days of your own values before it
/// is judged. Pure, unit-tested.
/// </summary>
internal static class HealthMonitor
{
    /// <summary>Days of your own values a sign needs before it's judged.</summary>
    public const int MinimumDays = 7;

    /// <summary>Spreads from your mean that still count as typical.</summary>
    public const double TypicalSpread = 1.5;

    /// <summary>Spreads beyond which a sign is far outside.</summary>
    public const double FarSpread = 2.5;

    /// <summary>
    /// Judges the five signs of a day.
    /// </summary>
    /// <param name="day">The day's score (values and baselines).</param>
    /// <returns>The signs, in display order.</returns>
    public static List<HealthSign> Of(DayScore day)
    {
        var temperature = day.Value(HealthMetric.WristTemperature);
        return
        [
            Sign("hrv", "Heart rate variability", day.Value(HealthMetric.Hrv), "ms", day.HrvBaseline, 0, lowOnly: false),
            Sign("resting", "Resting heart rate", day.Value(HealthMetric.RestingHeartRate), "bpm", day.RestingBaseline, 0, lowOnly: false) with { Estimated = day.RestingEstimated },
            Sign("respiratory", "Respiratory rate", day.Value(HealthMetric.RespiratoryRate), "rpm", day.RespiratoryBaseline, 1, lowOnly: false),
            Sign("oxygen", "Blood oxygen", day.Value(HealthMetric.BloodOxygen), "%", day.OxygenBaseline, 0, lowOnly: true, cap: 100),

            // Temperature is shown as the change from your normal, like the Watch does.
            Sign("temperature", "Wrist temperature", temperature is { } t ? t - day.TemperatureBaseline.Mean : null, "°C",
                day.TemperatureBaseline with { Mean = 0 }, 1, lowOnly: false),
        ];
    }

    /// <summary>How many judged signs sit outside their range.</summary>
    /// <param name="signs">The signs.</param>
    /// <returns>The count.</returns>
    public static int Outside(IEnumerable<HealthSign> signs) => signs.Count(static sign => sign.Status is HealthSignStatus.Outside or HealthSignStatus.FarOutside);

    private static HealthSign Sign(string key, string name, double? value, string unit, Baseline baseline, int decimals, bool lowOnly, double? cap = null)
    {
        var low = Math.Round(baseline.Mean - TypicalSpread * baseline.Sd, decimals);
        var high = Math.Round(Math.Min(cap ?? double.MaxValue, baseline.Mean + TypicalSpread * baseline.Sd), decimals);
        var shown = value is { } raw ? Math.Round(raw, decimals) : (double?)null;
        var status = HealthSignStatus.None;
        if (value is { } v && baseline.Count >= MinimumDays)
        {
            var z = baseline.Sd <= 0 ? 0 : (v - baseline.Mean) / baseline.Sd;
            if (lowOnly)
            {
                z = Math.Min(0, z);
            }

            // Judged on the numbers shown, so "68 ms" is never "outside 53–68 ms".
            var inside = shown >= low && (lowOnly || shown <= high);
            status = inside || Math.Abs(z) <= TypicalSpread ? HealthSignStatus.InRange
                : Math.Abs(z) <= FarSpread ? HealthSignStatus.Outside
                : HealthSignStatus.FarOutside;
        }

        return new HealthSign(key, name, shown, unit, Math.Round(baseline.Mean, decimals), low, high, status, decimals);
    }
}
