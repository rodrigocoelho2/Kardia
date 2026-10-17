using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Plans.Models.DTOs;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Settings.Models.Entities;
using Kardia.WebApi.Features.Settings.Repositories;
using Kardia.WebApi.Features.Users.Repositories;

namespace Kardia.WebApi.Features.Plans.Services;

/// <summary>The weekly plan.</summary>
internal interface IPlanService
{
    /// <summary>Gets this week's plan.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The plan.</returns>
    Task<ServiceApiResponse<PlanDTO>> GetAsync(string userId);
}

/// <summary>The weekly plan from the scored days (<see cref="WeeklyPlan"/>).</summary>
/// <param name="userRepository">Accounts.</param>
/// <param name="profileRepository">Profiles.</param>
/// <param name="settingRepository">Targets.</param>
/// <param name="dayScoreReader">Scores days.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="clock">The app's clock.</param>
internal sealed class PlanService(
    IUserRepository userRepository,
    IProfileRepository profileRepository,
    ISettingRepository settingRepository,
    IDayScoreReader dayScoreReader,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock) : IPlanService
{
    /// <inheritdoc />
    public async Task<ServiceApiResponse<PlanDTO>> GetAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id) || await userRepository.GetAsync(id) is null)
        {
            return ServiceApiResponse<PlanDTO>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        var today = clock.Today;
        var facts = ProfileFacts.From(await profileRepository.GetAsync(id), today);
        var setting = await settingRepository.GetAsync(id) ?? UserSetting.Defaults;
        var from = WeeklyPlan.Monday(today).AddDays(-7 - WeeklyPlan.HistoryDays);
        var scores = await dayScoreReader.ScoresAsync(id, facts, from, today);
        return ServiceApiResponse<PlanDTO>.Success(WeeklyPlan.Build(scores, setting, today));
    }
}
