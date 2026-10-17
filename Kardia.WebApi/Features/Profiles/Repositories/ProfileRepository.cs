using Kardia.WebApi.Extensions.Data;
using Kardia.WebApi.Features.Profiles.Models.Entities;

namespace Kardia.WebApi.Features.Profiles.Repositories;

/// <summary>
/// The profiles in SQL Server (stored procedures in Kardia.Database/StoredProcedures/Profiles).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class ProfileRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IProfileRepository
{
    /// <inheritdoc />
    public Task<UserProfile?> GetAsync(Guid userId) => QuerySingleOrDefaultAsync<UserProfile>("[dbo].[UserProfileGet]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task SaveAsync(Guid userId, UserProfile profile) => ExecuteAsync("[dbo].[UserProfileSave]", new
    {
        UserUniqueID = userId,
        profile.BirthDate,
        profile.Sex,
        profile.HeightCm,
        profile.WeightKg,
        profile.MaxHeartRate,
        profile.SleepNeedMinutes,
    });
}
