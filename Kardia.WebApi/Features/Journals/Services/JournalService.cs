using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Journals.Models.DTOs;
using Kardia.WebApi.Features.Journals.Repositories;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Users.Repositories;

namespace Kardia.WebApi.Features.Journals.Services;

/// <summary>
/// The journal: habits per day, and what each one does to the next morning's recovery.
/// </summary>
internal interface IJournalService
{
    /// <summary>Gets a day's journal and the effects.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The day (today when null).</param>
    /// <returns>The journal.</returns>
    Task<ServiceApiResponse<JournalDayDTO>> GetAsync(string userId, DateOnly? date);

    /// <summary>Marks or unmarks a habit.</summary>
    /// <param name="item">The habit, day and state.</param>
    /// <returns>The day's journal.</returns>
    Task<ServiceApiResponse<JournalDayDTO>> SetAsync(JournalSetRequest item);
}

/// <summary>
/// <see cref="IJournalService"/> over the journal and the day scores.
/// </summary>
/// <param name="journalRepository">Journal.</param>
/// <param name="userRepository">Accounts.</param>
/// <param name="profileRepository">Profiles.</param>
/// <param name="dayScoreReader">Day scores.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="clock">The app's clock.</param>
internal sealed class JournalService(
    IJournalRepository journalRepository,
    IUserRepository userRepository,
    IProfileRepository profileRepository,
    IDayScoreReader dayScoreReader,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock) : IJournalService
{
    private const int LookBackDays = 90;

    private readonly IJournalRepository _journalRepository = journalRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IProfileRepository _profileRepository = profileRepository;
    private readonly IDayScoreReader _dayScoreReader = dayScoreReader;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly IAppClock _clock = clock;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<JournalDayDTO>> GetAsync(string userId, DateOnly? date)
    {
        if (!Guid.TryParse(userId, out var id) || await _userRepository.GetAsync(id) is null)
        {
            return ServiceApiResponse<JournalDayDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        var today = _clock.Today;
        return ServiceApiResponse<JournalDayDTO>.Success(await BuildAsync(id, date is { } day && day <= today ? day : today));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<JournalDayDTO>> SetAsync(JournalSetRequest item)
    {
        if (!Guid.TryParse(item.UserId, out var id) || await _userRepository.GetAsync(id) is null)
        {
            return ServiceApiResponse<JournalDayDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.UserId ?? string.Empty));
        }

        var today = _clock.Today;
        if (!Enum.IsDefined(item.Tag))
        {
            var error = await _globalizationHelper.ValidationErrorAsync("Tag", nameof(TranslationKeys.UnknownTag), item.Tag);
            return ServiceApiResponse<JournalDayDTO>.ValidationProblems(error.Message).AddError(error.Field, error.Message);
        }

        if (item.Date > today || item.Date < today.AddDays(-LookBackDays))
        {
            var error = await _globalizationHelper.ValidationErrorAsync("Date", nameof(TranslationKeys.OutOfRange), today.AddDays(-LookBackDays), today);
            return ServiceApiResponse<JournalDayDTO>.ValidationProblems(error.Message).AddError(error.Field, error.Message);
        }

        await _journalRepository.SetAsync(id, item.Date, item.Tag.ToString(), item.On);
        return ServiceApiResponse<JournalDayDTO>.Success(await BuildAsync(id, item.Date));
    }

    private async Task<JournalDayDTO> BuildAsync(Guid userId, DateOnly day)
    {
        var today = _clock.Today;
        var from = today.AddDays(-LookBackDays);
        var entries = await _journalRepository.ListAsync(userId, from, today);
        var facts = ProfileFacts.From(await _profileRepository.GetAsync(userId), today);
        var scores = await _dayScoreReader.ScoresAsync(userId, facts, from, today);
        var marked = entries.Where(entry => DateOnly.FromDateTime(entry.DayDate) == day).Select(static entry => entry.Tag).ToHashSet(StringComparer.Ordinal);

        return new JournalDayDTO
        {
            Date = day,
            Tags = Enum.GetValues<JournalTag>().Select(tag => new JournalTagDTO { Tag = tag, Name = JournalText.Name(tag), On = marked.Contains(tag.ToString()) }).ToList(),
            Impacts = JournalImpacts.Calculate(entries.Select(static entry => (DateOnly.FromDateTime(entry.DayDate), entry.Tag)), scores)
                .Select(static impact => new JournalImpactDTO
                {
                    Tag = impact.Tag,
                    Name = JournalText.Name(impact.Tag),
                    DaysWith = impact.DaysWith,
                    RecoveryWith = impact.RecoveryWith,
                    RecoveryWithout = impact.RecoveryWithout,
                    Difference = impact.Difference,
                }).ToList(),
            DaysLogged = entries.Select(static entry => entry.DayDate.Date).Distinct().Count(),
        };
    }
}
