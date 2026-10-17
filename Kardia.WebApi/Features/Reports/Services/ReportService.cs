using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Reports.Models.DTOs;
using Kardia.WebApi.Features.Users.Repositories;

namespace Kardia.WebApi.Features.Reports.Services;

/// <summary>Weekly and monthly performance assessments.</summary>
internal interface IReportService
{
    /// <summary>Gets the report of the week or month around a day.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="period">Week or month.</param>
    /// <param name="date">A day inside it (today when null).</param>
    /// <returns>The report.</returns>
    Task<ServiceApiResponse<ReportDTO>> GetAsync(string userId, ReportPeriod period, DateOnly? date);
}

/// <summary>Reports from the scored days (<see cref="PerformanceReport"/>).</summary>
/// <param name="userRepository">Accounts.</param>
/// <param name="profileRepository">Profiles.</param>
/// <param name="dayScoreReader">Scores days.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="clock">The app's clock.</param>
internal sealed class ReportService(
    IUserRepository userRepository,
    IProfileRepository profileRepository,
    IDayScoreReader dayScoreReader,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock) : IReportService
{
    /// <inheritdoc />
    public async Task<ServiceApiResponse<ReportDTO>> GetAsync(string userId, ReportPeriod period, DateOnly? date)
    {
        if (!Guid.TryParse(userId, out var id) || await userRepository.GetAsync(id) is null)
        {
            return ServiceApiResponse<ReportDTO>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        var today = clock.Today;
        var day = date is { } given && given <= today ? given : today;
        var (_, to, previousFrom, _) = PerformanceReport.Range(period, day);
        var facts = ProfileFacts.From(await profileRepository.GetAsync(id), today);
        var scores = await dayScoreReader.ScoresAsync(id, facts, previousFrom.AddDays(-28), to < today ? to : today);
        return ServiceApiResponse<ReportDTO>.Success(PerformanceReport.Build(period, day, scores, today));
    }
}
