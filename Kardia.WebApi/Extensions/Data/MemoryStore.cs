using Kardia.WebApi.Features.Journals.Models.Entities;
using Kardia.WebApi.Features.Profiles.Models.Entities;
using Kardia.WebApi.Features.Syncs.Models.Entities;
using Kardia.WebApi.Features.Users.Models.Entities;

namespace Kardia.WebApi.Extensions.Data;

/// <summary>
/// The tables of demo mode (<see cref="Features.KardiaOptions.DemoMode"/>): the same shapes as the SQL Server tables,
/// kept in memory and lost on restart. The Memory* repositories read and write it under <see cref="Gate"/>.
/// </summary>
internal sealed class MemoryStore
{
    /// <summary>Gets the lock every memory repository takes.</summary>
    public Lock Gate { get; } = new();

    /// <summary>Gets the accounts.</summary>
    public List<UserAccount> Users { get; } = [];

    /// <summary>Gets the profiles, by account.</summary>
    public Dictionary<Guid, UserProfile> Profiles { get; } = [];

    /// <summary>Gets the sync keys, by account.</summary>
    public Dictionary<Guid, SyncKeyRow> SyncKeys { get; } = [];

    /// <summary>Gets the daily values, by account then (day, metric).</summary>
    public Dictionary<Guid, Dictionary<(DateTime Day, int Metric), decimal>> Metrics { get; } = [];

    /// <summary>Gets the heart rate per minute, by account then minute.</summary>
    public Dictionary<Guid, SortedDictionary<DateTime, short>> HeartRates { get; } = [];

    /// <summary>Gets the nights, by account then wake-up date.</summary>
    public Dictionary<Guid, SortedDictionary<DateTime, SleepNightRow>> Sleeps { get; } = [];

    /// <summary>Gets the workouts, by account then start.</summary>
    public Dictionary<Guid, SortedDictionary<DateTime, WorkoutRow>> Workouts { get; } = [];

    /// <summary>Gets the day loads, by account then day.</summary>
    public Dictionary<Guid, SortedDictionary<DateTime, DayLoadRow>> Loads { get; } = [];

    /// <summary>Gets the journal, by account then (day, tag).</summary>
    public Dictionary<Guid, HashSet<(DateTime Day, string Tag)>> Journal { get; } = [];

    /// <summary>
    /// Gets (creating it) the per-account part of a table.
    /// </summary>
    /// <typeparam name="T">The per-account collection.</typeparam>
    /// <param name="table">The table.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The account's rows.</returns>
    public static T For<T>(Dictionary<Guid, T> table, Guid userId)
        where T : new()
    {
        if (!table.TryGetValue(userId, out var rows))
        {
            rows = new T();
            table[userId] = rows;
        }

        return rows;
    }
}
