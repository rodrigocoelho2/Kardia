using Kardia.WebApi.Extensions.Data;
using Kardia.WebApi.Features.Settings.Models.Entities;

namespace Kardia.WebApi.Features.Settings.Repositories;

/// <summary>Reads and saves goals and notification choices.</summary>
internal interface ISettingRepository
{
    /// <summary>Gets an account's settings (null until first saved).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The settings.</returns>
    Task<UserSetting?> GetAsync(Guid userId);

    /// <summary>Creates or replaces an account's settings.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="setting">The settings.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(Guid userId, UserSetting setting);
}

/// <summary>The settings in SQL Server (stored procedures in Kardia.Database/StoredProcedures/Settings).</summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class SettingRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), ISettingRepository
{
    /// <inheritdoc />
    public Task<UserSetting?> GetAsync(Guid userId) => QuerySingleOrDefaultAsync<UserSetting>("[dbo].[UserSettingGet]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task SaveAsync(Guid userId, UserSetting setting) => ExecuteAsync("[dbo].[UserSettingSave]", new
    {
        UserUniqueID = userId,
        setting.SleepGoalPercent,
        setting.ActiveMinutesWeek,
        setting.StepsDay,
        setting.SleepNightsWeek,
        setting.BedtimeNightsWeek,
        setting.NotifyMorning,
        setting.NotifyBedtime,
        setting.BedtimeLeadMinutes,
        setting.NotifyHealth,
        setting.NotifyStrain,
        setting.NotifyReports,
    });
}

/// <summary>The settings in demo mode (<see cref="MemoryStore"/>).</summary>
/// <param name="store">The in-memory tables.</param>
internal sealed class MemorySettingRepository(MemoryStore store) : ISettingRepository
{
    /// <inheritdoc />
    public Task<UserSetting?> GetAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Settings.TryGetValue(userId, out var setting) ? Copy(setting) : null);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(Guid userId, UserSetting setting)
    {
        lock (store.Gate)
        {
            store.Settings[userId] = Copy(setting);
        }

        return Task.CompletedTask;
    }

    private static UserSetting Copy(UserSetting s) => new()
    {
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
}
