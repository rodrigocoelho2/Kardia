using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kardia.Library.Constants;
using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Syncs.Models.DTOs;
using Kardia.WebApi.Features.Syncs.Models.Entities;
using Kardia.WebApi.Features.Syncs.Repositories;
using Kardia.WebApi.Features.Users.Repositories;

namespace Kardia.WebApi.Features.Syncs.Services;

/// <summary>
/// The Apple Health connection: the personal key, and what Health Auto Export or the Shortcut sends with it.
/// </summary>
internal interface ISyncService
{
    /// <summary>Gets the connection (hint only).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    Task<ServiceApiResponse<SyncKeyDTO>> KeyGetAsync(string userId);

    /// <summary>Makes a new key (the old one stops working). The key is in this answer only.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The key.</returns>
    Task<ServiceApiResponse<SyncKeyCreatedDTO>> KeyCreateAsync(string userId);

    /// <summary>Revokes the key.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection, without a key.</returns>
    Task<ServiceApiResponse<SyncKeyDTO>> KeyRevokeAsync(string userId);

    /// <summary>Imports an export.</summary>
    /// <param name="key">The personal key sent with it.</param>
    /// <param name="body">The JSON body.</param>
    /// <returns>What was imported.</returns>
    Task<ServiceApiResponse<SyncIngestResultDTO>> IngestAsync(string? key, JsonElement body);
}

