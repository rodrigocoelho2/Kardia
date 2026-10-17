using Kardia.Library.Enums;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Syncs.Models.Entities;
using Kardia.WebApi.Features.Syncs.Repositories;

namespace Kardia.WebApi.Features.Days.Services;

/// <summary>
/// Works out each day's cardiovascular load (dbo.DayLoad) from its heart-rate minutes, so the screens never have to
/// read a whole day of heart rate to show strain. Runs after every sync for the days it touched.
/// </summary>
internal interface IDayLoadService
{
    /// <summary>
    /// Recomputes the load of some days.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="days">The days.</param>
    /// <returns>The loads saved.</returns>
    Task<List<DayLoadRow>> RecomputeAsync(Guid userId, IEnumerable<DateOnly> days);
}

/// <summary>
/// <see cref="IDayLoadService"/> over the health data.
/// </summary>
/// <param name="healthDataRepository">Health data.</param>
/// <param name="profileRepository">Profiles (maximum heart rate, sex).</param>
/// <param name="clock">The app's clock.</param>
internal sealed class DayLoadService(IHealthDataRepository healthDataRepository, IProfileRepository profileRepository, IAppClock clock) : IDayLoadService
{
    private readonly IHealthDataRepository _healthDataRepository = healthDataRepository;
    private readonly IProfileRepository _profileRepository = profileRepository;
    private readonly IAppClock _clock = clock;

    /// <inheritdoc />
    public async Task<List<DayLoadRow>> RecomputeAsync(Guid userId, IEnumerable<DateOnly> days)
    {
        var list = days.Distinct().Order().ToList();
        if (list.Count == 0)
        {
            return [];
        }

        var facts = ProfileFacts.From(await _profileRepository.GetAsync(userId), _clock.Today);
        var metrics = await _healthDataRepository.MetricsListAsync(userId, list[0].AddDays(-Baseline.Days), list[^1]);
        var resting = metrics.Where(static row => row.Metric == (int)HealthMetric.RestingHeartRate)
            .ToDictionary(static row => DateOnly.FromDateTime(row.DayDate), static row => (double)row.Value);

        var loads = new List<DayLoadRow>();
        foreach (var day in list)
        {
            var samples = await _healthDataRepository.HeartRatesListAsync(userId, day.ToDateTime(TimeOnly.MinValue), day.AddDays(1).ToDateTime(TimeOnly.MinValue));
            if (samples.Count == 0)
            {
                continue;
            }

            var rest = resting.TryGetValue(day, out var value) ? value
                : resting.Where(pair => pair.Key < day && pair.Key >= day.AddDays(-Baseline.Days)).Select(static pair => pair.Value).DefaultIfEmpty(60).Average();
            var load = StrainScore.Load(samples, rest, facts.MaxHeartRate, facts.Sex);
            loads.Add(new DayLoadRow
            {
                DayDate = day.ToDateTime(TimeOnly.MinValue),
                Trimp = (decimal)load.Trimp,
                ZoneSeconds = string.Join(',', load.ZoneSeconds),
                MaxBpm = load.MaxBpm is { } max ? (short)max : null,
                AvgBpm = load.AvgBpm is { } avg ? (short)avg : null,
                Minutes = (short)Math.Min(short.MaxValue, load.Minutes),
            });
        }

        await _healthDataRepository.LoadsSaveAsync(userId, loads);
        DataVersion.Bump(userId);
        return loads;
    }
}
