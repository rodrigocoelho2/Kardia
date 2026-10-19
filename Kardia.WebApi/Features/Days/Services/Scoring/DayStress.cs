using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Days.Services.Scoring;

/// <summary>One 10-minute block of the stress monitor.</summary>
/// <param name="At">When the block starts (local time).</param>
/// <param name="Bpm">The average heart rate in it, or null when the Watch had nothing.</param>
/// <param name="Level">The stress, 0–3, or null when the block was movement or had no heart rate.</param>
/// <param name="Active">Whether you were moving (a workout, steps or a heart rate only exercise explains).</param>
internal sealed record StressBlock(DateTime At, int? Bpm, double? Level, bool Active);

/// <summary>One point of the energy bank.</summary>
/// <param name="At">The time (local).</param>
/// <param name="Value">The charge, 0–100.</param>
internal sealed record EnergyPoint(DateTime At, double Value);

/// <summary>The stress monitor and energy bank of one day.</summary>
/// <param name="Blocks">The awake day in 10-minute blocks.</param>
/// <param name="Energy">The energy bank through the day.</param>
/// <param name="EnergyStart">The charge you woke up with.</param>
/// <param name="ActivityDrain">The charge spent moving.</param>
/// <param name="StressDrain">The charge spent on stress (heart rate above rest without moving).</param>
/// <param name="BaseDrain">The charge spent simply being awake.</param>
internal sealed record DayStressResult(
    IReadOnlyList<StressBlock> Blocks,
    IReadOnlyList<EnergyPoint> Energy,
    double? EnergyStart,
    double ActivityDrain,
    double StressDrain,
    double BaseDrain)
{
    /// <summary>Gets the blocks with a stress level.</summary>
    public IEnumerable<StressBlock> Judged => Blocks.Where(static block => block.Level is not null);

    /// <summary>Gets the average stress of the day, or null with under an hour of judged blocks.</summary>
    public double? Average => Judged.Count() >= 6 ? Math.Round(Judged.Average(static block => block.Level!.Value), 1) : null;

    /// <summary>Gets the latest stress level.</summary>
    public double? Current => Blocks.LastOrDefault(static block => block.Bpm is not null) is { } last ? last.Level : null;

    /// <summary>Gets the charge now (or at the end of the day).</summary>
    public double? EnergyNow => Energy.Count > 0 ? Energy[^1].Value : null;

    /// <summary>Gets the minutes of low (under 1), medium (1–2) and high (2 and over) stress.</summary>
    /// <returns>Low, medium and high minutes.</returns>
    public (int Low, int Medium, int High) Minutes()
    {
        var judged = Judged.ToList();
        return (judged.Count(static b => b.Level < 1) * DayStress.BlockMinutes,
            judged.Count(static b => b.Level is >= 1 and < 2) * DayStress.BlockMinutes,
            judged.Count(static b => b.Level >= 2) * DayStress.BlockMinutes);
    }
}

/// <summary>
/// The stress monitor (WHOOP) and energy bank (Bevel) from heart rate alone. Stress is how far your heart rate sits
/// above its resting level while you're <i>not</i> moving, from 0 (calm) to 3 (high); blocks with a workout, steps or a
/// heart rate only exercise explains are movement, and the block after one is skipped while your heart rate settles.
/// The energy bank starts the day at your recovery and drains with being awake, moving and stress.
/// </summary>
internal static class DayStress
{
    /// <summary>The minutes in a block.</summary>
    public const int BlockMinutes = 10;

    /// <summary>Steps in a block above which you were walking (about 15 a minute).</summary>
    public const int ActiveSteps = 150;

    /// <summary>The share of the heart-rate reserve above which a block counts as movement whatever the steps.</summary>
    public const double ActiveReserve = 0.4;

    /// <summary>The share of the heart-rate reserve that still reads as calm (sitting is a little above resting).</summary>
    public const double CalmReserve = 0.06;

    /// <summary>The share of the heart-rate reserve that reads as the top of the stress scale.</summary>
    public const double TopReserve = 0.34;

    /// <summary>Works out a day.</summary>
    /// <param name="day">The day.</param>
    /// <param name="samples">Its heart-rate minutes.</param>
    /// <param name="steps">Its timed step counts (may be empty when Health Auto Export only sends daily totals).</param>
    /// <param name="workouts">Its workouts.</param>
    /// <param name="wakeAt">When you got up, or null without a night.</param>
    /// <param name="rest">Your resting heart rate.</param>
    /// <param name="maxHeartRate">Your maximum heart rate.</param>
    /// <param name="recovery">The day's recovery, the energy you start with.</param>
    /// <param name="now">Now, when the day is today; the day stops there.</param>
    /// <returns>The stress blocks and energy bank.</returns>
    public static DayStressResult Compute(
        DateOnly day,
        IReadOnlyList<HeartRateRow> samples,
        IReadOnlyList<MetricSampleRow> steps,
        IReadOnlyList<WorkoutRow> workouts,
        DateTime? wakeAt,
        double rest,
        int maxHeartRate,
        int? recovery,
        DateTime? now)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        var end = now is { } clock && clock < midnight.AddDays(1) ? clock : midnight.AddDays(1);
        var first = samples.Where(sample => sample.MinuteAt >= midnight.AddHours(4)).Select(static sample => (DateTime?)sample.MinuteAt).FirstOrDefault();
        var start = wakeAt is { } wake && wake >= midnight && wake < end ? wake : first is { } seen && seen < end ? seen : (DateTime?)null;
        if (start is null || samples.Count == 0)
        {
            return new DayStressResult([], [], null, 0, 0, 0);
        }

