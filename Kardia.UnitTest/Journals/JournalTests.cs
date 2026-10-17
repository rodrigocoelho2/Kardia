using Kardia.Library.Enums;
using Kardia.WebApi.Features.Journals.Models.DTOs;

namespace Kardia.UnitTest.Journals;

public sealed class JournalTests
{
    [Fact]
    public async Task AlcoholShowsUpTheNextMorning()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync();

        var journal = (await app.JournalService.GetAsync(userId, null)).Result!;

        var alcohol = Assert.Single(journal.Impacts, i => i.Tag == JournalTag.Alcohol);
        Assert.True(alcohol.Difference < -5, $"alcohol {alcohol.Difference}");
        Assert.True(alcohol.DaysWith >= 3);
    }

    [Fact]
    public async Task MarkingAHabitShowsOnTheDay()
    {
        var app = new TestApp();
        var userId = await app.CreateUserAsync();

        var on = await app.JournalService.SetAsync(new JournalSetRequest { UserId = userId, Date = TestSupport.Today, Tag = JournalTag.Sauna, On = true });
        Assert.True(on.IsSuccess);
        Assert.True(on.Result!.Tags.Single(t => t.Tag == JournalTag.Sauna).On);

        var off = await app.JournalService.SetAsync(new JournalSetRequest { UserId = userId, Date = TestSupport.Today, Tag = JournalTag.Sauna, On = false });
        Assert.False(off.Result!.Tags.Single(t => t.Tag == JournalTag.Sauna).On);
    }

    [Fact]
    public async Task TheFutureCantBeLogged()
    {
        var app = new TestApp();
        var userId = await app.CreateUserAsync();

        var response = await app.JournalService.SetAsync(new JournalSetRequest { UserId = userId, Date = TestSupport.Today.AddDays(2), Tag = JournalTag.Alcohol, On = true });

        Assert.False(response.IsSuccess);
    }
}
