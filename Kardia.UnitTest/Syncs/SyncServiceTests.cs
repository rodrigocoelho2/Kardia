using System.Text.Json;
using Kardia.Library.Services;

namespace Kardia.UnitTest.Syncs;

public sealed class SyncServiceTests
{
    private static JsonElement Body(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task AKeyIsShownOnceAndOnlyItsHintIsKept()
    {
        var app = new TestApp();
        var userId = await app.CreateUserAsync();

        var created = (await app.SyncService.KeyCreateAsync(userId)).Result!;
        var status = (await app.SyncService.KeyGetAsync(userId)).Result!;

        Assert.StartsWith(Kardia.Library.Constants.ApiHeaders.SyncKeyPrefix, created.Key);
        Assert.True(status.HasKey);
        Assert.DoesNotContain(created.Key, status.KeyHint ?? string.Empty);
        Assert.Equal("…" + created.Key[^4..], status.KeyHint);
    }

    [Fact]
    public async Task ASyncWithTheKeyLandsInTheAccount()
    {
        var app = new TestApp();
        var userId = await app.CreateUserAsync();
        var key = (await app.SyncService.KeyCreateAsync(userId)).Result!.Key;

        var result = await app.SyncService.IngestAsync(key, Body("""
            { "data": { "metrics": [
              { "name": "heart_rate_variability", "units": "ms", "data": [ { "date": "2026-10-16 06:58:00 +0100", "qty": 68 } ] },
              { "name": "heart_rate", "units": "count/min", "data": [ { "date": "2026-10-16 08:30:00 +0100", "Avg": 152 }, { "date": "2026-10-16 08:31:00 +0100", "Avg": 156 } ] }
            ] } }
            """));

        Assert.True(result.IsSuccess, result.Problems?.Title);
        Assert.Equal(2, result.Result!.HeartRates);
        Assert.Equal(1, result.Result.Metrics);
        Assert.NotEqual(string.Empty, result.Result.Summary);
        var stored = await app.Health.MetricsListAsync(Guid.Parse(userId), TestSupport.Today, TestSupport.Today);
        Assert.Contains(stored, m => m.Value == 68m);
        Assert.NotNull((await app.SyncService.KeyGetAsync(userId)).Result!.LastUsedAt);
    }

    [Fact]
    public async Task AWrongKeyIsRefused()
    {
        var app = new TestApp();
        var userId = await app.CreateUserAsync();
        await app.SyncService.KeyCreateAsync(userId);

        var result = await app.SyncService.IngestAsync("kardia_sk_not-the-key", Body("""{ "hrv": 68 }"""));

        Assert.Equal(ServiceApiResponseStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task ARevokedKeyStopsWorking()
    {
        var app = new TestApp();
        var userId = await app.CreateUserAsync();
        var key = (await app.SyncService.KeyCreateAsync(userId)).Result!.Key;

        await app.SyncService.KeyRevokeAsync(userId);
        var result = await app.SyncService.IngestAsync(key, Body("""{ "hrv": 68 }"""));

        Assert.Equal(ServiceApiResponseStatus.Unauthorized, result.Status);
    }
}
