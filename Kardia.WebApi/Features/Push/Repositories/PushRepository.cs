using System.Data;
using Dapper;
using Kardia.WebApi.Extensions.Data;
using Kardia.WebApi.Features.Push.Models.Entities;

namespace Kardia.WebApi.Features.Push.Repositories;

/// <summary>The devices with notifications on (dbo.PushSubscription) and the notifications already sent (dbo.PushSent).</summary>
internal interface IPushRepository
{
    /// <summary>Lists devices.</summary>
    /// <param name="userId">One account's, or null for every account's.</param>
    /// <returns>The devices.</returns>
    Task<List<PushSubscriptionRow>> ListAsync(Guid? userId);

    /// <summary>Adds or updates this browser for an account.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="p256dh">The public key.</param>
    /// <param name="auth">The secret.</param>
    /// <param name="device">The label.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(Guid userId, string endpoint, string p256dh, string auth, string device);

    /// <summary>Removes a device by id (one of the account's) or by endpoint.</summary>
    /// <param name="userId">The account (with <paramref name="id"/>).</param>
    /// <param name="id">The device id.</param>
    /// <param name="endpoint">The endpoint.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(Guid? userId, long? id, string? endpoint);

    /// <summary>Records how the last notification to a device went.</summary>
    /// <param name="id">The device.</param>
    /// <param name="error">The error, or empty when delivered.</param>
    /// <returns>A task.</returns>
    Task MarkAsync(long id, string error);

    /// <summary>Claims a notification so it goes out once.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="key">The notification key ("morning:2026-10-16").</param>
    /// <returns>True the first time.</returns>
    Task<bool> ClaimAsync(Guid userId, string key);
}

/// <summary>Devices and sent notifications in SQL Server (stored procedures in Kardia.Database/StoredProcedures/Push).</summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class PushRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IPushRepository
{
    /// <inheritdoc />
    public Task<List<PushSubscriptionRow>> ListAsync(Guid? userId) => QueryAsync<PushSubscriptionRow>("[dbo].[PushSubscriptionList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task SaveAsync(Guid userId, string endpoint, string p256dh, string auth, string device) =>
        ExecuteAsync("[dbo].[PushSubscriptionSave]", new { UserUniqueID = userId, Endpoint = endpoint, P256dh = p256dh, Auth = auth, Device = device });

    /// <inheritdoc />
    public Task DeleteAsync(Guid? userId, long? id, string? endpoint) =>
        ExecuteAsync("[dbo].[PushSubscriptionDelete]", new { UserUniqueID = userId, PushSubscriptionID = id, Endpoint = endpoint });

    /// <inheritdoc />
    public Task MarkAsync(long id, string error) =>
        ExecuteAsync("[dbo].[PushSubscriptionMark]", new { PushSubscriptionID = id, LastError = error.Length <= 200 ? error : error[..200] });

    /// <inheritdoc />
    public async Task<bool> ClaimAsync(Guid userId, string key)
    {
        var parameters = new DynamicParameters(new { UserUniqueID = userId, SentKey = key.Length <= 80 ? key : key[..80] });
        parameters.Add("Claimed", dbType: DbType.Boolean, direction: ParameterDirection.Output);
        await ExecuteAsync("[dbo].[PushSentClaim]", parameters);
        return parameters.Get<bool>("Claimed");
    }
}

/// <summary>Devices and sent notifications in demo mode (<see cref="MemoryStore"/>).</summary>
/// <param name="store">The in-memory tables.</param>
internal sealed class MemoryPushRepository(MemoryStore store) : IPushRepository
{
    private long _nextId;

    /// <inheritdoc />
    public Task<List<PushSubscriptionRow>> ListAsync(Guid? userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.PushSubscriptions.Where(row => userId is null || row.UserUniqueID == userId).Select(Copy).ToList());
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(Guid userId, string endpoint, string p256dh, string auth, string device)
    {
        lock (store.Gate)
        {
            var row = store.PushSubscriptions.FirstOrDefault(row => row.Endpoint == endpoint);
            if (row is null)
            {
                row = new PushSubscriptionRow { PushSubscriptionID = ++_nextId, Endpoint = endpoint, CreatedAt = DateTime.UtcNow };
                store.PushSubscriptions.Add(row);
            }

            row.UserUniqueID = userId;
            row.P256dh = p256dh;
            row.Auth = auth;
            row.Device = device;
            row.LastError = string.Empty;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid? userId, long? id, string? endpoint)
    {
        lock (store.Gate)
        {
            store.PushSubscriptions.RemoveAll(row => (id is not null && row.PushSubscriptionID == id && row.UserUniqueID == userId) || (endpoint is not null && row.Endpoint == endpoint));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkAsync(long id, string error)
    {
        lock (store.Gate)
        {
            if (store.PushSubscriptions.FirstOrDefault(row => row.PushSubscriptionID == id) is { } row)
            {
                row.LastError = error;
                row.LastSentAt = error.Length == 0 ? DateTime.UtcNow : row.LastSentAt;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ClaimAsync(Guid userId, string key)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.PushSent.Add((userId, key)));
        }
    }

    private static PushSubscriptionRow Copy(PushSubscriptionRow row) => new()
    {
        PushSubscriptionID = row.PushSubscriptionID,
        UserUniqueID = row.UserUniqueID,
        Endpoint = row.Endpoint,
        P256dh = row.P256dh,
        Auth = row.Auth,
        Device = row.Device,
        CreatedAt = row.CreatedAt,
        LastSentAt = row.LastSentAt,
        LastError = row.LastError,
    };
}
