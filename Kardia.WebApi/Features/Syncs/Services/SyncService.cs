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

        var userId = trimmed.StartsWith(ApiHeaders.SyncKeyPrefix, StringComparison.Ordinal) && trimmed.Length < 100
            ? await _syncKeyRepository.UseAsync(Hash(trimmed))
            : null;
        if (userId is not { } id)
        {
            return ServiceApiResponse<SyncIngestResultDTO>.Unauthorized(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.SyncKeyInvalid)));
        }

        var data = HealthPayload.Parse(body, _clock.Zone, _clock.Now);
        if (data.IsEmpty)
        {
            var message = await _globalizationHelper.TranslateAsync(body.ValueKind == JsonValueKind.Object ? nameof(TranslationKeys.SyncNothingToImport) : nameof(TranslationKeys.SyncPayloadInvalid));
            await _syncKeyRepository.MarkAsync(id, message);
            return ServiceApiResponse<SyncIngestResultDTO>.ValidationProblems(message).AddError("Body", message);
        }

        await _healthDataRepository.MetricsSaveAsync(id, data.Metrics);
        await _healthDataRepository.HeartRatesSaveAsync(id, data.HeartRates);
        await _healthDataRepository.SleepsSaveAsync(id, data.Sleeps);
        await _healthDataRepository.WorkoutsSaveAsync(id, data.Workouts);

        // A new resting heart rate changes the load of its day too.
        var loadDays = data.HeartRates.Select(static row => DateOnly.FromDateTime(row.MinuteAt))
            .Concat(data.Metrics.Where(static row => row.Metric == (int)HealthMetric.RestingHeartRate).Select(static row => DateOnly.FromDateTime(row.DayDate)));
        await _dayLoadService.RecomputeAsync(id, loadDays);

        var days = data.Days.ToList();
        var result = new SyncIngestResultDTO
        {
            Days = days.Count,
            Metrics = data.Metrics.Count,
            HeartRates = data.HeartRates.Count,
            Sleeps = data.Sleeps.Count,
            Workouts = data.Workouts.Count,
            From = days.Count > 0 ? days[0] : null,
            To = days.Count > 0 ? days[^1] : null,
        };
        result.Summary = Summary(result);
        await _syncKeyRepository.MarkAsync(id, result.Summary);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Sync for {UserId}: {Summary}", id, result.Summary);

        return ServiceApiResponse<SyncIngestResultDTO>.Success(result);
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
