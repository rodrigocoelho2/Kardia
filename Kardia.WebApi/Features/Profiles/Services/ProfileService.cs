using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Profiles.Models.DTOs;
using Kardia.WebApi.Features.Profiles.Models.Entities;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Users.Repositories;

namespace Kardia.WebApi.Features.Profiles.Services;

/// <summary>
/// Your profile. Every field is optional; empty ones fall back to estimates (<see cref="ProfileFacts"/>).
/// </summary>
/// <param name="profileRepository">Profiles.</param>
/// <param name="userRepository">Accounts (the profile's owner must exist).</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="clock">The app's clock.</param>
internal sealed class ProfileService(
    IProfileRepository profileRepository,
    IUserRepository userRepository,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock) : IProfileService
{
    private readonly IProfileRepository _profileRepository = profileRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly IAppClock _clock = clock;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ProfileDTO>> GetAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id) || await _userRepository.GetAsync(id) is null)
        {
            return ServiceApiResponse<ProfileDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        return ServiceApiResponse<ProfileDTO>.Success(ToDTO(id, await _profileRepository.GetAsync(id)));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ProfileDTO>> SaveAsync(ProfileSaveRequest item)
    {
        if (!Guid.TryParse(item.UserId, out var id) || await _userRepository.GetAsync(id) is null)
        {
            return ServiceApiResponse<ProfileDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.UserId ?? string.Empty));
        }

        var errors = new List<ApiValidationError>();
        var today = _clock.Today;
        if (item.BirthDate is { } birth && (birth > today.AddYears(-13) || birth < today.AddYears(-110)))
        {
            errors.Add(await _globalizationHelper.ValidationErrorAsync("BirthDate", nameof(TranslationKeys.OutOfRange), today.AddYears(-110).Year, today.AddYears(-13).Year));
        }

        await RangeAsync(errors, "HeightCm", item.HeightCm, 100, 250);
        await RangeAsync(errors, "WeightKg", item.WeightKg, 30, 300);
        await RangeAsync(errors, "MaxHeartRate", item.MaxHeartRate, 120, 230);
        await RangeAsync(errors, "SleepNeedMinutes", item.SleepNeedMinutes, 360, 600);
        if (errors.Count > 0)
        {
            return ServiceApiResponse<ProfileDTO>.ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems))).AddErrorRange(errors);
        }

        var profile = new UserProfile
        {
            BirthDate = item.BirthDate?.ToDateTime(TimeOnly.MinValue),
            Sex = (byte)(Enum.IsDefined(item.Sex) ? item.Sex : BiologicalSex.Unspecified),
            HeightCm = item.HeightCm is { } height ? Math.Round(height, 1) : null,
            WeightKg = item.WeightKg is { } weight ? Math.Round(weight, 1) : null,
            MaxHeartRate = item.MaxHeartRate is { } max ? (short)max : null,
            SleepNeedMinutes = item.SleepNeedMinutes is { } need ? (short)need : null,
        };
        await _profileRepository.SaveAsync(id, profile);
        return ServiceApiResponse<ProfileDTO>.Success(ToDTO(id, profile));
    }

    private ProfileDTO ToDTO(Guid userId, UserProfile? profile)
    {
        var facts = ProfileFacts.From(profile, _clock.Today);
        return new ProfileDTO
        {
            UserId = userId.ToString("N"),
            BirthDate = profile?.BirthDate is { } birth ? DateOnly.FromDateTime(birth) : null,
            Age = facts.Age is { } age ? (int)Math.Floor(age) : null,
            Sex = facts.Sex,
            HeightCm = profile?.HeightCm,
            WeightKg = profile?.WeightKg,
            MaxHeartRate = profile?.MaxHeartRate,
            MaxHeartRateUsed = facts.MaxHeartRate,
            SleepNeedMinutes = profile?.SleepNeedMinutes,
            SleepNeedMinutesUsed = facts.SleepNeedMinutes,
        };
    }

    private async Task RangeAsync(List<ApiValidationError> errors, string field, decimal? value, decimal min, decimal max)
    {
        if (value is { } number && (number < min || number > max))
        {
            errors.Add(await _globalizationHelper.ValidationErrorAsync(field, nameof(TranslationKeys.OutOfRange), min, max));
        }
    }
}
