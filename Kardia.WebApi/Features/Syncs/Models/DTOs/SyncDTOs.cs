namespace Kardia.WebApi.Features.Syncs.Models.DTOs;

/// <summary>The account's Apple Health connection (never the key itself).</summary>
public sealed class SyncKeyDTO
{
    /// <summary>Gets or sets a value indicating whether a key exists.</summary>
    public bool HasKey { get; set; }

    /// <summary>Gets or sets the key's last characters.</summary>
    public string? KeyHint { get; set; }

    /// <summary>Gets or sets when the key was made.</summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Gets or sets when it was last used.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Gets or sets what the last sync brought.</summary>
    public string? LastSummary { get; set; }
}

/// <summary>A new key: shown this once, then only its hint.</summary>
public sealed class SyncKeyCreatedDTO
{
    /// <summary>Gets or sets the key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the connection with the new key.</summary>
    public SyncKeyDTO Status { get; set; } = new();
}

/// <summary>What a sync brought, in numbers and words (Health Auto Export and the Shortcut show the words).</summary>
public sealed class SyncIngestResultDTO
{
    /// <summary>Gets or sets the summary, e.g. "2 days · 1 440 heart-rate minutes · 1 night · 1 workout".</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the days touched.</summary>
    public int Days { get; set; }

    /// <summary>Gets or sets the daily values kept.</summary>
    public int Metrics { get; set; }

    /// <summary>Gets or sets the heart-rate minutes kept.</summary>
    public int HeartRates { get; set; }

    /// <summary>Gets or sets the nights kept.</summary>
    public int Sleeps { get; set; }

    /// <summary>Gets or sets the workouts kept.</summary>
    public int Workouts { get; set; }

    /// <summary>Gets or sets the first day touched.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Gets or sets the last day touched.</summary>
    public DateOnly? To { get; set; }
}
