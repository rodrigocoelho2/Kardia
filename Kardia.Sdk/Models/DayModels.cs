using Kardia.Library.Enums;

namespace Kardia.Sdk.Models;

/// <summary>Everything the Today, Recovery, Sleep and Strain screens show for one day.</summary>
public sealed class DayDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets a value indicating whether the day is today.</summary>
    public bool IsToday { get; set; }

    /// <summary>Gets or sets a value indicating whether anything was synced for the day.</summary>
    public bool HasData { get; set; }

    /// <summary>Gets or sets when Apple Health last synced (UTC).</summary>
    public DateTimeOffset? LastSyncAt { get; set; }

    /// <summary>Gets or sets the recovery.</summary>
    public RecoveryDTO Recovery { get; set; } = new();

    /// <summary>Gets or sets the night that ended this morning.</summary>
    public SleepDTO Sleep { get; set; } = new();

    /// <summary>Gets or sets the strain.</summary>
    public StrainDTO Strain { get; set; } = new();

    /// <summary>Gets or sets the heart rate through the day, in 5-minute points.</summary>
    public List<HeartRatePointDTO> HeartRates { get; set; } = [];

    /// <summary>Gets or sets the last 14 days (this one last).</summary>
    public List<DaySummaryDTO> Recent { get; set; } = [];

    /// <summary>Gets or sets the 30-day HRV trend (this day last).</summary>
    public List<TrendPointDTO> HrvTrend { get; set; } = [];

    /// <summary>Gets or sets the coach's tips for the day, most useful first.</summary>
    public List<CoachTipDTO> Coach { get; set; } = [];
}

/// <summary>Recovery and what went into it.</summary>
public sealed class RecoveryDTO
{
    /// <summary>Gets or sets the score (1–99), or null without heart data.</summary>
    public int? Score { get; set; }

    /// <summary>Gets or sets the band.</summary>
    public RecoveryZone Zone { get; set; }

    /// <summary>Gets or sets HRV (ms).</summary>
    public double? Hrv { get; set; }

    /// <summary>Gets or sets the HRV baseline (ms).</summary>
    public double HrvBaseline { get; set; }

    /// <summary>Gets or sets the resting heart rate.</summary>
    public double? RestingHeartRate { get; set; }

    /// <summary>Gets or sets the resting heart rate baseline.</summary>
    public double RestingBaseline { get; set; }

    /// <summary>Gets or sets the respiratory rate.</summary>
    public double? RespiratoryRate { get; set; }

    /// <summary>Gets or sets the respiratory rate baseline.</summary>
    public double RespiratoryBaseline { get; set; }

    /// <summary>Gets or sets blood oxygen (%).</summary>
    public double? BloodOxygen { get; set; }

    /// <summary>Gets or sets the wrist temperature compared with your baseline (°C).</summary>
    public double? WristTemperatureChange { get; set; }

    /// <summary>Gets or sets the sleep performance that went into it.</summary>
    public int? SleepPerformance { get; set; }

    /// <summary>Gets or sets the strain range worth aiming for today.</summary>
    public double TargetLow { get; set; }

    /// <summary>Gets or sets the top of the strain range.</summary>
    public double TargetHigh { get; set; }
}

/// <summary>A night.</summary>
public sealed class SleepDTO
{
    /// <summary>Gets or sets a value indicating whether there is a night.</summary>
    public bool HasNight { get; set; }

    /// <summary>Gets or sets the share of the need slept (0–100).</summary>
    public int? Performance { get; set; }

    /// <summary>Gets or sets the minutes asleep.</summary>
    public int AsleepMinutes { get; set; }

    /// <summary>Gets or sets the minutes in bed.</summary>
    public int InBedMinutes { get; set; }

    /// <summary>Gets or sets what the night needed, in minutes.</summary>
    public int NeedMinutes { get; set; }

    /// <summary>Gets or sets the base part of the need.</summary>
    public int NeedBaseMinutes { get; set; }

    /// <summary>Gets or sets the part of the need from the day before's strain.</summary>
    public int NeedStrainMinutes { get; set; }

    /// <summary>Gets or sets the part of the need from recent short nights.</summary>
    public int NeedDebtMinutes { get; set; }

    /// <summary>Gets or sets the efficiency (0–100).</summary>
    public int? Efficiency { get; set; }

    /// <summary>Gets or sets the consistency (0–100).</summary>
    public int? Consistency { get; set; }

    /// <summary>Gets or sets how far short of the need the night was.</summary>
    public int DebtMinutes { get; set; }

    /// <summary>Gets or sets when you went to bed (local time).</summary>
    public DateTime? BedStart { get; set; }

    /// <summary>Gets or sets when you got up (local time).</summary>
    public DateTime? BedEnd { get; set; }

    /// <summary>Gets or sets the minutes awake.</summary>
    public int AwakeMinutes { get; set; }

    /// <summary>Gets or sets the minutes of REM.</summary>
    public int RemMinutes { get; set; }

    /// <summary>Gets or sets the minutes of core (light) sleep.</summary>
    public int CoreMinutes { get; set; }

    /// <summary>Gets or sets the minutes of deep sleep.</summary>
    public int DeepMinutes { get; set; }

    /// <summary>Gets or sets the sleep cycles.</summary>
    public int Cycles { get; set; }

    /// <summary>Gets or sets the stages in order (empty when only totals were synced).</summary>
    public List<SleepSegmentDTO> Segments { get; set; } = [];

