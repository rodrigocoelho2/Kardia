using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Features.Push.Models.DTOs;
using Kardia.WebApi.Features.Push.Models.Entities;
using Kardia.WebApi.Features.Push.Repositories;
using Kardia.WebApi.Features.Users.Repositories;
using Microsoft.Extensions.Options;

namespace Kardia.WebApi.Features.Push.Services;

/// <summary>Notifications for an account: devices, turning them on and off, and a test.</summary>
internal interface IPushService
{
    /// <summary>Whether notifications are on, the key to subscribe with and the account's devices.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    Task<ServiceApiResponse<PushDTO>> GetAsync(string userId);

    /// <summary>Turns notifications on for a browser.</summary>
    /// <param name="request">The subscription.</param>
    /// <returns>The state.</returns>
    Task<ServiceApiResponse<PushDTO>> SubscribeAsync(PushSubscribeRequest request);

    /// <summary>Turns notifications off for a device.</summary>
    /// <param name="request">The device.</param>
    /// <returns>The state.</returns>
    Task<ServiceApiResponse<PushDTO>> UnsubscribeAsync(PushUnsubscribeRequest request);

    /// <summary>Sends a test notification to the account's devices.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>How many it reached.</returns>
    Task<ServiceApiResponse<PushTestResultDTO>> TestAsync(string userId);
}

/// <summary>Notifications for an account. Only the browsers' own push services are accepted as endpoints.</summary>
/// <param name="pushRepository">Devices.</param>
/// <param name="pushSender">Sends notifications.</param>
/// <param name="userRepository">Accounts.</param>
/// <param name="options">The options (push on or off).</param>
/// <param name="globalizationHelper">Messages.</param>
internal sealed class PushService(IPushRepository pushRepository, IPushSender pushSender, IUserRepository userRepository, IOptions<KardiaOptions> options, IGlobalizationHelper globalizationHelper) : IPushService
{
    /// <summary>The longest device label kept.</summary>
    public const int MaxDevice = 60;

    /// <summary>The longest endpoint accepted.</summary>
    public const int MaxEndpoint = 800;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PushDTO>> GetAsync(string userId) => await UserAsync(userId) is { } id
        ? ServiceApiResponse<PushDTO>.Success(await StateAsync(id))
        : await NotFoundAsync<PushDTO>(userId);

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PushDTO>> SubscribeAsync(PushSubscribeRequest request)
    {
        if (await UserAsync(request.UserId) is not { } id)
        {
            return await NotFoundAsync<PushDTO>(request.UserId);
        }

        if (!options.Value.PushEnabled)
        {
            return await ProblemAsync<PushDTO>("Devices", nameof(TranslationKeys.PushDisabled), withLabel: false);
        }

        var endpoint = request.Endpoint?.Trim() ?? string.Empty;
        if (endpoint.Length > MaxEndpoint || !PushSender.IsPushServiceEndpoint(endpoint))
        {
            return await ProblemAsync<PushDTO>("Endpoint", nameof(TranslationKeys.PushEndpointNotAllowed));
        }

        if (!IsKey(request.P256dh, 65) || !IsKey(request.Auth, 16))
        {
            return await ProblemAsync<PushDTO>("Endpoint", nameof(TranslationKeys.PushKeysInvalid));
        }

        var device = (request.Device ?? string.Empty).Trim();
        await pushRepository.SaveAsync(id, endpoint, request.P256dh!.Trim(), request.Auth!.Trim(), device.Length <= MaxDevice ? device : device[..MaxDevice]);
        return ServiceApiResponse<PushDTO>.Success(await StateAsync(id));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PushDTO>> UnsubscribeAsync(PushUnsubscribeRequest request)
    {
        if (await UserAsync(request.UserId) is not { } id)
        {
            return await NotFoundAsync<PushDTO>(request.UserId);
        }

        if (request.Id is { } device)
        {
            await pushRepository.DeleteAsync(id, device, null);
        }

        if (!string.IsNullOrWhiteSpace(request.Endpoint))
        {
            await pushRepository.DeleteAsync(null, null, request.Endpoint.Trim());
        }

        return ServiceApiResponse<PushDTO>.Success(await StateAsync(id));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PushTestResultDTO>> TestAsync(string userId)
    {
        if (await UserAsync(userId) is not { } id)
        {
            return await NotFoundAsync<PushTestResultDTO>(userId);
        }

        if (!options.Value.PushEnabled)
        {
            return await ProblemAsync<PushTestResultDTO>("Devices", nameof(TranslationKeys.PushDisabled), withLabel: false);
        }

        var devices = await pushRepository.ListAsync(id);
        if (devices.Count == 0)
        {
            return await ProblemAsync<PushTestResultDTO>("Devices", nameof(TranslationKeys.PushNoDevices), withLabel: false);
        }

        var (reached, failed) = await pushSender.SendAsync(devices, new PushMessage("Kardia notifications are on", "Your morning recovery and bedtime will show up like this.", "/settings#notifications", "test"));
        return ServiceApiResponse<PushTestResultDTO>.Success(new PushTestResultDTO { Reached = reached, Failed = failed });
    }

    /// <summary>Whether a subscription key is base64url of the right length (65-byte uncompressed P-256 point, 16-byte secret).</summary>
    /// <param name="text">The key.</param>
    /// <param name="length">The byte length.</param>
    /// <returns>True when valid.</returns>
    internal static bool IsKey(string? text, int length)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
        {
            return false;
        }

        try
        {
            var bytes = WebPushCrypto.FromBase64Url(text.Trim());
            return bytes.Length == length && (length != 65 || bytes[0] == 0x04);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Maps a device.</summary>
    /// <param name="row">The device.</param>
    /// <returns>The DTO.</returns>
    internal static PushDeviceDTO ToDTO(PushSubscriptionRow row) => new()
    {
        Id = row.PushSubscriptionID,
        Key = PushDeviceDTO.KeyOf(row.Endpoint),
        Device = row.Device,
        CreatedAt = row.CreatedAt,
        LastSentAt = row.LastSentAt,
        LastError = row.LastError,
    };

    private async Task<PushDTO> StateAsync(Guid userId) => new()
    {
        Enabled = options.Value.PushEnabled,
        PublicKey = options.Value.PushEnabled ? await pushSender.PublicKeyAsync() : string.Empty,
        Devices = [.. (await pushRepository.ListAsync(userId)).OrderByDescending(static row => row.CreatedAt).Select(ToDTO)],
    };

    private async Task<Guid?> UserAsync(string userId) => Guid.TryParse(userId, out var id) && await userRepository.GetAsync(id) is not null ? id : null;

    private async Task<ServiceApiResponse<T>> NotFoundAsync<T>(string? userId) =>
        ServiceApiResponse<T>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));

    private async Task<ServiceApiResponse<T>> ProblemAsync<T>(string field, string key, bool withLabel = true)
    {
        var message = withLabel ? (await globalizationHelper.ValidationErrorAsync(field, key)).Message : await globalizationHelper.TranslateAsync(key);
        return ServiceApiResponse<T>.ValidationProblems(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems))).AddError(field, message);
    }
}
