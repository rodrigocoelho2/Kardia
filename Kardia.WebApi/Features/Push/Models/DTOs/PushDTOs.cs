using System.Security.Cryptography;
using System.Text;

namespace Kardia.WebApi.Features.Push.Models.DTOs;

/// <summary>Notifications for an account: whether the server sends them, the key browsers subscribe with, and the devices.</summary>
public sealed class PushDTO
{
    /// <summary>Gets or sets a value indicating whether the server sends notifications.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the VAPID public key (base64url) browsers subscribe with.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's devices, newest first.</summary>
    public List<PushDeviceDTO> Devices { get; set; } = [];
}

/// <summary>One device with notifications on.</summary>
public sealed class PushDeviceDTO
{
    /// <summary>Gets or sets the identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the first 12 hex characters of the endpoint's SHA-256, so a page knows its own device.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the label.</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was added (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when a notification last reached it (UTC).</summary>
    public DateTime? LastSentAt { get; set; }

    /// <summary>Gets or sets the last delivery error, or empty.</summary>
    public string LastError { get; set; } = string.Empty;

    /// <summary>The device key of an endpoint.</summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <returns>12 hex characters.</returns>
    public static string KeyOf(string endpoint) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)))[..12];
}

/// <summary>A browser's push subscription (PushSubscription.toJSON() of the page).</summary>
public sealed class PushSubscribeRequest
{
    /// <summary>Gets or sets the account.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the endpoint.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Gets or sets the public key (keys.p256dh).</summary>
    public string? P256dh { get; set; }

    /// <summary>Gets or sets the authentication secret (keys.auth).</summary>
    public string? Auth { get; set; }

    /// <summary>Gets or sets a label for the device list.</summary>
    public string? Device { get; set; }
}

/// <summary>A device to turn notifications off for: one of the account's by id, or this browser by its endpoint.</summary>
public sealed class PushUnsubscribeRequest
{
    /// <summary>Gets or sets the account.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the device id.</summary>
    public long? Id { get; set; }

    /// <summary>Gets or sets the endpoint.</summary>
    public string? Endpoint { get; set; }
}

/// <summary>How many devices a test notification reached.</summary>
public sealed class PushTestResultDTO
{
    /// <summary>Gets or sets the devices reached.</summary>
    public int Reached { get; set; }

    /// <summary>Gets or sets the devices that failed.</summary>
    public int Failed { get; set; }
}
