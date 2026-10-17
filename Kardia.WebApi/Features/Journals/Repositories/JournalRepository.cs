using Kardia.WebApi.Extensions.Data;
using Kardia.WebApi.Features.Journals.Models.Entities;

namespace Kardia.WebApi.Features.Journals.Repositories;

/// <summary>
/// The journal in SQL Server (stored procedures in Kardia.Database/StoredProcedures/Journals).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class JournalRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IJournalRepository
{
    /// <inheritdoc />
    public Task<List<JournalEntry>> ListAsync(Guid userId, DateOnly from, DateOnly to) =>
        QueryAsync<JournalEntry>("[dbo].[JournalList]", new { UserUniqueID = userId, From = Date(from), To = Date(to) });

    /// <inheritdoc />
    public Task SetAsync(Guid userId, DateOnly day, string tag, bool on) =>
        ExecuteAsync("[dbo].[JournalSet]", new { UserUniqueID = userId, DayDate = Date(day), Tag = tag, IsOn = on });
}

/// <summary>
/// The journal in demo mode (<see cref="MemoryStore"/>).
/// </summary>
/// <param name="store">The in-memory tables.</param>
internal sealed class MemoryJournalRepository(MemoryStore store) : IJournalRepository
{
    /// <inheritdoc />
    public Task<List<JournalEntry>> ListAsync(Guid userId, DateOnly from, DateOnly to)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.ToDateTime(TimeOnly.MinValue);
        lock (store.Gate)
        {
            return Task.FromResult(MemoryStore.For(store.Journal, userId)
                .Where(entry => entry.Day >= start && entry.Day <= end)
                .OrderBy(static entry => entry.Day).ThenBy(static entry => entry.Tag, StringComparer.Ordinal)
                .Select(static entry => new JournalEntry { DayDate = entry.Day, Tag = entry.Tag })
                .ToList());
        }
    }

    /// <inheritdoc />
    public Task SetAsync(Guid userId, DateOnly day, string tag, bool on)
    {
        lock (store.Gate)
        {
            var entries = MemoryStore.For(store.Journal, userId);
            var key = (day.ToDateTime(TimeOnly.MinValue), tag);
            if (on)
            {
                entries.Add(key);
            }
            else
            {
                entries.Remove(key);
            }
        }

        return Task.CompletedTask;
    }
}
