using Kardia.Library.Enums;
using Kardia.WebApi.Features.Days.Models.DTOs;

namespace Kardia.WebApi.Features.Reports.Models.DTOs;

/// <summary>A weekly or monthly performance assessment: the period against the one before it.</summary>
public sealed class ReportDTO
{
    /// <summary>Gets or sets the period.</summary>
    public ReportPeriod Period { get; set; }

    /// <summary>Gets or sets the title ("Week of 12 Oct", "October 2026").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the first day.</summary>
    public DateOnly From { get; set; }

    /// <summary>Gets or sets the last day.</summary>
    public DateOnly To { get; set; }

    /// <summary>Gets or sets the first day of the period before.</summary>
    public DateOnly PreviousFrom { get; set; }

    /// <summary>Gets or sets the last day of the period before.</summary>
    public DateOnly PreviousTo { get; set; }

    /// <summary>Gets or sets a value indicating whether the period is still running.</summary>
    public bool InProgress { get; set; }

    /// <summary>Gets or sets the days with any Apple Watch data.</summary>
    public int DaysWithData { get; set; }

    /// <summary>Gets or sets the assessment, 0–100: strain matched to recovery, sleep against need, and consistency.</summary>
    public int? Score { get; set; }

    /// <summary>Gets or sets the assessment in a word: Peak, Strong, Fair or Off track.</summary>
    public string Assessment { get; set; } = string.Empty;

    /// <summary>Gets or sets the days strain landed in the target for that day's recovery.</summary>
    public int StrainOnTargetDays { get; set; }

    /// <summary>Gets or sets the finished days with a recovery (today's strain still grows, so it isn't judged).</summary>
    public int StrainDays { get; set; }

    /// <summary>Gets or sets the days with a recovery.</summary>
    public int RecoveryDays { get; set; }

    /// <summary>Gets or sets the green days.</summary>
    public int HighDays { get; set; }

    /// <summary>Gets or sets the yellow days.</summary>
    public int MediumDays { get; set; }

    /// <summary>Gets or sets the red days.</summary>
    public int LowDays { get; set; }

    /// <summary>Gets or sets the days with two or more Health Monitor signs outside their range.</summary>
    public int HealthFlaggedDays { get; set; }

    /// <summary>Gets or sets the cardio load on the last day.</summary>
    public double? CardioRatio { get; set; }

    /// <summary>Gets or sets the cardio load band on the last day.</summary>
    public CardioLoadStatus CardioStatus { get; set; }

    /// <summary>Gets or sets the averages and totals against the period before.</summary>
    public List<ReportMetricDTO> Metrics { get; set; } = [];

    /// <summary>Gets or sets what stood out, in sentences.</summary>
    public List<string> Highlights { get; set; } = [];

    /// <summary>Gets or sets every day of the period.</summary>
    public List<DaySummaryDTO> Days { get; set; } = [];
}

/// <summary>One value of a report against the period before.</summary>
public sealed class ReportMetricDTO
{
    /// <summary>Gets or sets the key: recovery, hrv, resting, asleep, sleep, strain, active, steps or vo2max.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the unit (%, ms, bpm, min, steps…).</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Gets or sets the decimals to show.</summary>
    public int Decimals { get; set; }

    /// <summary>Gets or sets this period's value.</summary>
    public double? Value { get; set; }

    /// <summary>Gets or sets the period before's value.</summary>
    public double? Previous { get; set; }

    /// <summary>Gets or sets the change.</summary>
    public double? Change { get; set; }

    /// <summary>Gets or sets a value indicating whether a higher value is better.</summary>
    public bool HigherIsBetter { get; set; }

    /// <summary>Gets or sets how it moved: better, worse or steady (empty without both values).</summary>
    public string Trend { get; set; } = string.Empty;
}
