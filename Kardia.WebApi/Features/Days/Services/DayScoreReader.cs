using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Syncs.Repositories;

namespace Kardia.WebApi.Features.Days.Services;

/// <summary>
/// Loads the rows for a range of days (with the history baselines need) and scores them. Shared by Days, Ages and
/// Journals so every screen agrees on the numbers.
/// </summary>
internal interface IDayScoreReader
{
    /// <summary>
    /// Scores a range of days.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="facts">The profile.</param>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day.</param>
    /// <returns>One score per day, in order.</returns>
    Task<List<DayScore>> ScoresAsync(Guid userId, ProfileFacts facts, DateOnly from, DateOnly to);
}

/// <summary>
/// <see cref="IDayScoreReader"/> over the health data.
/// </summary>
/// <param name="healthDataRepository">Health data.</param>
internal sealed class DayScoreReader(IHealthDataRepository healthDataRepository) : IDayScoreReader
{
    private readonly IHealthDataRepository _healthDataRepository = healthDataRepository;

    /// <inheritdoc />
    public async Task<List<DayScore>> ScoresAsync(Guid userId, ProfileFacts facts, DateOnly from, DateOnly to)
    {
        var start = from.AddDays(-DayScoreBuilder.History);
        var metrics = await _healthDataRepository.MetricsListAsync(userId, start, to);
        var nights = await _healthDataRepository.SleepsListAsync(userId, start, to);
        var loads = await _healthDataRepository.LoadsListAsync(userId, start, to);
        return DayScoreBuilder.Build(from, to, facts, metrics, nights, loads);
    }
}
