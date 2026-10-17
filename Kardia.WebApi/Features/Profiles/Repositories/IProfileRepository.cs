using Kardia.WebApi.Features.Profiles.Models.Entities;

namespace Kardia.WebApi.Features.Profiles.Repositories;

/// <summary>
/// The profiles (dbo.UserProfile).
/// </summary>
internal interface IProfileRepository
{
    /// <summary>Gets a profile.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The profile, or null when never saved.</returns>
    Task<UserProfile?> GetAsync(Guid userId);

    /// <summary>Creates or replaces a profile.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="profile">The profile.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(Guid userId, UserProfile profile);
}
