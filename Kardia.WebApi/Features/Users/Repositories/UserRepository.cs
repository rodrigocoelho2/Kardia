using Kardia.WebApi.Extensions.Data;
using Kardia.WebApi.Features.Users.Models.Entities;

namespace Kardia.WebApi.Features.Users.Repositories;

/// <summary>
/// The accounts in SQL Server (stored procedures in Kardia.Database/StoredProcedures/Users).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class UserRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IUserRepository
{
    /// <inheritdoc />
    public Task<int> CountAsync() => ExecuteScalarAsync<int>("[dbo].[UserCount]");

    /// <inheritdoc />
    public Task<List<UserAccount>> ListAsync() => QueryAsync<UserAccount>("[dbo].[UserList]");

    /// <inheritdoc />
    public Task<UserAccount?> GetAsync(Guid userId) => QuerySingleOrDefaultAsync<UserAccount>("[dbo].[UserGet]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task<UserAccount?> GetByNameAsync(string userName) => QuerySingleOrDefaultAsync<UserAccount>("[dbo].[UserGetByName]", new { Username = userName });

    /// <inheritdoc />
    public Task<UserAccount?> CreateAsync(string userName, string passwordHash, string securityStamp) =>
        QuerySingleOrDefaultAsync<UserAccount>("[dbo].[UserCreate]", new { Username = userName, PasswordHash = passwordHash, SecurityStamp = securityStamp });

    /// <inheritdoc />
    public Task SetPasswordAsync(Guid userId, string passwordHash, string securityStamp) =>
        ExecuteAsync("[dbo].[UserSetPassword]", new { UserUniqueID = userId, PasswordHash = passwordHash, SecurityStamp = securityStamp });

    /// <inheritdoc />
    public Task TouchLoginAsync(Guid userId) => ExecuteAsync("[dbo].[UserTouchLogin]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task<UserAccount?> GetByDiscordAsync(string discordId) =>
        QuerySingleOrDefaultAsync<UserAccount>("[dbo].[UserGetByDiscord]", new { DiscordID = discordId });

    /// <inheritdoc />
    public Task<bool> SetDiscordAsync(Guid userId, string? discordId, string? discordName) =>
        ExecuteScalarAsync<bool>("[dbo].[UserSetDiscord]", new { UserUniqueID = userId, DiscordID = discordId, DiscordName = discordName });

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId) => ExecuteAsync("[dbo].[UserDelete]", new { UserUniqueID = userId });
}
