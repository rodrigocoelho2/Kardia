using System.Globalization;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Days.Services.Scoring;

/// <summary>How much sleep a night needed, and why.</summary>
/// <param name="BaseMinutes">Your base need.</param>
/// <param name="StrainMinutes">Extra for the strain of the day before.</param>
/// <param name="DebtMinutes">Extra to pay back recent short nights.</param>
internal sealed record SleepNeed(int BaseMinutes, int StrainMinutes, int DebtMinutes)
{
    /// <summary>Gets the total need in minutes.</summary>
    public int TotalMinutes => BaseMinutes + StrainMinutes + DebtMinutes;
}

/// <summary>One stretch of one stage.</summary>
/// <param name="Stage">The stage.</param>
/// <param name="Start">Start, local time.</param>
/// <param name="End">End, local time.</param>
internal sealed record SleepSegment(SleepStage Stage, DateTime Start, DateTime End);

/// <summary>
/// Sleep: what a night needed (base need + the strain of the day before + recent sleep debt), how much of it you got
/// (performance), how much of the time in bed you slept (efficiency), how regular your bed and wake times are
/// (consistency), and when to go to bed tonight. Pure, unit-tested.
/// </summary>
internal static class SleepScore
{
    /// <summary>Minutes added to the need per strain point above 8 (a hard day needs more sleep).</summary>
    public const double MinutesPerStrainPoint = 4;

    /// <summary>The share of the last three nights' shortfall added to the need.</summary>
    public const double DebtShare = 0.25;

    /// <summary>The time it usually takes to fall asleep.</summary>
    public const int LatencyMinutes = 15;

    /// <summary>
    /// Works out a night's need.
    /// </summary>
    /// <param name="baseMinutes">Your base need.</param>
    /// <param name="strainBefore">The strain of the day before the night.</param>
    /// <param name="recentShortfalls">The shortfall (need minus sleep, never negative) of up to three nights before.</param>
    /// <returns>The need.</returns>
    public static SleepNeed Need(int baseMinutes, double? strainBefore, IEnumerable<int> recentShortfalls)
    {
        var strain = strainBefore is { } value && value > 8 ? (int)Math.Round(Math.Min(40, (value - 8) * MinutesPerStrainPoint)) : 0;
        var debt = (int)Math.Round(Math.Min(45, recentShortfalls.Take(3).Sum(static minutes => Math.Max(0, minutes)) * DebtShare));
        return new SleepNeed(baseMinutes, strain, debt);
    }

    /// <summary>The minutes asleep in a night (core + deep + REM).</summary>
    /// <param name="night">The night.</param>
    /// <returns>The minutes.</returns>
    public static int AsleepMinutes(SleepNightRow night) => night.CoreMinutes + night.DeepMinutes + night.RemMinutes;

    /// <summary>
    /// Sleep performance: the share of the need you slept, 0–100.
    /// </summary>
    /// <param name="asleepMinutes">Minutes asleep.</param>
    /// <param name="needMinutes">The night's need.</param>
    /// <returns>The performance.</returns>
    public static int Performance(int asleepMinutes, int needMinutes) =>
        needMinutes <= 0 ? 0 : (int)Math.Clamp(Math.Round(asleepMinutes * 100.0 / needMinutes), 0, 100);

    /// <summary>
    /// Sleep efficiency: the share of the time in bed you were asleep, 0–100.
    /// </summary>
    /// <param name="night">The night.</param>
    /// <returns>The efficiency.</returns>
    public static int Efficiency(SleepNightRow night)
    {
        var inBed = (night.BedEnd - night.BedStart).TotalMinutes;
        return inBed <= 0 ? 0 : (int)Math.Clamp(Math.Round(AsleepMinutes(night) * 100 / inBed), 0, 100);
    }

