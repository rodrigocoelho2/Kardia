namespace Kardia.UnitTest.Ages;

public sealed class BiologicalAgeTests
{
    [Fact]
    public async Task AFitThirtyYearOldIsYoungerThanTheCalendar()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync(sex: Kardia.Library.Enums.BiologicalSex.Male);

        var age = (await app.AgeService.GetAsync(userId)).Result!;

        Assert.True(age.Ready);
        Assert.Equal(30, Math.Round(age.ChronologicalAge!.Value));
        Assert.True(age.BiologicalAge < age.ChronologicalAge, $"{age.BiologicalAge} < {age.ChronologicalAge}");
        Assert.InRange(age.Difference!.Value, -12, 12);
        Assert.NotEmpty(age.Factors);
        Assert.Contains(age.Factors, f => f.Key == "vo2max");
        Assert.True(age.History.Count >= 2);
        Assert.InRange(age.Pace!.Value, 0.2, 2.5);
    }

    [Fact]
    public async Task WithoutABirthDateItAsksForOne()
    {
        var app = new TestApp(demo: true);
        var created = await app.UserService.CreateAsync(new Kardia.WebApi.Features.Users.Models.DTOs.UserCreateRequest { UserName = "nobirthday", Password = "recovery-2026" });

        var age = (await app.AgeService.GetAsync(created.Result!.Id)).Result!;

        Assert.False(age.HasBirthDate);
        Assert.Null(age.BiologicalAge);
    }

    [Fact]
    public async Task ItWaitsForTwoWeeksOfData()
    {
        var app = new TestApp();
        var userId = await app.CreateUserAsync();

        var age = (await app.AgeService.GetAsync(userId)).Result!;

        Assert.True(age.HasBirthDate);
        Assert.False(age.Ready);
        Assert.Equal(14, age.DaysNeeded);
    }
}