    /// <summary>Gets or sets the respiratory rate.</summary>
    public double? RespiratoryRate { get; set; }

    /// <summary>Gets or sets tonight's need, in minutes.</summary>
    public int TonightNeedMinutes { get; set; }

    /// <summary>Gets or sets the part of tonight's need that comes from today's strain, in minutes.</summary>
    public int TonightStrainMinutes { get; set; }

    /// <summary>Gets or sets the part of tonight's need that pays back sleep debt, in minutes.</summary>
    public int TonightDebtMinutes { get; set; }

    /// <summary>Gets or sets when to go to bed tonight (local time).</summary>
    public DateTime? Bedtime { get; set; }

    /// <summary>Gets or sets the usual wake-up time it is worked out for (local time).</summary>
    public DateTime? WakeAt { get; set; }
}

/// <summary>One stretch of one sleep stage.</summary>
public sealed class SleepSegmentDTO
{
    /// <summary>Gets or sets the stage.</summary>
    public SleepStage Stage { get; set; }

    /// <summary>Gets or sets the start (local time).</summary>
    public DateTime Start { get; set; }

    /// <summary>Gets or sets the end (local time).</summary>
    public DateTime End { get; set; }
}

/// <summary>The day's strain.</summary>
public sealed class StrainDTO
{
    /// <summary>Gets or sets the strain (0–21), or null without heart rate.</summary>
    public double? Score { get; set; }

    /// <summary>Gets or sets the bottom of the range worth aiming for.</summary>
    public double TargetLow { get; set; }

    /// <summary>Gets or sets the top of that range.</summary>
    public double TargetHigh { get; set; }

    /// <summary>Gets or sets the active calories.</summary>
    public double? ActiveKcal { get; set; }

    /// <summary>Gets or sets all calories (active + resting).</summary>
    public double? TotalKcal { get; set; }

    /// <summary>Gets or sets the steps.</summary>
    public double? Steps { get; set; }

    /// <summary>Gets or sets the highest heart rate.</summary>
    public int? MaxBpm { get; set; }

    /// <summary>Gets or sets the average heart rate.</summary>
    public int? AvgBpm { get; set; }

    /// <summary>Gets or sets the lowest heart rate.</summary>
    public int? MinBpm { get; set; }

    /// <summary>Gets or sets the latest heart rate.</summary>
    public int? CurrentBpm { get; set; }

    /// <summary>Gets or sets the minutes in heart-rate zones 1–5.</summary>
    public List<int> ZoneMinutes { get; set; } = [0, 0, 0, 0, 0];

    /// <summary>Gets or sets the maximum heart rate the zones use.</summary>
    public int MaxHeartRate { get; set; }

    /// <summary>Gets or sets the day's workouts.</summary>
    public List<WorkoutDTO> Workouts { get; set; } = [];
}

/// <summary>A workout.</summary>
public sealed class WorkoutDTO
{
    /// <summary>Gets or sets the Watch's workout type.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets when it started (local time).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Gets or sets how long it lasted, in minutes.</summary>
    public int Minutes { get; set; }

    /// <summary>Gets or sets the active calories.</summary>
    public double? ActiveKcal { get; set; }

    /// <summary>Gets or sets the distance in km.</summary>
    public double? DistanceKm { get; set; }

    /// <summary>Gets or sets the average heart rate.</summary>
    public int? AvgBpm { get; set; }

    /// <summary>Gets or sets the maximum heart rate.</summary>
    public int? MaxBpm { get; set; }

    /// <summary>Gets or sets the workout's own strain (0–21).</summary>
    public double? Strain { get; set; }

    /// <summary>Gets or sets its heart rate, one point a minute (for the small chart).</summary>
    public List<int> HeartRates { get; set; } = [];
}

/// <summary>A heart-rate point.</summary>
public sealed class HeartRatePointDTO
{
    /// <summary>Gets or sets the time (local).</summary>
    public DateTime At { get; set; }

    /// <summary>Gets or sets the heart rate.</summary>
    public int Bpm { get; set; }
}

/// <summary>A day in short, for charts and the week strip.</summary>
public sealed class DaySummaryDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the recovery.</summary>
    public int? Recovery { get; set; }

    /// <summary>Gets or sets the recovery band.</summary>
    public RecoveryZone Zone { get; set; }

    /// <summary>Gets or sets the strain.</summary>
    public double? Strain { get; set; }

    /// <summary>Gets or sets the sleep performance.</summary>
    public int? SleepPerformance { get; set; }

    /// <summary>Gets or sets the minutes asleep.</summary>
    public int? AsleepMinutes { get; set; }

    /// <summary>Gets or sets HRV.</summary>
    public double? Hrv { get; set; }

    /// <summary>Gets or sets the resting heart rate.</summary>
    public double? RestingHeartRate { get; set; }
}

/// <summary>One point of a trend line.</summary>
public sealed class TrendPointDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the value.</summary>
    public double? Value { get; set; }
}

/// <summary>A tip from the coach.</summary>
public sealed class CoachTipDTO
{
    /// <summary>Gets or sets what it is about.</summary>
    public CoachArea Area { get; set; }

    /// <summary>Gets or sets how it reads.</summary>
    public CoachTone Tone { get; set; }

    /// <summary>Gets or sets the headline.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the tip.</summary>
    public string Text { get; set; } = string.Empty;
}
