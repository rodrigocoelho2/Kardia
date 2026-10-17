using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Journals.Repositories;
using Kardia.WebApi.Features.Syncs.Repositories;

namespace Kardia.WebApi.Features.Demo.Services;

/// <summary>
/// Fills an account with sample data in demo mode; does nothing otherwise.
/// </summary>
internal interface IDemoSeeder
{
    /// <summary>
    /// Fills an account with <see cref="DemoData.Days"/> days of generated Apple Watch data.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>A task.</returns>
    Task SeedAsync(Guid userId);

    /// <summary>
    /// Regenerates yesterday and today so a demo left running keeps a live-looking day (heart rate up to now,
    /// tonight's sleep once a new day starts). Journal entries are left as the visitor set them.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>A task.</returns>
    Task RefreshAsync(Guid userId);
}

/// <summary>
/// Outside demo mode: new accounts start empty and wait for their first sync.
/// </summary>
internal sealed class NoDemoSeeder : IDemoSeeder
{
    /// <inheritdoc />
    public Task SeedAsync(Guid userId) => Task.CompletedTask;

    /// <inheritdoc />
    public Task RefreshAsync(Guid userId) => Task.CompletedTask;
}

/// <summary>
/// Demo mode: saves <see cref="DemoData"/> through the same repositories a sync uses, then works out the day loads.
/// </summary>
/// <param name="healthDataRepository">Health data.</param>
/// <param name="syncKeyRepository">Sync keys (the demo looks like a watch that synced a few minutes ago).</param>
/// <param name="journalRepository">Journal.</param>
/// <param name="dayLoadService">Day loads.</param>
/// <param name="clock">The app's clock.</param>
internal sealed class DemoSeeder(
    IHealthDataRepository healthDataRepository,
    ISyncKeyRepository syncKeyRepository,
    IJournalRepository journalRepository,
    IDayLoadService dayLoadService,
    IAppClock clock) : IDemoSeeder
{
    /// <inheritdoc />
    public async Task SeedAsync(Guid userId)
    {
        var today = clock.Today;
        var data = DemoData.Generate(today, clock.Now, 20261016);
        await healthDataRepository.MetricsSaveAsync(userId, data.Metrics);
        await healthDataRepository.HeartRatesSaveAsync(userId, data.HeartRates);
        await healthDataRepository.SleepsSaveAsync(userId, data.Sleeps);
        await healthDataRepository.WorkoutsSaveAsync(userId, data.Workouts);
        foreach (var (day, tag) in data.Journal)
        {
            await journalRepository.SetAsync(userId, day, tag.ToString(), true);
        }

        await dayLoadService.RecomputeAsync(userId, Enumerable.Range(0, DemoData.Days).Select(back => today.AddDays(-back)));
        DataVersion.Bump(userId);
        await MarkSyncedAsync(userId, true);
    }

    /// <inheritdoc />
    public async Task RefreshAsync(Guid userId)
    {
        var today = clock.Today;
        var from = today.AddDays(-1);
        var data = DemoData.Generate(today, clock.Now, 20261016);
        await healthDataRepository.MetricsSaveAsync(userId, [.. data.Metrics.Where(row => DateOnly.FromDateTime(row.DayDate) >= from)]);
        await healthDataRepository.HeartRatesSaveAsync(userId, [.. data.HeartRates.Where(row => DateOnly.FromDateTime(row.MinuteAt) >= from)]);
        await healthDataRepository.SleepsSaveAsync(userId, [.. data.Sleeps.Where(row => DateOnly.FromDateTime(row.NightDate) >= from)]);
        await healthDataRepository.WorkoutsSaveAsync(userId, [.. data.Workouts.Where(row => DateOnly.FromDateTime(row.StartedAt) >= from)]);
        await dayLoadService.RecomputeAsync(userId, [from, today]);
        DataVersion.Bump(userId);
        await MarkSyncedAsync(userId, false);
    }

    private async Task MarkSyncedAsync(Guid userId, bool create)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(userId.ToByteArray());
        if (create || await syncKeyRepository.GetAsync(userId) is null)
        {
            await syncKeyRepository.SaveAsync(userId, hash, "…demo");
        }

        if (await syncKeyRepository.UseAsync(hash) is not null)
        {
            await syncKeyRepository.MarkAsync(userId, "Demo data · refreshed every 15 minutes");
        }
    }
}
