using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Profiles.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Kardia.UnitTest.Days;

/// <summary>Scored days are kept between screens, and dropped as soon as the account's data changes.</summary>
public sealed class ScoreCacheTests
{
    [Fact]
    public async Task ScoresAreKept_UntilTheDataChanges()
    {
        var app = new TestApp(demo: true);
        var user = Guid.Parse(await app.CreateUserAsync());
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10_000 });
        var reader = new DayScoreReader(app.Health, cache);
        var facts = ProfileFacts.From(null, TestSupport.Today);
        var from = TestSupport.Today.AddDays(-29);

        var first = await reader.ScoresAsync(user, facts, from, TestSupport.Today);
        var second = await reader.ScoresAsync(user, facts, from, TestSupport.Today);
        Assert.False(ReferenceEquals(first, second));
        Assert.True(ReferenceEquals(first[^1], second[^1]));

        DataVersion.Bump(user);
        var third = await reader.ScoresAsync(user, facts, from, TestSupport.Today);
        Assert.False(ReferenceEquals(first[^1], third[^1]));
        Assert.Equal(first[^1].Recovery.Score, third[^1].Recovery.Score);
    }
}
