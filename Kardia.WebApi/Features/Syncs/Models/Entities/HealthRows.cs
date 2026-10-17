namespace Kardia.WebApi.Features.Syncs.Models.Entities;

/// <summary>
/// The personal key of an account for Health Auto Export / the iOS Shortcut (dbo.SyncKey). Only the SHA-256 of the
/// key is kept; the key itself is shown once, when it is made.
/// </summary>
public sealed class SyncKeyRow
{
    /// <summary>Gets or sets the SHA-256 of the key.</summary>
    public byte[] KeyHash { get; set; } = [];

    /// <summary>Gets or sets the last characters of the key, to recognise it.</summary>
    public string KeyHint { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was made (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when it was last used (UTC).</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Gets or sets what the last sync brought, in words.</summary>
    public string LastSummary { get; set; } = string.Empty;
}

/// <summary>One value of one day (dbo.DailyMetric).</summary>
public sealed class DailyMetricRow
{
    /// <summary>Gets or sets the day.</summary>
    public DateTime DayDate { get; set; }

    /// <summary>Gets or sets the metric (<see cref="Library.Enums.HealthMetric"/>).</summary>
    public int Metric { get; set; }

    /// <summary>Gets or sets the value, in the metric's unit.</summary>
    public decimal Value { get; set; }
}

/// <summary>The heart rate of one minute (dbo.HeartRateMinute), local time.</summary>
public sealed class HeartRateRow
{
    /// <summary>Gets or sets the minute (seconds are zero).</summary>
    public DateTime MinuteAt { get; set; }

    /// <summary>Gets or sets the average heart rate that minute.</summary>
    public short Bpm { get; set; }
}

/// <summary>
/// One timed reading of a value the Watch measures mostly in your sleep (dbo.MetricSample): HRV, respiratory rate and
/// blood oxygen. The day's value of these is the average of the readings taken during that night (see NightlyVitals).
/// </summary>
public sealed class MetricSampleRow
{
    /// <summary>Gets or sets the metric (<see cref="Library.Enums.HealthMetric"/> number).</summary>
    public int Metric { get; set; }

    /// <summary>Gets or sets when it was measured, local time.</summary>
    public DateTime SampleAt { get; set; }

    /// <summary>Gets or sets the value.</summary>
    public decimal Value { get; set; }
}

/// <summary>A night (dbo.SleepNight), keyed by the date you woke up.</summary>
public sealed class SleepNightRow
{
    /// <summary>Gets or sets the date of the morning you woke up.</summary>
    public DateTime NightDate { get; set; }

    /// <summary>Gets or sets when you got into bed (or fell asleep), local time.</summary>
    public DateTime BedStart { get; set; }

    /// <summary>Gets or sets when you got up, local time.</summary>
    public DateTime BedEnd { get; set; }

    /// <summary>Gets or sets the minutes awake in bed.</summary>
    public short AwakeMinutes { get; set; }

    /// <summary>Gets or sets the minutes of REM sleep.</summary>
    public short RemMinutes { get; set; }

    /// <summary>Gets or sets the minutes of core (light) sleep.</summary>
    public short CoreMinutes { get; set; }

    /// <summary>Gets or sets the minutes of deep sleep.</summary>
    public short DeepMinutes { get; set; }

    /// <summary>
    /// Gets or sets the stages, in order: "stage,startMinute,endMinute;…" with minutes counted from <see cref="BedStart"/>
    /// (stage = <see cref="Library.Enums.SleepStage"/> number). Empty when only the totals were sent.
    /// </summary>
    public string Stages { get; set; } = string.Empty;
}

/// <summary>A workout from the Watch (dbo.Workout), local times.</summary>
public sealed class WorkoutRow
{
    /// <summary>Gets or sets when it started.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Gets or sets when it ended.</summary>
    public DateTime EndedAt { get; set; }

    /// <summary>Gets or sets the Watch's workout type ("Outdoor Run", "Traditional Strength Training").</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the active calories.</summary>
    public decimal? ActiveKcal { get; set; }

    /// <summary>Gets or sets the distance in km.</summary>
    public decimal? DistanceKm { get; set; }

    /// <summary>Gets or sets the average heart rate.</summary>
    public short? AvgBpm { get; set; }

    /// <summary>Gets or sets the maximum heart rate.</summary>
    public short? MaxBpm { get; set; }
}

/// <summary>The cardiovascular load of one day (dbo.DayLoad), worked out from its heart rate after every sync.</summary>
public sealed class DayLoadRow
{
    /// <summary>Gets or sets the day.</summary>
    public DateTime DayDate { get; set; }

    /// <summary>Gets or sets the training impulse (Banister TRIMP) the strain score comes from.</summary>
    public decimal Trimp { get; set; }

    /// <summary>Gets or sets the seconds in heart-rate zones 1–5, comma separated.</summary>
    public string ZoneSeconds { get; set; } = "0,0,0,0,0";

    /// <summary>Gets or sets the day's highest heart rate.</summary>
    public short? MaxBpm { get; set; }

    /// <summary>Gets or sets the day's average heart rate.</summary>
    public short? AvgBpm { get; set; }

    /// <summary>Gets or sets how many minutes had a heart rate (how long the Watch was worn, roughly).</summary>
    public short Minutes { get; set; }
}
