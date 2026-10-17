using System.Collections.Concurrent;

namespace Kardia.WebApi.Features.Days.Services;

/// <summary>
/// A counter per account that moves on every write of its health data (a sync, the day loads, the demo refresh).
/// Scored days are cached under it (<see cref="DayScoreReader"/>), so moving it is all it takes to stop serving them.
/// </summary>
internal static class DataVersion
{
    private static readonly ConcurrentDictionary<Guid, long> Versions = new();

    /// <summary>The current version of an account's data.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The version.</returns>
    public static long Of(Guid userId) => Versions.GetValueOrDefault(userId);

    /// <summary>Marks an account's data as changed.</summary>
    /// <param name="userId">The account.</param>
    public static void Bump(Guid userId) => Versions.AddOrUpdate(userId, 1, static (_, version) => version + 1);
}
