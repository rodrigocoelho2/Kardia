namespace Kardia.WebApi.Features.Journals.Models.Entities;

/// <summary>
/// One habit marked on one day (dbo.JournalEntry).
/// </summary>
public sealed class JournalEntry
{
    /// <summary>Gets or sets the day.</summary>
    public DateTime DayDate { get; set; }

    /// <summary>Gets or sets the habit (<see cref="Library.Enums.JournalTag"/> name).</summary>
    public string Tag { get; set; } = string.Empty;
}
