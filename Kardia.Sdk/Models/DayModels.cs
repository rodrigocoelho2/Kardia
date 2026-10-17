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

    /// <summary>Gets or sets the Health Monitor: last night's five signs against your typical range.</summary>
    public HealthMonitorDTO Health { get; set; } = new();

    /// <summary>Gets or sets the cardio load: the last 7 days of heart load against the last 28.</summary>
    public CardioLoadDTO CardioLoad { get; set; } = new();

    /// <summary>Gets or sets the coach's tips for the day, most useful first.</summary>
    public List<CoachTipDTO> Coach { get; set; } = [];

    /// <summary>Gets or sets the stress monitor: heart rate above rest while you weren't moving, 0–3.</summary>
    public StressDTO Stress { get; set; } = new();

    /// <summary>Gets or sets the energy bank: the charge you woke up with and how the day spent it.</summary>
    public EnergyDTO Energy { get; set; } = new();
}

/// <summary>The stress monitor of a day.</summary>
public sealed class StressDTO
{
    /// <summary>Gets or sets a value indicating whether there was enough heart rate to judge (an hour of still blocks).</summary>
    public bool Ready { get; set; }

    /// <summary>Gets or sets the day's average stress, 0–3.</summary>
    public double? Average { get; set; }

    /// <summary>Gets or sets the latest stress level, 0–3 (null when you were moving).</summary>
    public double? Current { get; set; }

    /// <summary>Gets or sets the minutes of low stress (under 1).</summary>
    public int LowMinutes { get; set; }

    /// <summary>Gets or sets the minutes of medium stress (1–2).</summary>
    public int MediumMinutes { get; set; }

    /// <summary>Gets or sets the minutes of high stress (2–3).</summary>
    public int HighMinutes { get; set; }

    /// <summary>Gets or sets the minutes moving (left out of stress).</summary>
    public int ActiveMinutes { get; set; }

    /// <summary>Gets or sets the resting heart rate stress is measured from.</summary>
    public int RestingBpm { get; set; }

    /// <summary>Gets or sets the awake day in 10-minute blocks.</summary>
    public List<StressBlockDTO> Blocks { get; set; } = [];
}

/// <summary>A 10-minute block of the stress monitor.</summary>
public sealed class StressBlockDTO
{
    /// <summary>Gets or sets when it starts (local time).</summary>
    public DateTime At { get; set; }

    /// <summary>Gets or sets the average heart rate, or null when the Watch had nothing.</summary>
    public int? Bpm { get; set; }

    /// <summary>Gets or sets the stress, 0–3, or null when moving or without heart rate.</summary>
    public double? Level { get; set; }

    /// <summary>Gets or sets a value indicating whether you were moving.</summary>
    public bool Active { get; set; }
}

/// <summary>The energy bank of a day.</summary>
public sealed class EnergyDTO
{
    /// <summary>Gets or sets a value indicating whether there's a curve to show.</summary>
    public bool Ready { get; set; }

    /// <summary>Gets or sets the charge you woke up with (your recovery), 0–100.</summary>
    public int? Start { get; set; }

    /// <summary>Gets or sets the charge now (or at the end of the day), 0–100.</summary>
    public int? Now { get; set; }

    /// <summary>Gets or sets the charge spent moving.</summary>
    public int ActivityDrain { get; set; }

    /// <summary>Gets or sets the charge spent on stress.</summary>
    public int StressDrain { get; set; }

    /// <summary>Gets or sets the charge spent just being awake.</summary>
    public int BaseDrain { get; set; }

    /// <summary>Gets or sets the charge through the day.</summary>
    public List<TrendPointTimeDTO> Points { get; set; } = [];
}

/// <summary>A value at a time of day.</summary>
public sealed class TrendPointTimeDTO
{
    /// <summary>Gets or sets the time (local).</summary>
    public DateTime At { get; set; }

    /// <summary>Gets or sets the value.</summary>
    public double Value { get; set; }
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

    /// <summary>Gets or sets the sleep goal: the share of the need to aim for (100, 85 or 70).</summary>
    public int GoalPercent { get; set; } = 100;

    /// <summary>Gets or sets the sleep to plan for tonight with the goal (the bedtime is set from it).</summary>
    public int TonightGoalMinutes { get; set; }

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

    /// <summary>Gets or sets the heart-rate recovery of the workouts of the last 60 days, oldest first.</summary>
    public List<HeartRecoveryDTO> RecoveryHistory { get; set; } = [];

    /// <summary>Gets or sets the average one-minute heart-rate recovery of the last 60 days.</summary>
    public double? RecoveryAverage { get; set; }
}

/// <summary>The heart-rate recovery of one workout.</summary>
public sealed class HeartRecoveryDTO
{
    /// <summary>Gets or sets when the workout started (local time).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Gets or sets the Watch's workout type.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the bpm the heart rate fell in the first minute.</summary>
    public int? OneMinute { get; set; }

    /// <summary>Gets or sets the bpm the heart rate fell in the first two minutes.</summary>
    public int? TwoMinutes { get; set; }
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

    /// <summary>Gets or sets how many bpm the heart rate fell in the first minute after the end (heart-rate recovery).</summary>
    public int? RecoveryOneMinute { get; set; }

    /// <summary>Gets or sets how many bpm the heart rate fell in the first two minutes after the end.</summary>
    public int? RecoveryTwoMinutes { get; set; }

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

    /// <summary>Gets or sets the respiratory rate while asleep.</summary>
    public double? RespiratoryRate { get; set; }

    /// <summary>Gets or sets blood oxygen.</summary>
    public double? BloodOxygen { get; set; }

    /// <summary>Gets or sets the wrist temperature change from your normal, °C.</summary>
    public double? WristTemperatureChange { get; set; }

