using Kardia.WebApi.Features.AiCoach.Services;
using Kardia.WebApi.Features.Plans.Services;
using Kardia.WebApi.Features.Reports.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kardia.UnitTest.AiCoach;

/// <summary>The AI coach: what it sends (numbers only), how it reads the answer, and the background run.</summary>
public sealed class AiCoachTests
{
    [Fact]
    public void Parse_ReadsJson_EvenInACodeFence()
    {
        var report = AiPrompt.Parse("""
            Here you go:
            ```json
            {"headline": "Sleep is the lever", "summary": "You slept 6h40 a night.", "wentWell": ["HRV up"], "watch": ["Late nights"], "actions": ["Bed by 23:00", "Easy Tuesday", "Walk daily"]}
            ```
            """);

        Assert.Equal("Sleep is the lever", report.Headline);
        Assert.Equal(["HRV up"], report.WentWell);
        Assert.Equal(3, report.Actions.Count);
    }

    [Fact]
    public void Parse_KeepsPlainText_AsTheSummary()
    {
        var report = AiPrompt.Parse("Sleep more this week.");

        Assert.Equal("Sleep more this week.", report.Summary);
        Assert.Empty(report.Actions);
    }

    [Fact]
    public async Task Analyse_RunsInTheBackground_AndSendsOnlyNumbers()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync(name: "rita");
        var model = new FakeModel("""{"headline":"Solid week","summary":"Recovery held up.","wentWell":[],"watch":[],"actions":["a","b","c"]}""");
        var service = Service(app, model);

        var started = (await service.AnalyseAsync(user)).Result!;
        var finished = await service.RunAsync(Guid.Parse(user), user);

        Assert.True(started.Enabled);
        Assert.True(started.Running);
        Assert.Equal("Solid week", finished.Report!.Headline);
        Assert.False(finished.Running);
        Assert.True(finished.Report.DaysAnalysed >= 25);
        Assert.Equal("Solid week", (await service.GetAsync(user)).Result!.Report!.Headline);
        Assert.False(model.User.Contains("rita", StringComparison.OrdinalIgnoreCase));
        Assert.False(model.User.Contains(user, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Last 30 days", model.User);
        Assert.Contains("Cardio load", model.User);
        Assert.Contains("Portuguese", model.System);
    }

    [Fact]
    public async Task EachScreen_GetsItsOwnReport_WithItsOwnFocus()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync();
        var model = new FakeModel("""{"headline":"Age","summary":"s","wentWell":[],"watch":[],"actions":["a"]}""");
        var service = Service(app, model);
        var yesterday = TestSupport.Today.AddDays(-1);

        await service.RunAsync(Guid.Parse(user), user, Kardia.Library.Enums.AiCoachTopic.Age);
        Assert.Contains("Biological age screen", model.System);
        Assert.Contains("Biological age:", model.User);

        await service.RunAsync(Guid.Parse(user), user, Kardia.Library.Enums.AiCoachTopic.Sleep, yesterday);
        Assert.Contains("Sleep screen", model.System);
        Assert.Contains("a past day", model.User);

        await service.RunAsync(Guid.Parse(user), user, Kardia.Library.Enums.AiCoachTopic.Trends);
        Assert.Contains("Last 90 days", model.User);

        var age = (await service.GetAsync(user, Kardia.Library.Enums.AiCoachTopic.Age)).Result!;
        var sleep = (await service.GetAsync(user, Kardia.Library.Enums.AiCoachTopic.Sleep, yesterday)).Result!;
        var overview = (await service.GetAsync(user)).Result!;
        Assert.NotNull(age.Report);
        Assert.NotNull(sleep.Report);
        Assert.Equal(yesterday, sleep.Date);
        Assert.Null(overview.Report);
    }

    [Fact]
    public async Task WithoutAProvider_TheCoachIsOff()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync();
        var client = new AiModelClient(TestSupport.Options());

        var state = (await Service(app, client).AnalyseAsync(user)).Result!;

        Assert.False(state.Enabled);
        Assert.False(state.Running);
        Assert.True(new AiModelClient(TestSupport.Options(configure: static o => { o.AiProvider = "ollama"; o.AiBaseUrl = "http://ollama:11434/v1"; })).Enabled);
        Assert.Equal("claude-haiku-5-5", new AiModelClient(TestSupport.Options(configure: static o => { o.AiProvider = "anthropic"; o.AiApiKey = "k"; })).Model);
    }

    private static AiCoachService Service(TestApp app, IAiModelClient model) => new(
        app.Users, app.Profiles, app.DayService, app.PlanService, app.ReportService, app.AgeService, app.JournalService, model, new Kardia.WebApi.Extensions.Globalization.GlobalizationHelper(),
        app.Clock, app.Time, TestSupport.Options(configure: static o => o.AiLanguage = "Portuguese (Portugal)"), NullLogger<AiCoachService>.Instance);

    private sealed class FakeModel(string answer) : IAiModelClient
    {
        public string System { get; private set; } = string.Empty;

        public string User { get; private set; } = string.Empty;

        public bool Enabled => true;

        public string Provider => "anthropic";

        public string Model => "test";

        public Task<string> CompleteAsync(string system, string user, CancellationToken cancellationToken)
        {
            System = system;
            User = user;
            return Task.FromResult(answer);
        }
    }
}
