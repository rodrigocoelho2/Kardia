using System.Collections.Concurrent;
using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.AiCoach.Models.DTOs;
using Kardia.WebApi.Features.Days.Services;
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
    /// <summary>Gets the state and the last report.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    Task<ServiceApiResponse<AiCoachDTO>> GetAsync(string userId);

    /// <summary>Starts an analysis (it runs in the background; poll <see cref="GetAsync"/>).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    Task<ServiceApiResponse<AiCoachDTO>> AnalyseAsync(string userId);
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
    IAiModelClient modelClient,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock,
    TimeProvider timeProvider,
    IOptions<KardiaOptions> options,
    ILogger<AiCoachService> logger) : IAiCoachService
{
    private readonly ConcurrentDictionary<Guid, State> _states = new();

    /// <inheritdoc />
    public async Task<ServiceApiResponse<AiCoachDTO>> GetAsync(string userId) => await UserAsync(userId) is { } id
        ? ServiceApiResponse<AiCoachDTO>.Success(ToDTO(_states.GetValueOrDefault(id)))
        : ServiceApiResponse<AiCoachDTO>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));

    /// <inheritdoc />
    public async Task<ServiceApiResponse<AiCoachDTO>> AnalyseAsync(string userId)
    {
        if (await UserAsync(userId) is not { } id)
        {
            return ServiceApiResponse<AiCoachDTO>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        if (!modelClient.Enabled)
        {
            return ServiceApiResponse<AiCoachDTO>.Success(ToDTO(null));
        }

        var now = timeProvider.GetUtcNow();
        var state = _states.AddOrUpdate(
            id,
            _ => new State { Running = true, StartedAt = now },
            (_, old) => old.Running ? old : new State { Running = true, StartedAt = now, Report = old.Report });
        if (state.StartedAt == now)
        {
            _ = Task.Run(() => RunAsync(id, userId));
        }

        return ServiceApiResponse<AiCoachDTO>.Success(ToDTO(state));
    }

    /// <summary>Gathers the numbers, asks the model and keeps the report.</summary>
    /// <param name="id">The account.</param>
    /// <param name="userId">The account, as sent.</param>
    /// <returns>The finished state.</returns>
    internal async Task<AiCoachDTO> RunAsync(Guid id, string userId)
    {
        State finished;
        try
        {
            var today = clock.Today;
            var day = (await dayService.GetAsync(userId, today)).Result ?? throw new InvalidOperationException("No day.");
            var days = (await dayService.RangeAsync(userId, today.AddDays(-29), today)).Result ?? [];
            var plan = (await planService.GetAsync(userId)).Result;
            var week = (await reportService.GetAsync(userId, ReportPeriod.Week, today)).Result;
            var facts = ProfileFacts.From(await profileRepository.GetAsync(id), today);
            var input = new AiCoachInput(facts.Age is { } age ? (int)Math.Floor(age) : null, facts.Sex, day, days, plan, week);

            var answer = await modelClient.CompleteAsync(AiPrompt.System(options.Value.AiLanguage), AiPrompt.Data(input), CancellationToken.None);
            var report = AiPrompt.Parse(answer);
            report.GeneratedAt = timeProvider.GetUtcNow();
            report.DaysAnalysed = days.Count(static d => d.Recovery is not null || d.Strain is not null || d.AsleepMinutes is not null);
            finished = new State { Report = report };
            logger.LogInformation("AI coach: report written by {Provider} {Model}", modelClient.Provider, modelClient.Model);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "AI coach: the analysis failed");
            finished = new State
            {
                Report = _states.GetValueOrDefault(id)?.Report,
                Error = exception is HttpRequestException or TaskCanceledException
                    ? $"Couldn't reach the AI ({modelClient.Provider}): {exception.Message}"
                    : "The analysis failed. Try again in a minute.",
            };
        }

        _states[id] = finished;
        return ToDTO(finished);
    }

    private AiCoachDTO ToDTO(State? state) => new()
    {
        Enabled = modelClient.Enabled,
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
