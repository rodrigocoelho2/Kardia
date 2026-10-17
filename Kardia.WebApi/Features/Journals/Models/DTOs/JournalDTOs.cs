using Kardia.Library.Enums;

namespace Kardia.WebApi.Features.Journals.Models.DTOs;

/// <summary>The journal of one day, and what each habit does to your recovery.</summary>
public sealed class JournalDayDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets every habit, marked or not.</summary>
    public List<JournalTagDTO> Tags { get; set; } = [];

    /// <summary>Gets or sets the effect of each habit with enough days logged, biggest first.</summary>
    public List<JournalImpactDTO> Impacts { get; set; } = [];

    /// <summary>Gets or sets the days the journal was filled in, in the last 90.</summary>
    public int DaysLogged { get; set; }
}

/// <summary>One habit on one day.</summary>
public sealed class JournalTagDTO
{
    /// <summary>Gets or sets the habit.</summary>
    public JournalTag Tag { get; set; }

    /// <summary>Gets or sets its name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether it is marked.</summary>
    public bool On { get; set; }
}

/// <summary>What one habit does to the next morning's recovery.</summary>
public sealed class JournalImpactDTO
{
    /// <summary>Gets or sets the habit.</summary>
    public JournalTag Tag { get; set; }

    /// <summary>Gets or sets its name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the days logged with it.</summary>
    public int DaysWith { get; set; }

    /// <summary>Gets or sets the average recovery the morning after.</summary>
    public double RecoveryWith { get; set; }

    /// <summary>Gets or sets the average recovery after days without it.</summary>
    public double RecoveryWithout { get; set; }

    /// <summary>Gets or sets the difference in recovery points (positive = it helps).</summary>
    public double Difference { get; set; }
}

/// <summary>Marks or unmarks a habit on a day.</summary>
public sealed class JournalSetRequest
{
    /// <summary>Gets or sets the account.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the habit.</summary>
    public JournalTag Tag { get; set; }

    /// <summary>Gets or sets a value indicating whether it is marked.</summary>
    public bool On { get; set; }
}
