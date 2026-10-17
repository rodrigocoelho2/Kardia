using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Features.Settings.Models.DTOs;
using Kardia.WebApi.Features.Settings.Models.Entities;
using Kardia.WebApi.Features.Settings.Repositories;
using Kardia.WebApi.Features.Users.Repositories;

namespace Kardia.WebApi.Features.Settings.Services;

/// <summary>Goals and notification choices.</summary>
internal interface ISettingService
{
    /// <summary>Gets an account's settings (the defaults until first saved).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The settings.</returns>
    Task<ServiceApiResponse<SettingDTO>> GetAsync(string userId);

    /// <summary>Saves an account's settings.</summary>
    /// <param name="item">The settings.</param>
    /// <returns>The saved settings.</returns>
    Task<ServiceApiResponse<SettingDTO>> SaveAsync(SettingDTO item);
}

/// <summary>Goals and notification choices, validated.</summary>
/// <param name="settingRepository">Settings.</param>
/// <param name="userRepository">Accounts.</param>
/// <param name="globalizationHelper">Messages.</param>
internal sealed class SettingService(ISettingRepository settingRepository, IUserRepository userRepository, IGlobalizationHelper globalizationHelper) : ISettingService
{
    /// <inheritdoc />
    public async Task<ServiceApiResponse<SettingDTO>> GetAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id) || await userRepository.GetAsync(id) is null)
        {
            return ServiceApiResponse<SettingDTO>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        return ServiceApiResponse<SettingDTO>.Success(ToDTO(id, await settingRepository.GetAsync(id) ?? UserSetting.Defaults));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SettingDTO>> SaveAsync(SettingDTO item)
    {
        if (!Guid.TryParse(item.UserId, out var id) || await userRepository.GetAsync(id) is null)
        {
            return ServiceApiResponse<SettingDTO>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.UserId ?? string.Empty));
        }

        var errors = new List<ApiValidationError>();
        if (item.SleepGoalPercent is not (100 or 85 or 70))
        {
            errors.Add(await globalizationHelper.ValidationErrorAsync("SleepGoalPercent", nameof(TranslationKeys.OutOfRange), 70, 100));
        }

        await RangeAsync(errors, "ActiveMinutesWeek", item.ActiveMinutesWeek, 0, 2000);
        await RangeAsync(errors, "StepsDay", item.StepsDay, 0, 50000);
        await RangeAsync(errors, "SleepNightsWeek", item.SleepNightsWeek, 0, 7);
        await RangeAsync(errors, "BedtimeNightsWeek", item.BedtimeNightsWeek, 0, 7);
        await RangeAsync(errors, "BedtimeLeadMinutes", item.BedtimeLeadMinutes, 0, 180);
        if (errors.Count > 0)
        {
            return ServiceApiResponse<SettingDTO>.ValidationProblems(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems))).AddErrorRange(errors);
        }

        var setting = new UserSetting
        {
            SleepGoalPercent = (byte)item.SleepGoalPercent,
            ActiveMinutesWeek = (short)item.ActiveMinutesWeek,
            StepsDay = item.StepsDay,
            SleepNightsWeek = (byte)item.SleepNightsWeek,
            BedtimeNightsWeek = (byte)item.BedtimeNightsWeek,
            NotifyMorning = item.NotifyMorning,
            NotifyBedtime = item.NotifyBedtime,
            BedtimeLeadMinutes = (short)item.BedtimeLeadMinutes,
            NotifyHealth = item.NotifyHealth,
            NotifyStrain = item.NotifyStrain,
            NotifyReports = item.NotifyReports,
        };
        await settingRepository.SaveAsync(id, setting);
        return ServiceApiResponse<SettingDTO>.Success(ToDTO(id, setting));
    }

    /// <summary>Maps settings to the DTO.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="s">The settings.</param>
    /// <returns>The DTO.</returns>
    internal static SettingDTO ToDTO(Guid userId, UserSetting s) => new()
    {
        UserId = userId.ToString("N"),
        SleepGoalPercent = s.SleepGoalPercent,
        ActiveMinutesWeek = s.ActiveMinutesWeek,
        StepsDay = s.StepsDay,
        SleepNightsWeek = s.SleepNightsWeek,
        BedtimeNightsWeek = s.BedtimeNightsWeek,
        NotifyMorning = s.NotifyMorning,
        NotifyBedtime = s.NotifyBedtime,
        BedtimeLeadMinutes = s.BedtimeLeadMinutes,
        NotifyHealth = s.NotifyHealth,
        NotifyStrain = s.NotifyStrain,
        NotifyReports = s.NotifyReports,
    };

    private async Task RangeAsync(List<ApiValidationError> errors, string field, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            errors.Add(await globalizationHelper.ValidationErrorAsync(field, nameof(TranslationKeys.OutOfRange), min, max));
        }
    }
}
