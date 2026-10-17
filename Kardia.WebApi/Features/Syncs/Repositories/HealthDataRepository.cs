using Kardia.Library.Enums;
using Kardia.WebApi.Extensions.Data;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Syncs.Repositories;

/// <summary>
/// Apple Health data in SQL Server (stored procedures in Kardia.Database/StoredProcedures/Health). Lists of rows
/// go to the procedures as one JSON parameter each.
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class HealthDataRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IHealthDataRepository
{
    /// <summary>The metrics whose daily value is a total (the larger one is kept).</summary>
    private static readonly string SummedMetrics = string.Join(',', new[] { HealthMetric.Steps, HealthMetric.ActiveEnergy, HealthMetric.BasalEnergy, HealthMetric.ExerciseMinutes }.Select(static metric => (int)metric));

    /// <inheritdoc />
    public Task MetricsSaveAsync(Guid userId, IReadOnlyList<DailyMetricRow> rows) => rows.Count == 0 ? Task.CompletedTask
        : ExecuteAsync("[dbo].[DailyMetricSave]", new
        {
            UserUniqueID = userId,
            Rows = Json(rows.Select(static row => new { d = row.DayDate.ToString("yyyy-MM-dd"), m = row.Metric, v = row.Value })),
            SummedMetrics,
        });

    /// <inheritdoc />
    public Task<List<DailyMetricRow>> MetricsListAsync(Guid userId, DateOnly from, DateOnly to) =>
        QueryAsync<DailyMetricRow>("[dbo].[DailyMetricList]", new { UserUniqueID = userId, From = Date(from), To = Date(to) });

    /// <inheritdoc />
    public async Task HeartRatesSaveAsync(Guid userId, IReadOnlyList<HeartRateRow> rows)
    {
        // A month is ~43 000 minutes: save a day or so at a time, so SQL Server never holds one huge transaction and
        // the NAS stays responsive while a big export goes in.
        foreach (var chunk in rows.Chunk(2_000))
        {
            await ExecuteAsync("[dbo].[HeartRateMinuteSave]", new
            {
                UserUniqueID = userId,
                Rows = Json(chunk.Select(static row => new { t = row.MinuteAt.ToString("yyyy-MM-ddTHH:mm:00"), b = row.Bpm })),
            });
        }
    }

    /// <inheritdoc />
    public async Task SamplesSaveAsync(Guid userId, IReadOnlyList<MetricSampleRow> rows)
    {
        foreach (var chunk in rows.Chunk(2_000))
        {
            await ExecuteAsync("[dbo].[MetricSampleSave]", new
            {
                UserUniqueID = userId,
                Rows = Json(chunk.Select(static row => new { m = row.Metric, t = row.SampleAt.ToString("yyyy-MM-ddTHH:mm:ss"), v = row.Value })),
            });
        }
    }

    /// <inheritdoc />
    public Task<List<MetricSampleRow>> SamplesListAsync(Guid userId, DateTime from, DateTime to) =>
        QueryAsync<MetricSampleRow>("[dbo].[MetricSampleList]", new { UserUniqueID = userId, From = from, To = to });

    /// <inheritdoc />
    public Task<List<HeartRateRow>> HeartRatesListAsync(Guid userId, DateTime from, DateTime to) =>
        QueryAsync<HeartRateRow>("[dbo].[HeartRateMinuteList]", new { UserUniqueID = userId, From = from, To = to });

    /// <inheritdoc />
    public Task SleepsSaveAsync(Guid userId, IReadOnlyList<SleepNightRow> rows) => rows.Count == 0 ? Task.CompletedTask
        : ExecuteAsync("[dbo].[SleepNightSave]", new
        {
            UserUniqueID = userId,
            Rows = Json(rows.Select(static row => new
            {
                n = row.NightDate.ToString("yyyy-MM-dd"),
                s = row.BedStart.ToString("yyyy-MM-ddTHH:mm:ss"),
                e = row.BedEnd.ToString("yyyy-MM-ddTHH:mm:ss"),
                aw = row.AwakeMinutes,
                re = row.RemMinutes,
                co = row.CoreMinutes,
                de = row.DeepMinutes,
                st = row.Stages,
            })),
        });

    /// <inheritdoc />
    public Task<List<SleepNightRow>> SleepsListAsync(Guid userId, DateOnly from, DateOnly to) =>
        QueryAsync<SleepNightRow>("[dbo].[SleepNightList]", new { UserUniqueID = userId, From = Date(from), To = Date(to) });

    /// <inheritdoc />
    public Task WorkoutsSaveAsync(Guid userId, IReadOnlyList<WorkoutRow> rows) => rows.Count == 0 ? Task.CompletedTask
        : ExecuteAsync("[dbo].[WorkoutSave]", new
        {
            UserUniqueID = userId,
            Rows = Json(rows.Select(static row => new
            {
                s = row.StartedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                e = row.EndedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                k = row.Kind,
                kc = row.ActiveKcal,
                km = row.DistanceKm,
                av = row.AvgBpm,
                mx = row.MaxBpm,
            })),
        });

    /// <inheritdoc />
    public Task<List<WorkoutRow>> WorkoutsListAsync(Guid userId, DateOnly from, DateOnly to) =>
        QueryAsync<WorkoutRow>("[dbo].[WorkoutList]", new { UserUniqueID = userId, From = Date(from), To = Date(to) });

    /// <inheritdoc />
    public Task LoadsSaveAsync(Guid userId, IReadOnlyList<DayLoadRow> rows) => rows.Count == 0 ? Task.CompletedTask
        : ExecuteAsync("[dbo].[DayLoadSave]", new
        {
            UserUniqueID = userId,
            Rows = Json(rows.Select(static row => new
            {
                d = row.DayDate.ToString("yyyy-MM-dd"),
                t = row.Trimp,
                z = row.ZoneSeconds,
                mx = row.MaxBpm,
                av = row.AvgBpm,
                mi = row.Minutes,
            })),
        });

    /// <inheritdoc />
    public Task<List<DayLoadRow>> LoadsListAsync(Guid userId, DateOnly from, DateOnly to) =>
        QueryAsync<DayLoadRow>("[dbo].[DayLoadList]", new { UserUniqueID = userId, From = Date(from), To = Date(to) });
}

/// <summary>
/// The sync keys in SQL Server (stored procedures in Kardia.Database/StoredProcedures/Syncs).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class SyncKeyRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), ISyncKeyRepository
{
    /// <inheritdoc />
    public Task<SyncKeyRow?> GetAsync(Guid userId) => QuerySingleOrDefaultAsync<SyncKeyRow>("[dbo].[SyncKeyGet]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task SaveAsync(Guid userId, byte[] keyHash, string keyHint) =>
        ExecuteAsync("[dbo].[SyncKeySave]", new { UserUniqueID = userId, KeyHash = keyHash, KeyHint = keyHint });

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId) => ExecuteAsync("[dbo].[SyncKeyDelete]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task<Guid?> UseAsync(byte[] keyHash) => QuerySingleOrDefaultAsync<Guid?>("[dbo].[SyncKeyUse]", new { KeyHash = keyHash });

    /// <inheritdoc />
    public Task MarkAsync(Guid userId, string summary) =>
        ExecuteAsync("[dbo].[SyncKeyMark]", new { UserUniqueID = userId, LastSummary = summary.Length > 200 ? summary[..200] : summary });
}
