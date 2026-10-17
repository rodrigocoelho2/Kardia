using Kardia.Library.Enums;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Days.Services.Scoring;

/// <summary>The cardiovascular load of a stretch of heart rate.</summary>
/// <param name="Trimp">The training impulse (Banister TRIMP).</param>
/// <param name="ZoneSeconds">Seconds in zones 1–5 (50–60 %, 60–70 %, 70–80 %, 80–90 %, 90 %+ of your maximum).</param>
/// <param name="MaxBpm">The highest heart rate.</param>
/// <param name="AvgBpm">The average heart rate.</param>
/// <param name="Minutes">Minutes with a heart rate.</param>
internal sealed record HeartLoad(double Trimp, int[] ZoneSeconds, int? MaxBpm, int? AvgBpm, int Minutes)
{
    /// <summary>Gets an empty load.</summary>
    public static HeartLoad None { get; } = new(0, new int[5], null, null, 0);
}

/// <summary>
/// Strain: how much load your heart took, on a 0–21 scale that gets harder to climb the higher you go (going from 18 to
/// 19 takes far more than from 8 to 9). Every minute with your heart rate above 30 % of your heart-rate reserve adds to
/// Banister's training impulse (TRIMP: minutes × reserve × 0.64·e^(1.92 × reserve), with the female constants when
/// set), and strain = 21 × (1 − e^(−TRIMP / 118)). A 40-minute steady run lands around 10–11, a day with that run and
/// some walking around 12–13. Pure, unit-tested.
/// </summary>
internal static class StrainScore
{
    /// <summary>The scale's top.</summary>
    public const double Max = 21;

    /// <summary>The TRIMP that takes strain to 63 % of the scale.</summary>
    public const double Scale = 118;

    /// <summary>Below this share of the heart-rate reserve, a minute doesn't count (sitting, standing, pottering about).</summary>
    public const double ReserveFloor = 0.2;

    /// <summary>Samples further apart than this don't stand for the time between them.</summary>
    public static readonly TimeSpan LongestGap = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Works out the load of a stretch of heart rate.
    /// </summary>
    /// <param name="samples">Heart-rate minutes, in order.</param>
    /// <param name="restingBpm">Resting heart rate.</param>
    /// <param name="maxBpm">Maximum heart rate.</param>
    /// <param name="sex">For the TRIMP constants.</param>
    /// <returns>The load.</returns>
    public static HeartLoad Load(IReadOnlyList<HeartRateRow> samples, double restingBpm, double maxBpm, BiologicalSex sex)
    {
        if (samples.Count == 0)
        {
            return HeartLoad.None;
        }

        var (a, b) = sex switch
        {
            BiologicalSex.Male => (0.64, 1.92),
            BiologicalSex.Female => (0.86, 1.67),
            _ => (0.75, 1.795),
        };
        var reserve = Math.Max(20, maxBpm - restingBpm);
        var zones = new int[5];
        var trimp = 0.0;
        var weighted = 0.0;
        var weight = 0.0;
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            var next = index + 1 < samples.Count ? samples[index + 1].MinuteAt : sample.MinuteAt.AddMinutes(1);
            var minutes = Math.Clamp((next - sample.MinuteAt).TotalMinutes, 0, LongestGap.TotalMinutes);
            if (minutes <= 0)
            {
                continue;
            }

            weighted += sample.Bpm * minutes;
            weight += minutes;

            var share = sample.Bpm / maxBpm;
            var zone = share switch
            {
                >= 0.9 => 4,
                >= 0.8 => 3,
                >= 0.7 => 2,
                >= 0.6 => 1,
                >= 0.5 => 0,
                _ => -1,
            };
            if (zone >= 0)
            {
                zones[zone] += (int)Math.Round(minutes * 60);
            }

            var hrr = (sample.Bpm - restingBpm) / reserve;
            if (hrr >= ReserveFloor)
            {
                hrr = Math.Min(hrr, 1.1);
                trimp += minutes * hrr * a * Math.Exp(b * hrr);
            }
        }

        return new HeartLoad(
            Math.Round(trimp, 2),
            zones,
            samples.Max(static sample => (int)sample.Bpm),
            weight > 0 ? (int)Math.Round(weighted / weight) : null,
            samples.Count);
    }

    /// <summary>
    /// Strain from a training impulse.
    /// </summary>
    /// <param name="trimp">The TRIMP.</param>
    /// <returns>Strain, 0–21, one decimal.</returns>
    public static double FromTrimp(double trimp) => Math.Round(Max * (1 - Math.Exp(-Math.Max(0, trimp) / Scale)), 1);

    /// <summary>
    /// Calories burned at rest in a day (Mifflin-St Jeor), when Apple Health didn't send resting energy.
    /// </summary>
    /// <param name="weightKg">Weight.</param>
    /// <param name="heightCm">Height.</param>
    /// <param name="age">Age.</param>
    /// <param name="sex">Sex.</param>
    /// <returns>kcal per day, or null when something is missing.</returns>
    public static double? RestingCalories(double? weightKg, double? heightCm, double? age, BiologicalSex sex)
    {
        if (weightKg is not { } weight || heightCm is not { } height || age is not { } years)
        {
            return null;
        }

        var offset = sex switch { BiologicalSex.Male => 5, BiologicalSex.Female => -161, _ => -78 };
        return 10 * weight + 6.25 * height - 5 * years + offset;
    }
}
