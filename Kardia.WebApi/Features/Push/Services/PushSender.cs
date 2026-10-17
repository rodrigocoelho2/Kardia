using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kardia.WebApi.Features.Push.Models.Entities;
using Kardia.WebApi.Features.Push.Repositories;
using Microsoft.Extensions.Options;

namespace Kardia.WebApi.Features.Push.Services;

/// <summary>Sends notifications to devices (faked in tests).</summary>
internal interface IPushSender
{
    /// <summary>Gets the VAPID public key browsers subscribe with (made on first use and kept in the database).</summary>
    /// <returns>The key (base64url).</returns>
    Task<string> PublicKeyAsync();

    /// <summary>Sends a notification to devices; devices the push service says are gone are removed.</summary>
    /// <param name="devices">The devices.</param>
    /// <param name="message">The notification.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Devices reached and devices that failed.</returns>
    Task<(int Reached, int Failed)> SendAsync(IReadOnlyList<PushSubscriptionRow> devices, PushMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Web Push without a third party (same as Milo and Holo): the VAPID keys are made on first use and kept in
/// dbo.AppSecret, every notification is encrypted for each device (<see cref="WebPushCrypto"/>) and posted to the
/// browser's own push service (Apple, Google, Mozilla, Microsoft).
/// </summary>
/// <param name="pushRepository">Devices.</param>
/// <param name="secretRepository">Where the VAPID keys are kept.</param>
/// <param name="options">The options (push contact).</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="logger">The logger.</param>
internal sealed class PushSender(IPushRepository pushRepository, ISecretRepository secretRepository, IOptions<KardiaOptions> options, TimeProvider timeProvider, ILogger<PushSender> logger) : IPushSender
{
    /// <summary>Hosts of the browsers' push services: endpoints anywhere else are refused.</summary>
    internal static readonly string[] PushServiceHosts =
    [
        "fcm.googleapis.com", "android.googleapis.com", "push.services.mozilla.com", "push.apple.com", "notify.windows.com",
    ];

    private const string SecretName = "push-vapid";
    private static readonly HttpClient SharedHttp = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) }) { Timeout = TimeSpan.FromSeconds(15) };
    private readonly SemaphoreSlim _keyLock = new(1, 1);
    private (string PublicKey, string PrivateKey)? _keys;

    /// <summary>Gets the client that posts to the push services (replaced in tests). No logging handler: endpoints carry per-device tokens.</summary>
    internal HttpClient Http { get; init; } = SharedHttp;

    /// <inheritdoc />
    public async Task<string> PublicKeyAsync() => (await KeysAsync()).PublicKey;

    /// <inheritdoc />
    public async Task<(int Reached, int Failed)> SendAsync(IReadOnlyList<PushSubscriptionRow> devices, PushMessage message, CancellationToken cancellationToken = default)
    {
        if (devices.Count == 0)
        {
            return (0, 0);
        }

        var keys = await KeysAsync();
        int reached = 0, failed = 0;
        foreach (var device in devices)
        {
            var (status, detail) = await PostAsync(device, message, keys, cancellationToken);
            if (status is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                await pushRepository.DeleteAsync(null, null, device.Endpoint);
                logger.LogInformation("Push: a device is no longer subscribed and was removed");
                continue;
            }

            var ok = status is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Accepted or HttpStatusCode.NoContent;
            await pushRepository.MarkAsync(device.PushSubscriptionID, ok ? string.Empty : detail);
            if (ok)
            {
                reached++;
            }
            else
            {
                failed++;
                logger.LogWarning("Push to a device failed: {Error}", detail);
            }
        }

        return (reached, failed);
    }

    /// <summary>The JSON the service worker reads, cut to fit one encrypted record.</summary>
    /// <param name="message">The notification.</param>
    /// <returns>The payload.</returns>
    internal static byte[] PayloadOf(PushMessage message)
    {
        var body = message.Body;
        while (true)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(new { title = message.Title, body, url = message.Url, tag = message.Tag });
            if (json.Length <= WebPushCrypto.MaxPayloadBytes || body.Length == 0)
            {
                return json;
            }

            body = body[..Math.Max(0, body.Length - Math.Max(50, json.Length - WebPushCrypto.MaxPayloadBytes))] + "…";
        }
    }

    /// <summary>Whether an endpoint is an https URL of a known push service.</summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <returns>True when allowed.</returns>
    internal static bool IsPushServiceEndpoint(string? endpoint) =>
        Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && PushServiceHosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));

    private async Task<(HttpStatusCode Status, string Detail)> PostAsync(PushSubscriptionRow device, PushMessage message, (string PublicKey, string PrivateKey) keys, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = new Uri(device.Endpoint);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new ByteArrayContent(WebPushCrypto.Encrypt(PayloadOf(message), device.P256dh, device.Auth)),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentEncoding.Add("aes128gcm");
            request.Headers.TryAddWithoutValidation("TTL", "14400");
            request.Headers.TryAddWithoutValidation("Urgency", "normal");
            request.Headers.TryAddWithoutValidation("Topic", WebPushCrypto.Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(message.Tag)))[..32]);
            request.Headers.TryAddWithoutValidation("Authorization",
                WebPushCrypto.VapidAuthorization(endpoint, options.Value.PushSubject, keys.PublicKey, keys.PrivateKey, timeProvider.GetUtcNow().AddHours(12)));

            using var response = await Http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (response.StatusCode, string.Empty);
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response.StatusCode, $"HTTP {(int)response.StatusCode} {(text.Length <= 150 ? text : text[..150])}".Trim());
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ArgumentException or CryptographicException or FormatException
            && !cancellationToken.IsCancellationRequested)
        {
            return (0, exception.Message);
        }
    }

    /// <summary>The VAPID keys, from dbo.AppSecret or made (and saved) the first time.</summary>
    private async Task<(string PublicKey, string PrivateKey)> KeysAsync()
    {
        if (_keys is { } cached)
        {
            return cached;
        }

        await _keyLock.WaitAsync();
        try
        {
            if (_keys is { } again)
            {
                return again;
            }

            var fresh = WebPushCrypto.CreateVapidKeys();
            var stored = await secretRepository.EnsureAsync(SecretName, JsonSerializer.Serialize(new { publicKey = fresh.PublicKey, privateKey = fresh.PrivateKey }));
            using var document = JsonDocument.Parse(stored);
            var keys = (document.RootElement.GetProperty("publicKey").GetString()!, document.RootElement.GetProperty("privateKey").GetString()!);
            return (_keys = keys).Value;
        }
        finally
        {
            _keyLock.Release();
        }
    }
}
