using Kardia.Library.Enums;

namespace Kardia.WebApi.Features.AiCoach.Models.DTOs;

/// <summary>The AI coach of an account: whether it's set up, whether an analysis is running, and the last report.</summary>
public sealed class AiCoachDTO
{
    /// <summary>Gets or sets a value indicating whether an AI provider is set up on this server.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets what the report looks at.</summary>
    public AiCoachTopic Topic { get; set; }

    /// <summary>Gets or sets the day the report is about (null: today).</summary>
    public DateOnly? Date { get; set; }

    /// <summary>Gets or sets the provider (anthropic or openai).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Gets or sets the model.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether an analysis is running.</summary>
    public bool Running { get; set; }

    /// <summary>Gets or sets when the running analysis started (UTC).</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets why the last analysis failed, or empty.</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>Gets or sets the last report, if any.</summary>
    public AiReportDTO? Report { get; set; }
}

/// <summary>A short report written by the AI coach from your numbers.</summary>
public sealed class AiReportDTO
{
    /// <summary>Gets or sets when it was written (UTC).</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>Gets or sets the days of data it looked at.</summary>
    public int DaysAnalysed { get; set; }

    /// <summary>Gets or sets the headline.</summary>
    public string Headline { get; set; } = string.Empty;

    /// <summary>Gets or sets two or three sentences on how you are.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets what went well.</summary>
    public List<string> WentWell { get; set; } = [];

    /// <summary>Gets or sets what to keep an eye on.</summary>
    public List<string> Watch { get; set; } = [];

    /// <summary>Gets or sets three things to do in the next seven days.</summary>
    public List<string> Actions { get; set; } = [];
}
