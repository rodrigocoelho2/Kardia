using Kardia.WebApi.Extensions.Data;

namespace Kardia.WebApi.Features.Push.Repositories;

/// <summary>Values the server makes once and keeps (dbo.AppSecret): the VAPID keys of push notifications.</summary>
internal interface ISecretRepository
{
    /// <summary>Gets a value, saving <paramref name="value"/> first when there's none (the first writer wins).</summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value to keep when there's none yet.</param>
    /// <returns>The value kept.</returns>
    Task<string> EnsureAsync(string name, string value);
}

/// <summary>Secrets in SQL Server.</summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class SecretRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), ISecretRepository
{
    /// <inheritdoc />
    public Task<string> EnsureAsync(string name, string value) => ExecuteScalarAsync<string>("[dbo].[AppSecretEnsure]", new { Name = name, Value = value });
}

/// <summary>Secrets in demo mode.</summary>
/// <param name="store">The in-memory tables.</param>
internal sealed class MemorySecretRepository(MemoryStore store) : ISecretRepository
{
    /// <inheritdoc />
    public Task<string> EnsureAsync(string name, string value)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Secrets.TryAdd(name, value) ? value : store.Secrets[name]);
        }
    }
}
