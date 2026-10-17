using Kardia.WebApi.Extensions.Data;
using Kardia.WebApi.Features.Profiles.Models.Entities;

namespace Kardia.WebApi.Features.Profiles.Repositories;

/// <summary>
/// The profiles in demo mode (<see cref="MemoryStore"/>).
/// </summary>
/// <param name="store">The in-memory tables.</param>
internal sealed class MemoryProfileRepository(MemoryStore store) : IProfileRepository
{
    /// <inheritdoc />
    public Task<UserProfile?> GetAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Profiles.TryGetValue(userId, out var profile) ? Copy(profile) : null);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(Guid userId, UserProfile profile)
    {
        lock (store.Gate)
        {
            store.Profiles[userId] = Copy(profile);
        }

        return Task.CompletedTask;
    }

    private static UserProfile Copy(UserProfile profile) => new()
    {
        BirthDate = profile.BirthDate,
        Sex = profile.Sex,
        HeightCm = profile.HeightCm,
        WeightKg = profile.WeightKg,
        MaxHeartRate = profile.MaxHeartRate,
        SleepNeedMinutes = profile.SleepNeedMinutes,
    };
}
