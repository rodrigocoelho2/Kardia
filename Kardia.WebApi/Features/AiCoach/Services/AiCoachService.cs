using System.Collections.Concurrent;
using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Ages.Services;
using Kardia.WebApi.Features.AiCoach.Models.DTOs;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Journals.Services;
using Kardia.WebApi.Features.Plans.Services;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Reports.Services;
using Kardia.WebApi.Features.Users.Repositories;
using Microsoft.Extensions.Options;

namespace Kardia.WebApi.Features.AiCoach.Services;

/// <summary>The AI coach: on request, sends a summary of the last 30 days to the AI provider and keeps its report.</summary>
internal interface IAiCoachService
{
    /// <summary>Gets the state and the last report of a topic and day.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="topic">What the report looks at.</param>
    /// <param name="date">The day (today when null).</param>
    /// <returns>The state.</returns>
    Task<ServiceApiResponse<AiCoachDTO>> GetAsync(string userId, AiCoachTopic topic = AiCoachTopic.Overview, DateOnly? date = null);

    /// <summary>Starts an analysis (it runs in the background; poll <see cref="GetAsync"/>).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="topic">What the report looks at.</param>
    /// <param name="date">The day (today when null).</param>
    /// <returns>The state.</returns>
    Task<ServiceApiResponse<AiCoachDTO>> AnalyseAsync(string userId, AiCoachTopic topic = AiCoachTopic.Overview, DateOnly? date = null);
}

