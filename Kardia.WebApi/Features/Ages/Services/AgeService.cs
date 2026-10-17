using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Ages.Models.DTOs;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Users.Repositories;

namespace Kardia.WebApi.Features.Ages.Services;

/// <summary>
/// Biological age.
/// </summary>
internal interface IAgeService
{
    /// <summary>Gets your biological age, its markers, pace and the last six months.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The age (not ready without a date of birth or with too few days of data).</returns>
    Task<ServiceApiResponse<AgeDTO>> GetAsync(string userId);
}

/// <summary>
/// Works out <see cref="BiologicalAge"/> over the last 30 days, three months earlier (for the pace) and at the end of each
/// of the last six months (for the chart).
/// </summary>
/// <param name="userRepository">Accounts.</param>
/// <param name="profileRepository">Profiles.</param>
/// <param name="dayScoreReader">Day scores.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="clock">The app's clock.</param>
internal sealed class AgeService(
    IUserRepository userRepository,
    IProfileRepository profileRepository,
    IDayScoreReader dayScoreReader,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock) : IAgeService
{
    private const int Months = 6;

    private readonly IUserRepository _userRepository = userRepository;
    private readonly IProfileRepository _profileRepository = profileRepository;
    private readonly IDayScoreReader _dayScoreReader = dayScoreReader;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly IAppClock _clock = clock;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<AgeDTO>> GetAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id) || await _userRepository.GetAsync(id) is null)
        {
            return ServiceApiResponse<AgeDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        var today = _clock.Today;
        var profile = await _profileRepository.GetAsync(id);
        var facts = ProfileFacts.From(profile, today);
        var first = today.AddDays(-(Months * 30 + BiologicalAge.WindowDays));
        var scores = await _dayScoreReader.ScoresAsync(id, facts, first, today);

        var latestWindow = Window(scores, today);
        var dto = new AgeDTO
        {
            HasBirthDate = profile?.BirthDate is not null,
            DaysWithData = latestWindow.Count(static day => day.HasData),
            DaysNeeded = BiologicalAge.MinimumDays,
        };
        if (profile?.BirthDate is not { } birthDate)
        {
            return ServiceApiResponse<AgeDTO>.Success(dto);
        }

        var birth = DateOnly.FromDateTime(birthDate);
        var latestVo2 = scores.Select(static day => day.Value(HealthMetric.Vo2Max)).LastOrDefault(static value => value is not null);

        AgeResult? At(DateOnly day) =>
            BiologicalAge.Calculate(ProfileFacts.AgeOn(birth, day), facts.Sex, Window(scores, day), latestVo2, facts.HeightCm, facts.WeightKg);

        var now = At(today);
        if (now is null)
        {
            return ServiceApiResponse<AgeDTO>.Success(dto);
        }

        var earlier = At(today.AddDays(-BiologicalAge.PaceDays));
        dto.Ready = true;
        dto.BiologicalAge = now.Age;
        dto.ChronologicalAge = now.ChronologicalAge;
        dto.Difference = now.Difference;
        dto.Pace = earlier is null ? null : BiologicalAge.Pace(now, earlier);
        dto.Factors = now.Factors.Select(static factor => new AgeFactorDTO
        {
            Key = factor.Key,
            Name = factor.Name,
            Value = factor.Value,
            Unit = factor.Unit,
            Typical = factor.Typical,
            Years = factor.Years,
        }).ToList();
        dto.History = Enumerable.Range(0, Months + 1).Reverse()
            .Select(months => today.AddDays(-30 * months))
            .Select(day => (Day: day, Result: At(day)))
            .Where(static point => point.Result is not null)
            .Select(static point => new AgePointDTO { Date = point.Day, BiologicalAge = point.Result!.Age, ChronologicalAge = point.Result.ChronologicalAge })
            .ToList();
        return ServiceApiResponse<AgeDTO>.Success(dto);
    }

    private static List<DayScore> Window(List<DayScore> scores, DateOnly end) =>
        scores.Where(day => day.Date <= end && day.Date > end.AddDays(-BiologicalAge.WindowDays)).ToList();
}
