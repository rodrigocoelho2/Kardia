using Kardia.WebApi.Features.Users.Services;

namespace Kardia.UnitTest.Users;

/// <summary>
/// Discord ids and names: only digits sign in, names are trimmed and kept short.
/// </summary>
public sealed class UserRulesTests
{
    [Theory]
    [InlineData("80351110224678912", true)]
    [InlineData("1570448224030826588", true)]
    [InlineData("1234", false)]
    [InlineData("8035111022467891a", false)]
    [InlineData(" 80351110224678912", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDiscordId_DigitsOnly(string? value, bool expected) => Assert.Equal(expected, UserRules.IsDiscordId(value));

    [Fact]
    public void DiscordName_TrimsAndShortens()
    {
        Assert.Equal("Rodas", UserRules.DiscordName("  Rodas "));
        Assert.Null(UserRules.DiscordName("   "));
        Assert.Null(UserRules.DiscordName(null));
        Assert.Equal(UserRules.DiscordNameLength, UserRules.DiscordName(new string('x', 100))!.Length);
    }
}