/// <summary>
/// Runs analyses in the background (a local model can take minutes) and keeps the last report of each account in
/// memory. Only numbers go to the provider: no name, no account, no minute-by-minute heart rate.
/// </summary>
/// <param name="userRepository">Accounts.</param>
/// <param name="profileRepository">Profiles.</param>
/// <param name="dayService">Days.</param>
/// <param name="planService">The weekly plan.</param>
/// <param name="reportService">The weekly assessment.</param>
/// <param name="ageService">Biological age.</param>
/// <param name="journalService">The journal.</param>
/// <param name="modelClient">The AI provider.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="clock">The app's clock.</param>
/// <param name="timeProvider">UTC time.</param>
/// <param name="options">The options (language).</param>
/// <param name="logger">The logger.</param>
internal sealed class AiCoachService(
    IUserRepository userRepository,
    IProfileRepository profileRepository,
    IDayService dayService,
    IPlanService planService,
    IReportService reportService,
    IAgeService ageService,
    IJournalService journalService,
    IAiModelClient modelClient,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock,
    TimeProvider timeProvider,
    IOptions<KardiaOptions> options,
    ILogger<AiCoachService> logger) : IAiCoachService
{
    private readonly ConcurrentDictionary<(Guid User, AiCoachTopic Topic, DateOnly Day), State> _states = new();

    /// <inheritdoc />
    public async Task<ServiceApiResponse<AiCoachDTO>> GetAsync(string userId, AiCoachTopic topic = AiCoachTopic.Overview, DateOnly? date = null)
    {
        if (await UserAsync(userId) is not { } id)
        {
            return ServiceApiResponse<AiCoachDTO>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        var key = Key(id, topic, date);
        return ServiceApiResponse<AiCoachDTO>.Success(ToDTO(_states.GetValueOrDefault(key), key));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<AiCoachDTO>> AnalyseAsync(string userId, AiCoachTopic topic = AiCoachTopic.Overview, DateOnly? date = null)
    {
        if (await UserAsync(userId) is not { } id)
        {
            return ServiceApiResponse<AiCoachDTO>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        var key = Key(id, topic, date);
        if (!modelClient.Enabled)
        {
            return ServiceApiResponse<AiCoachDTO>.Success(ToDTO(null, key));
        }

        var now = timeProvider.GetUtcNow();
        var state = _states.AddOrUpdate(
            key,
            _ => new State { Running = true, StartedAt = now },
            (_, old) => old.Running ? old : new State { Running = true, StartedAt = now, Report = old.Report });
        if (state.StartedAt == now)
        {
            _ = Task.Run(() => RunAsync(id, userId, topic, key.Day));
        }

        return ServiceApiResponse<AiCoachDTO>.Success(ToDTO(state, key));
    }

    /// <summary>Gathers the numbers, asks the model and keeps the report.</summary>
    /// <param name="id">The account.</param>
    /// <param name="userId">The account, as sent.</param>
    /// <param name="topic">What the report looks at.</param>
    /// <param name="day">The day.</param>
    /// <returns>The finished state.</returns>
    internal async Task<AiCoachDTO> RunAsync(Guid id, string userId, AiCoachTopic topic = AiCoachTopic.Overview, DateOnly? day = null)
    {
        var key = Key(id, topic, day);
        State finished;
        try
        {
            var date = key.Day;
            var span = topic == AiCoachTopic.Trends ? 90 : 30;
            var dayDto = (await dayService.GetAsync(userId, date)).Result ?? throw new InvalidOperationException("No day.");
            var days = (await dayService.RangeAsync(userId, date.AddDays(-(span - 1)), date)).Result ?? [];
            var plan = (await planService.GetAsync(userId)).Result;
            var week = (await reportService.GetAsync(userId, ReportPeriod.Week, date)).Result;
            var facts = ProfileFacts.From(await profileRepository.GetAsync(id), clock.Today);
            var input = new AiCoachInput(facts.Age is { } age ? (int)Math.Floor(age) : null, facts.Sex, dayDto, days, plan, week)
            {
                Topic = topic,
                BiologicalAge = topic is AiCoachTopic.Age or AiCoachTopic.Overview ? (await ageService.GetAsync(userId)).Result : null,
                Journal = topic is AiCoachTopic.Journal or AiCoachTopic.Recovery or AiCoachTopic.Health or AiCoachTopic.Sleep or AiCoachTopic.Stress ? (await journalService.GetAsync(userId, date)).Result : null,
                HealthMonth = topic is AiCoachTopic.Health ? (await dayService.HealthReportAsync(userId, 30)).Result : null,
                Month = topic is AiCoachTopic.Report or AiCoachTopic.Trends ? (await reportService.GetAsync(userId, ReportPeriod.Month, date)).Result : null,
            };

            var answer = await modelClient.CompleteAsync(AiPrompt.System(options.Value.AiLanguage, topic), AiPrompt.Data(input), CancellationToken.None);
            var report = AiPrompt.Parse(answer);
            report.GeneratedAt = timeProvider.GetUtcNow();
            report.DaysAnalysed = days.Count(static d => d.Recovery is not null || d.Strain is not null || d.AsleepMinutes is not null);
            finished = new State { Report = report };
            logger.LogInformation("AI coach: {Topic} report written by {Provider} {Model}", topic, modelClient.Provider, modelClient.Model);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "AI coach: the analysis failed");
            finished = new State
            {
                Report = _states.GetValueOrDefault(key)?.Report,
                Error = exception is HttpRequestException or TaskCanceledException
                    ? $"Couldn't reach the AI ({modelClient.Provider}): {exception.Message}"
                    : "The analysis failed. Try again in a minute.",
            };
        }

        _states[key] = finished;
        return ToDTO(finished, key);
    }

    private (Guid User, AiCoachTopic Topic, DateOnly Day) Key(Guid id, AiCoachTopic topic, DateOnly? date) =>
        (id, topic, date is { } day && day <= clock.Today ? day : clock.Today);

    private AiCoachDTO ToDTO(State? state, (Guid User, AiCoachTopic Topic, DateOnly Day) key) => new()
    {
        Enabled = modelClient.Enabled,
        Topic = key.Topic,
        Date = key.Day,
        Provider = modelClient.Provider,
        Model = modelClient.Enabled ? modelClient.Model : string.Empty,
        Running = state?.Running == true,
        StartedAt = state?.Running == true ? state.StartedAt : null,
        Error = state?.Error ?? string.Empty,
        Report = state?.Report,
    };

    private async Task<Guid?> UserAsync(string userId) => Guid.TryParse(userId, out var id) && await userRepository.GetAsync(id) is not null ? id : null;

    private sealed class State
    {
        public bool Running { get; init; }

        public DateTimeOffset? StartedAt { get; init; }

        public string Error { get; init; } = string.Empty;

        public AiReportDTO? Report { get; init; }
    }
}
