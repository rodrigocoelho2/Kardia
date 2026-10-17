using Microsoft.Extensions.Caching.Memory;
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
/// <see cref="IDayScoreReader"/> over the health data. Scored ranges are kept for a few minutes under the account's
/// <see cref="DataVersion"/> and profile, so going from screen to screen doesn't score the same months again; any write
/// of the account's data (a sync) moves the version and the next screen scores afresh.
/// </summary>
/// <param name="healthDataRepository">Health data.</param>
/// <param name="cache">Where scored ranges are kept (none in tests).</param>
internal sealed class DayScoreReader(IHealthDataRepository healthDataRepository, IMemoryCache? cache = null) : IDayScoreReader
{
    /// <summary>How long a scored range is kept.</summary>
    public static readonly TimeSpan KeepFor = TimeSpan.FromMinutes(5);

    private readonly IHealthDataRepository _healthDataRepository = healthDataRepository;

    /// <inheritdoc />
    public async Task<List<DayScore>> ScoresAsync(Guid userId, ProfileFacts facts, DateOnly from, DateOnly to)
    {
        if (cache is null)
        {
            return await ScoreAsync(userId, facts, from, to);
        }

        var key = (nameof(DayScoreReader), userId, DataVersion.Of(userId), facts, from, to);
        if (!cache.TryGetValue(key, out List<DayScore>? scores) || scores is null)
        {
            scores = await ScoreAsync(userId, facts, from, to);
            cache.Set(key, scores, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = KeepFor, Size = scores.Count });
        }

        // Callers get their own list (the scores themselves are immutable).
        return [.. scores];
    }

    private async Task<List<DayScore>> ScoreAsync(Guid userId, ProfileFacts facts, DateOnly from, DateOnly to)
    {
        var start = from.AddDays(-DayScoreBuilder.History);
        var metrics = await _healthDataRepository.MetricsListAsync(userId, start, to);
        var nights = await _healthDataRepository.SleepsListAsync(userId, start, to);
        var loads = await _healthDataRepository.LoadsListAsync(userId, start, to);
        return DayScoreBuilder.Build(from, to, facts, metrics, nights, loads);
    }
}
