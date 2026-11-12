namespace Kardia.WebApi.Features.Users.Services;

/// <summary>
/// Small rules about accounts that don't need the database.
/// </summary>
internal static class UserRules
{
    /// <summary>The longest Discord name kept.</summary>
    public const int DiscordNameLength = 64;

    /// <summary>Whether a text is a Discord account id (a snowflake: digits only).</summary>
    /// <param name="value">The text.</param>
    /// <returns>True for a Discord id.</returns>
    public static bool IsDiscordId(string? value) =>
        value is { Length: >= 5 and <= 32 } && value.All(char.IsAsciiDigit);

    /// <summary>The Discord name to keep: trimmed, shortened, or null when there's none.</summary>
    /// <param name="value">The name Discord gave.</param>
    /// <returns>The name, or null.</returns>
    public static string? DiscordName(string? value)
    {
        var name = value?.Trim();
        return string.IsNullOrEmpty(name) ? null : name.Length <= DiscordNameLength ? name : name[..DiscordNameLength];
    }
}
