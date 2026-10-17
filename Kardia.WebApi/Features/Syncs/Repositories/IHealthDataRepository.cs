using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Syncs.Repositories;

/// <summary>
/// What Apple Health sent, kept per account: daily values, heart rate per minute, nights, workouts, and each day's
/// cardiovascular load. Syncs write it; Days, Ages and Journals read it.
/// </summary>
internal interface IHealthDataRepository
{
    /// <summary>Adds or replaces daily values (summed metrics keep the larger total of the day).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The values.</param>
    /// <returns>A task.</returns>
    Task MetricsSaveAsync(Guid userId, IReadOnlyList<DailyMetricRow> rows);

    /// <summary>Lists the daily values of a range of days.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day (inclusive).</param>
    /// <returns>The values.</returns>
    Task<List<DailyMetricRow>> MetricsListAsync(Guid userId, DateOnly from, DateOnly to);

    /// <summary>Adds or replaces heart-rate minutes.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The minutes.</param>
    /// <returns>A task.</returns>
    Task HeartRatesSaveAsync(Guid userId, IReadOnlyList<HeartRateRow> rows);

    /// <summary>Saves timed readings (HRV, respiratory rate, blood oxygen); one sent again replaces the old one.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The readings.</param>
    /// <returns>A task.</returns>
    Task SamplesSaveAsync(Guid userId, IReadOnlyList<MetricSampleRow> rows);

    /// <summary>Lists timed readings.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">From (inclusive), local time.</param>
    /// <param name="to">To (exclusive), local time.</param>
    /// <returns>The readings, in time order.</returns>
    Task<List<MetricSampleRow>> SamplesListAsync(Guid userId, DateTime from, DateTime to);

    /// <summary>Lists the heart-rate minutes of a time range.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">Start (inclusive), local time.</param>
    /// <param name="to">End (exclusive), local time.</param>
    /// <returns>The minutes, in order.</returns>
    Task<List<HeartRateRow>> HeartRatesListAsync(Guid userId, DateTime from, DateTime to);

    /// <summary>Adds or replaces nights.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The nights.</param>
    /// <returns>A task.</returns>
    Task SleepsSaveAsync(Guid userId, IReadOnlyList<SleepNightRow> rows);

    /// <summary>Lists the nights that ended in a range of days.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">First wake-up date.</param>
    /// <param name="to">Last wake-up date (inclusive).</param>
    /// <returns>The nights, in order.</returns>
    Task<List<SleepNightRow>> SleepsListAsync(Guid userId, DateOnly from, DateOnly to);

    /// <summary>Adds or replaces workouts (the same start is the same workout).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The workouts.</param>
    /// <returns>A task.</returns>
    Task WorkoutsSaveAsync(Guid userId, IReadOnlyList<WorkoutRow> rows);

    /// <summary>Saves the heart-rate recovery of workouts (<see cref="WorkoutRow.Hrr1"/>, <see cref="WorkoutRow.Hrr2"/>).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The workouts (by start) with their recovery.</param>
    /// <returns>A task.</returns>
    Task WorkoutsRecoverySaveAsync(Guid userId, IReadOnlyList<WorkoutRow> rows);

    /// <summary>Lists the workouts that started in a range of days.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day (inclusive).</param>
    /// <returns>The workouts, in order.</returns>
    Task<List<WorkoutRow>> WorkoutsListAsync(Guid userId, DateOnly from, DateOnly to);

    /// <summary>Adds or replaces day loads.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The loads.</param>
    /// <returns>A task.</returns>
    Task LoadsSaveAsync(Guid userId, IReadOnlyList<DayLoadRow> rows);

    /// <summary>Lists the day loads of a range of days.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day (inclusive).</param>
    /// <returns>The loads, in order.</returns>
    Task<List<DayLoadRow>> LoadsListAsync(Guid userId, DateOnly from, DateOnly to);
}

/// <summary>
/// The personal sync keys (dbo.SyncKey).
/// </summary>
internal interface ISyncKeyRepository
{
    /// <summary>Gets an account's key (hash and hint only).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The key, or null.</returns>
    Task<SyncKeyRow?> GetAsync(Guid userId);

    /// <summary>Saves a new key (replacing the old one).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="keyHash">The SHA-256 of the key.</param>
    /// <param name="keyHint">Its last characters.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(Guid userId, byte[] keyHash, string keyHint);

    /// <summary>Deletes an account's key.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(Guid userId);

    /// <summary>Finds whose key this is and records the use.</summary>
    /// <param name="keyHash">The SHA-256 of the key sent.</param>
    /// <returns>The account, or null for an unknown key.</returns>
    Task<Guid?> UseAsync(byte[] keyHash);

    /// <summary>Records what the last sync brought.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="summary">The summary, in words.</param>
    /// <returns>A task.</returns>
    Task MarkAsync(Guid userId, string summary);
}
