using Kardia.Library.Enums;
using Kardia.WebApi.Extensions.Data;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Syncs.Repositories;

/// <summary>
/// Apple Health data in demo mode (<see cref="MemoryStore"/>): same behaviour as the stored procedures.
/// </summary>
/// <param name="store">The in-memory tables.</param>
internal sealed class MemoryHealthDataRepository(MemoryStore store) : IHealthDataRepository
{
    private static readonly HashSet<int> Summed = [(int)HealthMetric.Steps, (int)HealthMetric.ActiveEnergy, (int)HealthMetric.BasalEnergy, (int)HealthMetric.ExerciseMinutes, (int)HealthMetric.Daylight];

    /// <inheritdoc />
    public Task MetricsSaveAsync(Guid userId, IReadOnlyList<DailyMetricRow> rows)
    {
        lock (store.Gate)
        {
            var table = MemoryStore.For(store.Metrics, userId);
            foreach (var row in rows)
            {
                var key = (row.DayDate.Date, row.Metric);
                table[key] = Summed.Contains(row.Metric) && table.TryGetValue(key, out var old) ? Math.Max(old, row.Value) : row.Value;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<DailyMetricRow>> MetricsListAsync(Guid userId, DateOnly from, DateOnly to)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.ToDateTime(TimeOnly.MinValue);
        lock (store.Gate)
        {
            return Task.FromResult(MemoryStore.For(store.Metrics, userId)
                .Where(pair => pair.Key.Day >= start && pair.Key.Day <= end)
                .OrderBy(static pair => pair.Key.Day).ThenBy(static pair => pair.Key.Metric)
                .Select(static pair => new DailyMetricRow { DayDate = pair.Key.Day, Metric = pair.Key.Metric, Value = pair.Value })
                .ToList());
        }
    }

    /// <inheritdoc />
    public Task HeartRatesSaveAsync(Guid userId, IReadOnlyList<HeartRateRow> rows)
    {
        lock (store.Gate)
        {
            var table = MemoryStore.For(store.HeartRates, userId);
            foreach (var row in rows)
            {
                table[row.MinuteAt] = row.Bpm;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<HeartRateRow>> HeartRatesListAsync(Guid userId, DateTime from, DateTime to)
    {
        lock (store.Gate)
        {
            return Task.FromResult(MemoryStore.For(store.HeartRates, userId)
                .Where(pair => pair.Key >= from && pair.Key < to)
                .Select(static pair => new HeartRateRow { MinuteAt = pair.Key, Bpm = pair.Value })
                .ToList());
        }
    }

    /// <inheritdoc />
    public Task SamplesSaveAsync(Guid userId, IReadOnlyList<MetricSampleRow> rows)
    {
        lock (store.Gate)
        {
            var table = MemoryStore.For(store.Samples, userId);
            foreach (var row in rows)
            {
                table[(row.Metric, row.SampleAt)] = row.Value;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<MetricSampleRow>> SamplesListAsync(Guid userId, DateTime from, DateTime to)
    {
        lock (store.Gate)
        {
            return Task.FromResult(MemoryStore.For(store.Samples, userId)
                .Where(pair => pair.Key.At >= from && pair.Key.At < to)
                .OrderBy(static pair => pair.Key.At)
                .Select(static pair => new MetricSampleRow { Metric = pair.Key.Metric, SampleAt = pair.Key.At, Value = pair.Value })
                .ToList());
        }
    }

    /// <inheritdoc />
    public Task SleepsSaveAsync(Guid userId, IReadOnlyList<SleepNightRow> rows)
    {
        lock (store.Gate)
        {
            var table = MemoryStore.For(store.Sleeps, userId);
            foreach (var row in rows)
            {
                table[row.NightDate.Date] = row;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<SleepNightRow>> SleepsListAsync(Guid userId, DateOnly from, DateOnly to)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.ToDateTime(TimeOnly.MinValue);
        lock (store.Gate)
        {
            return Task.FromResult(MemoryStore.For(store.Sleeps, userId).Values.Where(row => row.NightDate >= start && row.NightDate <= end).ToList());
        }
    }

    /// <inheritdoc />
    public Task WorkoutsSaveAsync(Guid userId, IReadOnlyList<WorkoutRow> rows)
    {
        lock (store.Gate)
        {
            var table = MemoryStore.For(store.Workouts, userId);
            foreach (var row in rows)
            {
                if (table.TryGetValue(row.StartedAt, out var old))
                {
                    row.Hrr1 ??= old.Hrr1;
                    row.Hrr2 ??= old.Hrr2;
                }

                table[row.StartedAt] = row;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<WorkoutRow>> WorkoutsListAsync(Guid userId, DateOnly from, DateOnly to)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        lock (store.Gate)
        {
            return Task.FromResult(MemoryStore.For(store.Workouts, userId).Values.Where(row => row.StartedAt >= start && row.StartedAt < end).ToList());
        }
    }

    /// <inheritdoc />
    public Task WorkoutsRecoverySaveAsync(Guid userId, IReadOnlyList<WorkoutRow> rows)
    {
        lock (store.Gate)
        {
            var table = MemoryStore.For(store.Workouts, userId);
            foreach (var row in rows)
            {
                if (table.TryGetValue(row.StartedAt, out var workout))
                {
                    workout.Hrr1 = row.Hrr1;
                    workout.Hrr2 = row.Hrr2;
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task LoadsSaveAsync(Guid userId, IReadOnlyList<DayLoadRow> rows)
    {
        lock (store.Gate)
        {
            var table = MemoryStore.For(store.Loads, userId);
            foreach (var row in rows)
            {
                table[row.DayDate.Date] = row;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<DayLoadRow>> LoadsListAsync(Guid userId, DateOnly from, DateOnly to)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.ToDateTime(TimeOnly.MinValue);
        lock (store.Gate)
        {
            return Task.FromResult(MemoryStore.For(store.Loads, userId).Values.Where(row => row.DayDate >= start && row.DayDate <= end).ToList());
        }
    }
}

/// <summary>
/// The sync keys in demo mode (<see cref="MemoryStore"/>).
/// </summary>
/// <param name="store">The in-memory tables.</param>
/// <param name="timeProvider">The clock.</param>
internal sealed class MemorySyncKeyRepository(MemoryStore store, TimeProvider timeProvider) : ISyncKeyRepository
{
    /// <inheritdoc />
    public Task<SyncKeyRow?> GetAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.SyncKeys.TryGetValue(userId, out var key) ? Copy(key) : null);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(Guid userId, byte[] keyHash, string keyHint)
    {
        lock (store.Gate)
        {
            store.SyncKeys[userId] = new SyncKeyRow { KeyHash = keyHash, KeyHint = keyHint, CreatedAt = timeProvider.GetUtcNow().UtcDateTime };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId)
    {
        lock (store.Gate)
        {
            store.SyncKeys.Remove(userId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Guid?> UseAsync(byte[] keyHash)
    {
        lock (store.Gate)
        {
            foreach (var (userId, key) in store.SyncKeys)
            {
                if (key.KeyHash.AsSpan().SequenceEqual(keyHash))
                {
                    key.LastUsedAt = timeProvider.GetUtcNow().UtcDateTime;
                    return Task.FromResult<Guid?>(userId);
                }
            }
        }

        return Task.FromResult<Guid?>(null);
    }

    /// <inheritdoc />
    public Task MarkAsync(Guid userId, string summary)
    {
        lock (store.Gate)
        {
            if (store.SyncKeys.TryGetValue(userId, out var key))
            {
                key.LastSummary = summary;
            }
        }

        return Task.CompletedTask;
    }

    private static SyncKeyRow Copy(SyncKeyRow key) => new()
    {
        KeyHash = key.KeyHash,
        KeyHint = key.KeyHint,
        CreatedAt = key.CreatedAt,
        LastUsedAt = key.LastUsedAt,
        LastSummary = key.LastSummary,
    };
}
