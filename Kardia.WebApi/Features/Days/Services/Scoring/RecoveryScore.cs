using Kardia.Library.Enums;

namespace Kardia.WebApi.Features.Days.Services.Scoring;

/// <summary>What recovery is worked out from.</summary>
/// <param name="Hrv">This morning's HRV (SDNN, ms).</param>
/// <param name="HrvBaseline">Your HRV baseline.</param>
/// <param name="RestingHeartRate">Today's resting heart rate.</param>
/// <param name="RestingBaseline">Your resting heart rate baseline.</param>
/// <param name="RespiratoryRate">Last night's respiratory rate.</param>
/// <param name="RespiratoryBaseline">Your respiratory rate baseline.</param>
/// <param name="WristTemperature">Last night's wrist temperature.</param>
/// <param name="TemperatureBaseline">Your wrist temperature baseline.</param>
/// <param name="SleepPerformance">Last night's sleep against the need, 0–100.</param>
internal sealed record RecoveryInputs(
    double? Hrv,
    Baseline HrvBaseline,
    double? RestingHeartRate,
    Baseline RestingBaseline,
    double? RespiratoryRate,
    Baseline RespiratoryBaseline,
    double? WristTemperature,
    Baseline TemperatureBaseline,
    double? SleepPerformance);

/// <summary>A recovery score.</summary>
/// <param name="Score">1–99, or null without HRV and resting heart rate.</param>
/// <param name="Zone">The band.</param>
/// <param name="Signal">The weighted signal the score comes from (0 = an ordinary day for you).</param>
internal sealed record RecoveryResult(int? Score, RecoveryZone Zone, double Signal);

/// <summary>
/// Recovery: how ready your body is for strain today, from 1 to 99. Each input is compared with your own 30-day normal
/// and weighted — HRV 45 %, resting heart rate 25 %, sleep 20 %, respiratory rate 5 %, wrist temperature 5 % — then
/// squashed through a logistic curve so an ordinary day lands around 60. Inputs that are missing are left out and the
/// weights of the rest are scaled up. Pure, unit-tested.
/// </summary>
internal static class RecoveryScore
{
    private const double HrvWeight = 0.45;
    private const double RestingWeight = 0.25;
    private const double SleepWeight = 0.20;
    private const double RespiratoryWeight = 0.05;
    private const double TemperatureWeight = 0.05;

    /// <summary>
    /// Works out the score.
    /// </summary>
    /// <param name="inputs">The inputs.</param>
    /// <returns>The score, or a null score when neither HRV nor resting heart rate is known.</returns>
    public static RecoveryResult Calculate(RecoveryInputs inputs)
    {
        if (inputs.Hrv is null && inputs.RestingHeartRate is null)
        {
            return new RecoveryResult(null, RecoveryZone.None, 0);
        }

        var parts = new List<(double Weight, double Value)>();
        if (inputs.Hrv is { } hrv)
        {
            // HRV is log-normal: compare on a log scale, in units of your usual day-to-day spread.
            var spread = Math.Max(0.06, inputs.HrvBaseline.Sd / Math.Max(1, inputs.HrvBaseline.Mean));
            parts.Add((HrvWeight, Math.Clamp(Math.Log(hrv / Math.Max(1, inputs.HrvBaseline.Mean)) / spread, -3, 3)));
        }

        if (inputs.RestingHeartRate is { } resting)
        {
            parts.Add((RestingWeight, Math.Clamp(-inputs.RestingBaseline.Z(resting), -3, 3)));
        }

        if (inputs.SleepPerformance is { } sleep)
        {
            parts.Add((SleepWeight, Math.Clamp((sleep - 85) / 10, -3, 1.5)));
        }

        if (inputs.RespiratoryRate is { } respiratory)
        {
            // Only a clear change either way counts (a cold often shows here first).
            parts.Add((RespiratoryWeight, -Math.Clamp(Math.Abs(inputs.RespiratoryBaseline.Z(respiratory)) - 1, 0, 3)));
        }

        if (inputs.WristTemperature is { } temperature)
        {
            parts.Add((TemperatureWeight, -Math.Clamp((Math.Abs(temperature - inputs.TemperatureBaseline.Mean) - 0.25) / 0.25, 0, 3)));
        }

        var signal = parts.Sum(static part => part.Weight * part.Value) / parts.Sum(static part => part.Weight);
        var score = (int)Math.Clamp(Math.Round(100 / (1 + Math.Exp(-1.3 * (signal + 0.35)))), 1, 99);
        return new RecoveryResult(score, ZoneOf(score), signal);
    }

    /// <summary>
    /// The band of a score.
    /// </summary>
    /// <param name="score">The score.</param>
    /// <returns>Low (0–33), Medium (34–66) or High (67–100).</returns>
    public static RecoveryZone ZoneOf(int? score) => score switch
    {
        null => RecoveryZone.None,
        >= 67 => RecoveryZone.High,
        >= 34 => RecoveryZone.Medium,
        _ => RecoveryZone.Low,
    };

    /// <summary>
    /// The strain worth aiming for at a recovery: more when you're recovered, less when you're not.
    /// </summary>
    /// <param name="recovery">The recovery score.</param>
    /// <returns>The range, on the 0–21 strain scale.</returns>
    public static (double Low, double High) StrainTarget(int? recovery)
    {
        var center = Math.Clamp(5.5 + (recovery ?? 50) * 0.115, 4, 19);
        return (Math.Round(center - 1, 1), Math.Round(center + 1, 1));
    }
}