        var from = Floor(start.Value);
        var reserve = Math.Max(30, maxHeartRate - rest);
        var minutes = samples.Where(sample => sample.MinuteAt >= from && sample.MinuteAt < end)
            .GroupBy(static sample => Floor(sample.MinuteAt))
            .ToDictionary(static group => group.Key, static group => group.Select(static sample => (double)sample.Bpm).ToList());
        var stepBlocks = steps.Where(sample => sample.SampleAt >= from && sample.SampleAt < end)
            .GroupBy(static sample => Floor(sample.SampleAt))
            .ToDictionary(static group => group.Key, static group => (double)group.Sum(static sample => sample.Value));

        var blocks = new List<StressBlock>();
        var settling = false;
        for (var at = from; at < end; at = at.AddMinutes(BlockMinutes))
        {
            var blockEnd = at.AddMinutes(BlockMinutes);
            // While you're still the Watch only measures every few minutes, so one reading is enough for a block.
            if (!minutes.TryGetValue(at, out var values) || values.Count == 0)
            {
                blocks.Add(new StressBlock(at, null, null, false));
                continue;
            }

            var bpm = values.Average();
            var share = (bpm - rest) / reserve;
            var moving = workouts.Any(workout => workout.StartedAt < blockEnd && workout.EndedAt > at)
                || (stepBlocks.TryGetValue(at, out var count) && count >= ActiveSteps)
                || share >= ActiveReserve;
            var level = moving || settling ? (double?)null : Math.Round(Math.Clamp((share - CalmReserve) / (TopReserve - CalmReserve) * 3, 0, 3), 1);
            blocks.Add(new StressBlock(at, (int)Math.Round(bpm), level, moving));
            settling = moving;
        }

        // The energy bank: start at recovery, spend a little every awake minute, more moving and under stress.
        var charge = Math.Clamp((double)(recovery ?? 50), 5, 100);
        var startCharge = charge;
        double activity = 0, stress = 0, baseline = 0;
        var energy = new List<EnergyPoint> { new(from, charge) };
        foreach (var block in blocks)
        {
            var span = Math.Min(BlockMinutes, (end - block.At).TotalMinutes);
            double spend;
            if (block.Active && block.Bpm is { } bpm)
            {
                var share = Math.Clamp((bpm - rest) / reserve, 0, 1);
                spend = (0.03 + 0.35 * share) * span;
                activity += spend - 0.03 * span;
                baseline += 0.03 * span;
            }
            else
            {
                var level = block.Level ?? 0.8;
                var calm = block.Level is < 0.5;
                var basePart = (calm ? 0.015 : 0.03) * span;
                var stressPart = 0.015 * level * span;
                spend = basePart + stressPart;
                stress += stressPart;
                baseline += basePart;
            }

            charge = Math.Max(0, charge - spend);
            energy.Add(new EnergyPoint(block.At.AddMinutes(span), Math.Round(charge, 1)));
        }

        return new DayStressResult(blocks, energy, startCharge, Math.Round(activity), Math.Round(stress), Math.Round(baseline));
    }

    /// <summary>The heart rate a workout must end at for its recovery to mean something (a stroll doesn't count).</summary>
    public const int MinRecoveryPeak = 110;

    /// <summary>Heart-rate recovery of a workout: how far the heart rate fell one and two minutes after the end.</summary>
    /// <param name="samples">Heart-rate minutes around the workout.</param>
    /// <param name="workout">The workout.</param>
    /// <returns>The fall after one and two minutes (null when a minute is missing).</returns>
    public static (int? One, int? Two) Recovery(IReadOnlyList<HeartRateRow> samples, WorkoutRow workout)
    {
        var endMinute = new DateTime(workout.EndedAt.Year, workout.EndedAt.Month, workout.EndedAt.Day, workout.EndedAt.Hour, workout.EndedAt.Minute, 0);
        var at = samples.Where(sample => sample.MinuteAt >= endMinute.AddMinutes(-2) && sample.MinuteAt <= endMinute.AddMinutes(2))
            .ToDictionary(static sample => sample.MinuteAt, static sample => (int)sample.Bpm);
        var peak = new[] { endMinute.AddMinutes(-1), endMinute }.Where(at.ContainsKey).Select(minute => at[minute]).DefaultIfEmpty(0).Max();
        if (peak < MinRecoveryPeak)
        {
            return (null, null);
        }

        int? Fall(int after) => at.TryGetValue(endMinute.AddMinutes(after), out var bpm) && peak - bpm is var fall and >= -10 and <= 120 ? Math.Max(0, fall) : null;
        return (Fall(1), Fall(2));
    }

    private static DateTime Floor(DateTime at) => new(at.Year, at.Month, at.Day, at.Hour, at.Minute / BlockMinutes * BlockMinutes, 0);
}