/// <summary>
/// <see cref="ISyncService"/>: keys are random, shown once and kept only as a SHA-256; an import is read by
/// <see cref="HealthPayload"/>, saved, and the touched days' loads are recomputed.
/// </summary>
/// <param name="syncKeyRepository">Sync keys.</param>
/// <param name="healthDataRepository">Health data.</param>
/// <param name="userRepository">Accounts.</param>
/// <param name="dayLoadService">Recomputes day loads.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="clock">The app's clock.</param>
/// <param name="logger">Logger (never logs a key).</param>
internal sealed class SyncService(
    ISyncKeyRepository syncKeyRepository,
    IHealthDataRepository healthDataRepository,
    IUserRepository userRepository,
    IDayLoadService dayLoadService,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock,
    ILogger<SyncService> logger) : ISyncService
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");

    private readonly ISyncKeyRepository _syncKeyRepository = syncKeyRepository;
    private readonly IHealthDataRepository _healthDataRepository = healthDataRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IDayLoadService _dayLoadService = dayLoadService;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly IAppClock _clock = clock;
    private readonly ILogger<SyncService> _logger = logger;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SyncKeyDTO>> KeyGetAsync(string userId)
    {
        if (await UserAsync(userId) is not { } id)
        {
            return ServiceApiResponse<SyncKeyDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        return ServiceApiResponse<SyncKeyDTO>.Success(ToDTO(await _syncKeyRepository.GetAsync(id)));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SyncKeyCreatedDTO>> KeyCreateAsync(string userId)
    {
        if (await UserAsync(userId) is not { } id)
        {
            return ServiceApiResponse<SyncKeyCreatedDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        var key = ApiHeaders.SyncKeyPrefix + Base32(RandomNumberGenerator.GetBytes(20));
        await _syncKeyRepository.SaveAsync(id, Hash(key), "…" + key[^4..]);
        return ServiceApiResponse<SyncKeyCreatedDTO>.Success(new SyncKeyCreatedDTO { Key = key, Status = ToDTO(await _syncKeyRepository.GetAsync(id)) });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SyncKeyDTO>> KeyRevokeAsync(string userId)
    {
        if (await UserAsync(userId) is not { } id)
        {
            return ServiceApiResponse<SyncKeyDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        await _syncKeyRepository.DeleteAsync(id);
        return ServiceApiResponse<SyncKeyDTO>.Success(new SyncKeyDTO());
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SyncIngestResultDTO>> IngestAsync(string? key, JsonElement body)
    {
        var trimmed = (key ?? string.Empty).Trim();
        if (body.ValueKind == JsonValueKind.Object && string.IsNullOrEmpty(trimmed)
            && body.TryGetProperty("key", out var inBody) && inBody.ValueKind == JsonValueKind.String)
        {
            trimmed = inBody.GetString()!.Trim();
        }

        trimmed = NormaliseKey(trimmed);
        var userId = trimmed.StartsWith(ApiHeaders.SyncKeyPrefix, StringComparison.Ordinal) && trimmed.Length < 100
            ? await _syncKeyRepository.UseAsync(Hash(trimmed))
            : null;
        if (userId is not { } id)
        {
            return ServiceApiResponse<SyncIngestResultDTO>.Unauthorized(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.SyncKeyInvalid)));
        }

        if (body.ValueKind != JsonValueKind.Object)
        {
            var invalid = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.SyncPayloadInvalid));
            await _syncKeyRepository.MarkAsync(id, invalid);
            return ServiceApiResponse<SyncIngestResultDTO>.ValidationProblems(invalid).AddError("Body", invalid);
        }

        // The work runs on its own task, detached from the request: a big export on a slow NAS can take longer than
        // Health Auto Export waits (60 s by default), and an iPhone that gives up must not cancel the save. Within the
        // budget the answer has the full counts; past it, Kardia answers "still saving" and finishes in the background.
        var copy = body.Clone();
        var work = Task.Run(() => ProcessAsync(id, copy));
        if (await Task.WhenAny(work, Task.Delay(AnswerWithin)) == work)
        {
            return ServiceApiResponse<SyncIngestResultDTO>.Success(await work);
        }

        _ = work.ContinueWith(
            task => _logger.LogError(task.Exception, "Sync for {UserId} failed in the background.", id),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        await _syncKeyRepository.MarkAsync(id, "Received a big export: still saving, refresh in a minute");
        return ServiceApiResponse<SyncIngestResultDTO>.Success(new SyncIngestResultDTO { Summary = "Received, still saving" });
    }

    /// <summary>How long a sync request waits for the save before answering "still saving".</summary>
    internal static TimeSpan AnswerWithin { get; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Reads an export and saves it, logging how long each step took.
    /// </summary>
    /// <param name="id">The account.</param>
    /// <param name="body">The JSON body (owned by this call).</param>
    /// <returns>What was imported.</returns>
    private async Task<SyncIngestResultDTO> ProcessAsync(Guid id, JsonElement body)
    {
        // One export at a time: Health Auto Export retries and several automations firing together would otherwise
        // parse and save in parallel and starve a small NAS.
        await OneAtATime.WaitAsync();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var data = HealthPayload.Parse(body, _clock.Zone, _clock.Now);
            var parsed = watch.ElapsedMilliseconds;
            if (data.IsEmpty)
            {
                // "Since last sync" with nothing new is a normal sync. What arrived is logged, so a format Kardia doesn't
                // read yet shows up in the container log.
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Sync for {UserId} had nothing to import: {Shape}", id, Describe(body));
                await _syncKeyRepository.MarkAsync(id, await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.SyncNothingToImport)));
                return new SyncIngestResultDTO { Summary = "Nothing new" };
            }

            await _healthDataRepository.MetricsSaveAsync(id, data.Metrics);
            await _healthDataRepository.HeartRatesSaveAsync(id, data.HeartRates);
            await _healthDataRepository.SleepsSaveAsync(id, await MergeNightsAsync(id, data.Sleeps));
            await _healthDataRepository.WorkoutsSaveAsync(id, data.Workouts);
            await SaveVitalsAsync(id, data);
            DataVersion.Bump(id);
            var saved = watch.ElapsedMilliseconds;

            // A new resting heart rate changes the load of its day too.
            var loadDays = data.HeartRates.Select(static row => DateOnly.FromDateTime(row.MinuteAt))
                .Concat(data.Metrics.Where(static row => row.Metric == (int)HealthMetric.RestingHeartRate).Select(static row => DateOnly.FromDateTime(row.DayDate)));
            await _dayLoadService.RecomputeAsync(id, loadDays);

            var days = data.Days.ToList();
            var result = new SyncIngestResultDTO
            {
                Days = days.Count,
                Metrics = data.Metrics.Count + data.Samples.Select(static sample => (sample.SampleAt.Date, sample.Metric)).Distinct().Count(),
                HeartRates = data.HeartRates.Count,
                Sleeps = data.Sleeps.Count,
                Workouts = data.Workouts.Count,
                From = days.Count > 0 ? days[0] : null,
                To = days.Count > 0 ? days[^1] : null,
            };
            result.Summary = Summary(result);
            await _syncKeyRepository.MarkAsync(id, result.Summary);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Sync for {UserId}: {Summary} (read {Parse} ms, saved {Save} ms, strain {Load} ms)", id, result.Summary, parsed, saved - parsed, watch.ElapsedMilliseconds - saved);

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Sync for {UserId} failed after {Elapsed} ms.", id, watch.ElapsedMilliseconds);
            await _syncKeyRepository.MarkAsync(id, "The last sync failed: " + exception.Message[..Math.Min(150, exception.Message.Length)]);
            throw;
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    /// <summary>
    /// Joins each night of an export with the same night already saved, so a partial re-send never shortens it.
    /// </summary>
    /// <param name="id">The account.</param>
    /// <param name="nights">The export's nights.</param>
    /// <returns>The nights to save.</returns>
    private async Task<List<SleepNightRow>> MergeNightsAsync(Guid id, IReadOnlyList<SleepNightRow> nights)
    {
        if (nights.Count == 0)
        {
            return [];
        }

        var saved = (await _healthDataRepository.SleepsListAsync(id, DateOnly.FromDateTime(nights.Min(static n => n.NightDate)), DateOnly.FromDateTime(nights.Max(static n => n.NightDate))))
            .ToDictionary(static night => night.NightDate.Date);
        return nights.Select(night => saved.TryGetValue(night.NightDate.Date, out var old) ? HealthPayload.Merge(old, night) : night).ToList();
    }

    /// <summary>
    /// Saves the timed HRV, respiratory-rate and blood-oxygen readings, then works out the day values they touch from the
    /// readings taken during each night (<see cref="NightlyVitals"/>). A new night also re-works its day.
    /// </summary>
    /// <param name="id">The account.</param>
    /// <param name="data">The export.</param>
    /// <returns>A task.</returns>
    private async Task SaveVitalsAsync(Guid id, HealthPayloadData data)
    {
        if (data.Samples.Count > 0)
        {
            await _healthDataRepository.SamplesSaveAsync(id, data.Samples);
        }

        // A reading in the evening belongs to the next morning's night.
        var days = data.Samples.SelectMany(static sample => new[] { DateOnly.FromDateTime(sample.SampleAt), DateOnly.FromDateTime(sample.SampleAt).AddDays(1) })
            .Concat(data.Sleeps.Select(static night => DateOnly.FromDateTime(night.NightDate)))
            .Where(day => day <= _clock.Today)
            .Distinct()
            .Order()
            .ToList();
        if (days.Count == 0)
        {
            return;
        }

        var nights = await _healthDataRepository.SleepsListAsync(id, days[0], days[^1]);
        var samples = await _healthDataRepository.SamplesListAsync(id, days[0].AddDays(-1).ToDateTime(new TimeOnly(12, 0)), days[^1].AddDays(1).ToDateTime(TimeOnly.MinValue));
        var rows = NightlyVitals.Compute(days, nights, samples);
        if (rows.Count > 0)
        {
            await _healthDataRepository.MetricsSaveAsync(id, rows);
        }
    }

    /// <summary>Lets one export be read and saved at a time.</summary>
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);

    /// <summary>
    /// A short description of an export's shape for the log: each metric's name, sample count and the fields of its
    /// first sample, the number of workouts, and any other top-level fields. No values.
    /// </summary>
    /// <param name="body">The JSON body.</param>
    /// <returns>The description.</returns>
    internal static string Describe(JsonElement body)
    {
        var parts = new List<string>();
        var data = body.TryGetProperty("data", out var inner) && inner.ValueKind == JsonValueKind.Object ? inner : body;
        if (data.TryGetProperty("metrics", out var metrics) && metrics.ValueKind == JsonValueKind.Array)
        {
            foreach (var metric in metrics.EnumerateArray().Where(static m => m.ValueKind == JsonValueKind.Object).Take(20))
            {
                var name = metric.TryGetProperty("name", out var n) ? n.ToString() : "?";
                var samples = metric.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array ? d : default;
                var count = samples.ValueKind == JsonValueKind.Array ? samples.GetArrayLength() : 0;
                var fields = count > 0 && samples[0].ValueKind == JsonValueKind.Object
                    ? string.Join(",", samples[0].EnumerateObject().Select(static f => f.Name))
                    : string.Empty;
                parts.Add($"{name}×{count}[{fields}]");
            }
        }

        if (data.TryGetProperty("workouts", out var workouts) && workouts.ValueKind == JsonValueKind.Array)
        {
            parts.Add($"workouts×{workouts.GetArrayLength()}");
        }

        if (parts.Count == 0)
        {
            parts.Add("fields: " + string.Join(",", data.EnumerateObject().Select(static f => f.Name).Take(20)));
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// The words a sync shows: "2 days · 1 440 heart-rate minutes · 1 night · 1 workout".
    /// </summary>
    /// <param name="result">The numbers.</param>
    /// <returns>The words.</returns>
    internal static string Summary(SyncIngestResultDTO result)
    {
        static string Count(int count, string one, string many) => count.ToString("N0", English) + " " + (count == 1 ? one : many);
        var parts = new List<string> { Count(result.Days, "day", "days") };
        if (result.HeartRates > 0)
        {
            parts.Add(Count(result.HeartRates, "heart-rate minute", "heart-rate minutes"));
        }

        if (result.Sleeps > 0)
        {
            parts.Add(Count(result.Sleeps, "night", "nights"));
        }

        if (result.Workouts > 0)
        {
            parts.Add(Count(result.Workouts, "workout", "workouts"));
        }

        if (result.Metrics > 0)
        {
            parts.Add(Count(result.Metrics, "daily value", "daily values"));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// The SHA-256 of a key (what the database keeps).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The hash.</returns>
    internal static byte[] Hash(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));

    /// <summary>
    /// Undoes what phones do to a pasted key: a capitalised first letter (iOS autocapitalisation), invisible characters
    /// from copy and paste, and a header sent twice ("key, key"). Keys are lower-case letters, digits and underscores,
    /// so this never turns a wrong key into a right one.
    /// </summary>
    /// <param name="key">The key as received.</param>
    /// <returns>The key as Kardia made it.</returns>
    internal static string NormaliseKey(string key)
    {
        var first = key.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        return new string([.. first.ToLowerInvariant().Where(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')]);
    }

    private static SyncKeyDTO ToDTO(SyncKeyRow? row) => row is null ? new SyncKeyDTO() : new SyncKeyDTO
    {
        HasKey = true,
        KeyHint = row.KeyHint,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)),
        LastUsedAt = row.LastUsedAt is { } used ? new DateTimeOffset(DateTime.SpecifyKind(used, DateTimeKind.Utc)) : null,
        LastSummary = string.IsNullOrEmpty(row.LastSummary) ? null : row.LastSummary,
    };

    /// <summary>Lower-case base32 without padding: easy to read out and to paste, no ambiguous symbols.</summary>
    private static string Base32(byte[] bytes)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz234567";
        var builder = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                builder.Append(alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            builder.Append(alphabet[(buffer << (5 - bits)) & 31]);
        }

        return builder.ToString();
    }

    private async Task<Guid?> UserAsync(string userId) =>
        Guid.TryParse(userId, out var id) && await _userRepository.GetAsync(id) is not null ? id : null;
}
