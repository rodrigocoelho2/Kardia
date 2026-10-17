using Kardia.Library.Services;
using Kardia.WebApi.Features.Profiles.Models.DTOs;

namespace Kardia.WebApi.Features.Profiles.Services;

/// <summary>
/// Your profile: date of birth, sex, height, weight, maximum heart rate and sleep need.
/// </summary>
internal interface IProfileService
{
    /// <summary>Gets the profile, with the values Kardia uses.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The profile.</returns>
    Task<ServiceApiResponse<ProfileDTO>> GetAsync(string userId);

    /// <summary>Saves the profile.</summary>
    /// <param name="item">The profile.</param>
    /// <returns>The saved profile, or validation problems.</returns>
    Task<ServiceApiResponse<ProfileDTO>> SaveAsync(ProfileSaveRequest item);
}