    /// <summary>Gets or sets cardio fitness (VO₂ max), when the Watch estimated it that day.</summary>
    public double? Vo2Max { get; set; }

    /// <summary>Gets or sets the steps.</summary>
    public double? Steps { get; set; }

    /// <summary>Gets or sets the minutes spent in daylight (Apple Watch "Time in Daylight").</summary>
    public double? DaylightMinutes { get; set; }

    /// <summary>Gets or sets the minutes in heart-rate zone 2 or higher.</summary>
    public int ActiveMinutes { get; set; }

    /// <summary>Gets or sets the sleep need of the night, in minutes.</summary>
    public int NeedMinutes { get; set; }

    /// <summary>Gets or sets when you went to bed (the night that ended this morning).</summary>
    public DateTime? BedStart { get; set; }

    /// <summary>Gets or sets the Health Monitor signs outside your typical range (keys: hrv, resting…).</summary>
    public List<string> HealthFlags { get; set; } = [];
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

/// <summary>The Health Monitor of a day: five signs measured overnight, each against your typical range.</summary>
public sealed class HealthMonitorDTO
{
    /// <summary>Gets or sets a value indicating whether there's enough history (7 days) to judge any sign.</summary>
    public bool Ready { get; set; }

    /// <summary>Gets or sets how many judged signs are in range.</summary>
    public int InRange { get; set; }

    /// <summary>Gets or sets how many signs were judged.</summary>
    public int Judged { get; set; }

    /// <summary>Gets or sets the signs.</summary>
    public List<HealthSignDTO> Signs { get; set; } = [];
}

/// <summary>One Health Monitor sign.</summary>
public sealed class HealthSignDTO
{
    /// <summary>Gets or sets the key: hrv, resting, respiratory, oxygen or temperature.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the value (temperature: the change from your normal).</summary>
    public double? Value { get; set; }

    /// <summary>Gets or sets the unit.</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Gets or sets your normal.</summary>
    public double Typical { get; set; }

    /// <summary>Gets or sets the bottom of your typical range.</summary>
    public double Low { get; set; }

    /// <summary>Gets or sets the top of your typical range.</summary>
    public double High { get; set; }

    /// <summary>Gets or sets where the value sits.</summary>
    public HealthSignStatus Status { get; set; }

    /// <summary>Gets or sets the decimals to show.</summary>
    public int Decimals { get; set; }
}

/// <summary>Cardio load: the last 7 days of heart load against the last 28.</summary>
public sealed class CardioLoadDTO
{
    /// <summary>Gets or sets a value indicating whether there are enough days (21 of the last 28) to judge it.</summary>
    public bool Ready { get; set; }

    /// <summary>Gets or sets the acute:chronic ratio.</summary>
    public double? Ratio { get; set; }

    /// <summary>Gets or sets the band.</summary>
    public CardioLoadStatus Status { get; set; }

    /// <summary>Gets or sets the mean daily heart load (TRIMP) of the last 7 days.</summary>
    public double AcuteLoad { get; set; }

    /// <summary>Gets or sets the mean daily heart load (TRIMP) of the last 28 days.</summary>
    public double ChronicLoad { get; set; }

    /// <summary>Gets or sets the daily strain that keeps the load productive, bottom.</summary>
    public double? StrainLow { get; set; }

    /// <summary>Gets or sets the daily strain that keeps the load productive, top.</summary>
    public double? StrainHigh { get; set; }

    /// <summary>Gets or sets the ratio over the last 8 weeks.</summary>
    public List<TrendPointDTO> History { get; set; } = [];
}

/// <summary>The Health Monitor over 30 or 180 days, for the report you can print or save as PDF.</summary>
public sealed class HealthReportDTO
{
    /// <summary>Gets or sets the first day.</summary>
    public DateOnly From { get; set; }

    /// <summary>Gets or sets the last day.</summary>
    public DateOnly To { get; set; }

    /// <summary>Gets or sets the days with any sign measured.</summary>
    public int DaysMeasured { get; set; }

    /// <summary>Gets or sets the days with two or more signs outside their range.</summary>
    public int DaysFlagged { get; set; }

    /// <summary>Gets or sets one series per sign.</summary>
    public List<HealthSeriesDTO> Signs { get; set; } = [];
}

/// <summary>One sign over the report.</summary>
public sealed class HealthSeriesDTO
{
    /// <summary>Gets or sets the key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the unit.</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Gets or sets the decimals to show.</summary>
    public int Decimals { get; set; }

    /// <summary>Gets or sets the average of the period.</summary>
    public double? Average { get; set; }

    /// <summary>Gets or sets the lowest value.</summary>
    public double? Min { get; set; }

    /// <summary>Gets or sets the highest value.</summary>
    public double? Max { get; set; }

    /// <summary>Gets or sets your typical range at the end of the period, bottom.</summary>
    public double Low { get; set; }

    /// <summary>Gets or sets your typical range at the end of the period, top.</summary>
    public double High { get; set; }

    /// <summary>Gets or sets the days in range.</summary>
    public int InRangeDays { get; set; }

    /// <summary>Gets or sets the days outside.</summary>
    public int OutsideDays { get; set; }

    /// <summary>Gets or sets every day.</summary>
    public List<HealthPointDTO> Days { get; set; } = [];
}

/// <summary>One day of a sign.</summary>
public sealed class HealthPointDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the value.</summary>
    public double? Value { get; set; }

    /// <summary>Gets or sets the range that day, bottom.</summary>
    public double Low { get; set; }

    /// <summary>Gets or sets the range that day, top.</summary>
    public double High { get; set; }

    /// <summary>Gets or sets where the value sat.</summary>
    public HealthSignStatus Status { get; set; }
}