    /// <summary>
    /// Consistency: how close your bed and wake times are from night to night (the last four nights), 0–100.
    /// A spread of about ±10 minutes scores in the 90s; ±45 minutes in the 60s.
    /// </summary>
    /// <param name="nights">Recent nights, newest last.</param>
    /// <returns>The consistency, or null with fewer than two nights.</returns>
    public static int? Consistency(IReadOnlyList<SleepNightRow> nights)
    {
        var recent = nights.TakeLast(4).ToList();
        if (recent.Count < 2)
        {
            return null;
        }

        var bed = Spread(recent.Select(static night => ClockMinutes(night.BedStart)));
        var wake = Spread(recent.Select(static night => ClockMinutes(night.BedEnd)));
        return (int)Math.Clamp(Math.Round(100 - (bed + wake) / 2 * 0.85), 0, 100);
    }

    /// <summary>
    /// When to go to bed tonight to wake at your usual time having slept what you'll need.
    /// </summary>
    /// <param name="nights">Recent nights, newest last (the usual wake-up time is their median).</param>
    /// <param name="tonightNeedMinutes">Tonight's need.</param>
    /// <param name="today">Today's date.</param>
    /// <returns>The bedtime and the wake-up time, or null without any night.</returns>
    public static (DateTime Bedtime, DateTime WakeAt)? Bedtime(IReadOnlyList<SleepNightRow> nights, int tonightNeedMinutes, DateOnly today)
    {
        var wakes = nights.TakeLast(7).Select(static night => night.BedEnd.TimeOfDay.TotalMinutes).Order().ToList();
        if (wakes.Count == 0)
        {
            return null;
        }

        var median = wakes[wakes.Count / 2];
        var wakeAt = today.AddDays(1).ToDateTime(TimeOnly.MinValue).AddMinutes(Math.Round(median / 5) * 5);
        var bedtime = wakeAt.AddMinutes(-(tonightNeedMinutes + LatencyMinutes));
        return (new DateTime(bedtime.Year, bedtime.Month, bedtime.Day, bedtime.Hour, bedtime.Minute / 5 * 5, 0), wakeAt);
    }

    /// <summary>
    /// The stage stretches of a night, from <see cref="SleepNightRow.Stages"/>.
    /// </summary>
    /// <param name="night">The night.</param>
    /// <returns>The stretches, in order (empty when only totals were sent).</returns>
    public static List<SleepSegment> Segments(SleepNightRow night)
    {
        var segments = new List<SleepSegment>();
        foreach (var part in night.Stages.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split(',');
            if (bits.Length == 3
                && int.TryParse(bits[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stage) && Enum.IsDefined((SleepStage)stage)
                && int.TryParse(bits[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var from)
                && int.TryParse(bits[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var to) && to > from)
            {
                segments.Add(new SleepSegment((SleepStage)stage, night.BedStart.AddMinutes(from), night.BedStart.AddMinutes(to)));
            }
        }

        return segments;
    }

    /// <summary>
    /// Sleep cycles: how many times you went into REM after deep or core sleep (a new REM stretch after at least
    /// 20 minutes of other sleep).
    /// </summary>
    /// <param name="segments">The night's stretches.</param>
    /// <returns>The cycles.</returns>
    public static int Cycles(IReadOnlyList<SleepSegment> segments)
    {
        var cycles = 0;
        var sinceRem = 0.0;
        foreach (var segment in segments)
        {
            if (segment.Stage == SleepStage.Rem)
            {
                if (sinceRem >= 20 || cycles == 0)
                {
                    cycles++;
                }

                sinceRem = 0;
            }
            else if (segment.Stage != SleepStage.Awake)
            {
                sinceRem += (segment.End - segment.Start).TotalMinutes;
            }
        }

        return cycles;
    }

    /// <summary>Minutes since noon of the day before, so 23:30 and 00:30 are an hour apart.</summary>
    private static double ClockMinutes(DateTime time)
    {
        var minutes = time.TimeOfDay.TotalMinutes - 12 * 60;
        return minutes < 0 ? minutes + 24 * 60 : minutes;
    }

    private static double Spread(IEnumerable<double> values)
    {
        var list = values.ToList();
        var mean = list.Average();
        return Math.Sqrt(list.Sum(value => (value - mean) * (value - mean)) / list.Count);
    }
}
