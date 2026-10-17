using Kardia.Library.Enums;
using Kardia.WebApi.Features.Users.Models.DTOs;

namespace Kardia.UnitTest.Users;

public sealed class UserServiceTests
{
    [Fact]
    public async Task TheFirstAccountIsTheAdmin()
    {
        var app = new TestApp();

        var first = await app.UserService.CreateAsync(new UserCreateRequest { UserName = "rita", Password = "recovery-2026" });
        var second = await app.UserService.CreateAsync(new UserCreateRequest { UserName = "joao", Password = "recovery-2026" });

        Assert.Equal(UserRole.Admin, first.Result!.Role);
        Assert.Equal(UserRole.User, second.Result!.Role);
    }

    [Fact]
    public async Task UserNamesAreUniqueIgnoringCase()
    {
        var app = new TestApp();
        await app.CreateUserAsync("rita");

        var again = await app.UserService.CreateAsync(new UserCreateRequest { UserName = "RITA", Password = "recovery-2026" });

        Assert.False(again.IsSuccess);
    }

    [Theory]
    [InlineData("ab", "recovery-2026")]
    [InlineData("rita with spaces", "recovery-2026")]
    [InlineData("rita", "short")]
    public async Task BadAccountsAreRefused(string name, string password)
    {
        var app = new TestApp();

        var response = await app.UserService.CreateAsync(new UserCreateRequest { UserName = name, Password = password });

        Assert.False(response.IsSuccess);
        Assert.NotNull(response.Problems);
    }

    [Fact]
    public async Task SignInChecksThePassword()
    {
        var app = new TestApp();
        await app.CreateUserAsync("rita");

        var good = await app.UserService.SignInAsync(new UserSignInRequest { UserName = "Rita", Password = "recovery-2026" });
        var bad = await app.UserService.SignInAsync(new UserSignInRequest { UserName = "rita", Password = "wrong-password" });

        Assert.True(good.IsSuccess);
        Assert.False(bad.IsSuccess);
    }

    [Fact]
    public async Task ChangingThePasswordChangesTheStamp()
    {
        var app = new TestApp();
        var id = await app.CreateUserAsync("rita");
        var before = (await app.UserService.GetAsync(id)).Result!.SecurityStamp;

        var changed = await app.UserService.ChangePasswordAsync(new UserPasswordRequest { Id = id, CurrentPassword = "recovery-2026", NewPassword = "sleep-more-2026" });

        Assert.True(changed.IsSuccess);
        Assert.NotEqual(before, changed.Result!.SecurityStamp);
        Assert.True((await app.UserService.SignInAsync(new UserSignInRequest { UserName = "rita", Password = "sleep-more-2026" })).IsSuccess);
    }

    [Fact]
    public async Task DeletingAnAccountDeletesItsData()
    {
        var app = new TestApp(demo: true);
        await app.CreateUserAsync("rita");
        var joao = await app.CreateUserAsync("joao");

        var deleted = await app.UserService.DeleteAsync(joao);

        Assert.True(deleted.IsSuccess);
        Assert.False((await app.UserService.GetAsync(joao)).IsSuccess);
        Assert.Empty(await app.Health.MetricsListAsync(Guid.Parse(joao), TestSupport.Today.AddDays(-30), TestSupport.Today));
    }

    [Fact]
    public async Task TheLastAdminCantBeDeleted()
    {
        var app = new TestApp();
        var rita = await app.CreateUserAsync("rita");

        var deleted = await app.UserService.DeleteAsync(rita);

        Assert.False(deleted.IsSuccess);
        Assert.True((await app.UserService.GetAsync(rita)).IsSuccess);
    }
}
