using Kardia.WebApi.Features.Journals.Models.Entities;

namespace Kardia.WebApi.Features.Journals.Repositories;

/// <summary>
/// The journal (dbo.JournalEntry).
/// </summary>
internal interface IJournalRepository
{
    /// <summary>Lists the habits marked in a range of days.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day (inclusive).</param>
    /// <returns>The entries.</returns>
    Task<List<JournalEntry>> ListAsync(Guid userId, DateOnly from, DateOnly to);

    /// <summary>Marks or unmarks a habit on a day.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="day">The day.</param>
    /// <param name="tag">The habit.</param>
    /// <param name="on">Marked or not.</param>
    /// <returns>A task.</returns>
    Task SetAsync(Guid userId, DateOnly day, string tag, bool on);
}
