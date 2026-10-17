using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Days.Models.DTOs;
using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Journals.Repositories;
using Kardia.WebApi.Features.Journals.Services;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Settings.Models.Entities;
using Kardia.WebApi.Features.Settings.Repositories;
using Kardia.WebApi.Features.Syncs.Models.Entities;
using Kardia.WebApi.Features.Syncs.Repositories;
using Kardia.WebApi.Features.Users.Repositories;

namespace Kardia.WebApi.Features.Days.Services;

/// <summary>
/// A day as the screens show it, and ranges of days for charts.
/// </summary>
internal interface IDayService
{
    /// <summary>Gets one day: recovery, sleep, strain, heart rate, the last two weeks and the coach.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The day (today when null).</param>
    /// <returns>The day.</returns>
    Task<ServiceApiResponse<DayDTO>> GetAsync(string userId, DateOnly? date);

    /// <summary>Gets a range of days in short.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day (at most 400 days after the first).</param>
    /// <returns>One summary per day.</returns>
    Task<ServiceApiResponse<List<DaySummaryDTO>>> RangeAsync(string userId, DateOnly from, DateOnly to);

    /// <summary>Gets the Health Monitor over the last 30 or 180 days (the printable report).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="days">30 or 180 (anything else becomes the nearer of the two).</param>
    /// <returns>One series per sign.</returns>
    Task<ServiceApiResponse<HealthReportDTO>> HealthReportAsync(string userId, int days);
}

/// <summary>
/// Builds days from the health data with <see cref="DayScoreBuilder"/>, then adds what only the screen needs (heart
/// rate curve, workouts with their own strain, tonight's bedtime, the coach).
/// </summary>
/// <param name="userRepository">Accounts.</param>
/// <param name="profileRepository">Profiles.</param>
/// <param name="healthDataRepository">Health data.</param>
/// <param name="syncKeyRepository">Sync keys (the last sync time).</param>
/// <param name="journalRepository">Journal (its biggest effect feeds the coach).</param>
/// <param name="dayScoreReader">Scores ranges of days.</param>
/// <param name="settingRepository">Goals (the sleep goal sets tonight's bedtime).</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="clock">The app's clock.</param>
internal sealed class DayService(
    IUserRepository userRepository,
    IProfileRepository profileRepository,
    IHealthDataRepository healthDataRepository,
    ISyncKeyRepository syncKeyRepository,
    IJournalRepository journalRepository,
    IDayScoreReader dayScoreReader,
    ISettingRepository settingRepository,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock) : IDayService
{
    /// <summary>The days scored for one day's screen (trends, journal effects).</summary>
    public const int ScoredDays = 90;

    private const int MaxRangeDays = 400;

    private readonly IUserRepository _userRepository = userRepository;
    private readonly IProfileRepository _profileRepository = profileRepository;
    private readonly IHealthDataRepository _healthDataRepository = healthDataRepository;
    private readonly ISyncKeyRepository _syncKeyRepository = syncKeyRepository;
    private readonly IJournalRepository _journalRepository = journalRepository;
    private readonly IDayScoreReader _dayScoreReader = dayScoreReader;
    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly IAppClock _clock = clock;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<DayDTO>> GetAsync(string userId, DateOnly? date)
    {
        if (await UserAsync(userId) is not { } id)
        {
            return ServiceApiResponse<DayDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        var today = _clock.Today;
        var day = date is { } given && given <= today ? given : today;
        var isToday = day == today;
        var now = _clock.Now;
        var facts = ProfileFacts.From(await _profileRepository.GetAsync(id), today);

        var first = day.AddDays(-(ScoredDays - 1));
        var scores = await _dayScoreReader.ScoresAsync(id, facts, first, day);
        var score = scores[^1];
        var recent = scores.TakeLast(31).SkipLast(1).ToList();

        var samples = await _healthDataRepository.HeartRatesListAsync(id, day.ToDateTime(TimeOnly.MinValue), day.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var workouts = await _healthDataRepository.WorkoutsListAsync(id, day, day);
        var key = await _syncKeyRepository.GetAsync(id);
        var journal = await _journalRepository.ListAsync(id, first, day);
        var impact = JournalImpacts.Calculate(journal.Select(static entry => (DateOnly.FromDateTime(entry.DayDate), entry.Tag)), scores).FirstOrDefault();

        // Tonight: the need after today's strain and the recent shortfalls, and when to be in bed for the usual wake-up time.
        var tonight = SleepScore.Need(facts.SleepNeedMinutes, score.Strain, scores.TakeLast(3).Reverse().Select(static s => s.ShortfallMinutes));
        var nights = scores.Where(static s => s.Night is not null).Select(static s => s.Night!).TakeLast(7).ToList();
        var setting = await _settingRepository.GetAsync(id) ?? UserSetting.Defaults;
        var tonightGoal = SleepScore.Goal(tonight.TotalMinutes, setting.SleepGoalPercent);
        var bedtime = isToday ? SleepScore.Bedtime(nights, tonightGoal, day) : null;
        var sleep = Sleep(score, tonight, bedtime);
        sleep.GoalPercent = setting.SleepGoalPercent;
        sleep.TonightGoalMinutes = tonightGoal;

        var dto = new DayDTO
        {
            Date = day,
            IsToday = isToday,
            HasData = score.HasData || samples.Count > 0,
            LastSyncAt = key?.LastUsedAt is { } used ? new DateTimeOffset(DateTime.SpecifyKind(used, DateTimeKind.Utc)) : null,
            Recovery = Recovery(score),
            Sleep = sleep,
            Strain = Strain(score, facts, samples, workouts, isToday ? now : null),
            HeartRates = Curve(samples),
            Recent = scores.TakeLast(14).Select(Summary).ToList(),
            Health = Health(score),
            CardioLoad = Cardio(scores),
            HrvTrend = scores.TakeLast(30).Select(static s => new TrendPointDTO { Date = s.Date, Value = s.Value(HealthMetric.Hrv) }).ToList(),
            Coach = CoachWriter.Write(new CoachContext(score, recent, isToday, now, tonight, bedtime?.Bedtime, impact)),
        };
        return ServiceApiResponse<DayDTO>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<DaySummaryDTO>>> RangeAsync(string userId, DateOnly from, DateOnly to)
    {
        if (await UserAsync(userId) is not { } id)
        {
            return ServiceApiResponse<List<DaySummaryDTO>>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        if (to < from || to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            var message = await _globalizationHelper.ValidationErrorAsync("Date", nameof(TranslationKeys.OutOfRange), 1, MaxRangeDays);
            return ServiceApiResponse<List<DaySummaryDTO>>.ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems))).AddError(message.Field, message.Message);
        }

        var facts = ProfileFacts.From(await _profileRepository.GetAsync(id), _clock.Today);
        var scores = await _dayScoreReader.ScoresAsync(id, facts, from, to);
        return ServiceApiResponse<List<DaySummaryDTO>>.Success(scores.Select(Summary).ToList());
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<HealthReportDTO>> HealthReportAsync(string userId, int days)
    {
        if (await UserAsync(userId) is not { } id)
        {
            return ServiceApiResponse<HealthReportDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), userId ?? string.Empty));
        }

        days = days > 90 ? 180 : 30;
        var today = _clock.Today;
        var from = today.AddDays(-(days - 1));
        var facts = ProfileFacts.From(await _profileRepository.GetAsync(id), today);
        var scores = await _dayScoreReader.ScoresAsync(id, facts, from, today);
        var perDay = scores.Select(static score => (score.Date, Signs: HealthMonitor.Of(score))).ToList();
        var series = perDay[^1].Signs.Select((last, index) =>
        {
            var points = perDay.Select(day => (day.Date, Sign: day.Signs[index])).ToList();
            var values = points.Where(static p => p.Sign.Value is not null).Select(static p => p.Sign.Value!.Value).ToList();
            return new HealthSeriesDTO
            {
                Key = last.Key,
                Name = last.Name,
                Unit = last.Unit,
                Decimals = last.Decimals,
                Average = values.Count > 0 ? Math.Round(values.Average(), last.Decimals) : null,
                Min = values.Count > 0 ? values.Min() : null,
                Max = values.Count > 0 ? values.Max() : null,
                Low = last.Low,
                High = last.High,
                InRangeDays = points.Count(static p => p.Sign.Status is HealthSignStatus.InRange),
                OutsideDays = points.Count(static p => p.Sign.Status is HealthSignStatus.Outside or HealthSignStatus.FarOutside),
                Days = points.Select(static p => new HealthPointDTO { Date = p.Date, Value = p.Sign.Value, Low = p.Sign.Low, High = p.Sign.High, Status = p.Sign.Status }).ToList(),
            };
        }).ToList();

        return ServiceApiResponse<HealthReportDTO>.Success(new HealthReportDTO
        {
            From = from,
            To = today,
            DaysMeasured = perDay.Count(static day => day.Signs.Any(static sign => sign.Value is not null)),
            DaysFlagged = perDay.Count(static day => HealthMonitor.Outside(day.Signs) >= 2),
            Signs = series,
        });
    }

    /// <summary>The Health Monitor of a day.</summary>
    /// <param name="score">The day.</param>
    /// <returns>The monitor.</returns>
    internal static HealthMonitorDTO Health(DayScore score)
    {
        var signs = HealthMonitor.Of(score);
        var judged = signs.Count(static sign => sign.Status is not HealthSignStatus.None);
        return new HealthMonitorDTO
        {
            Ready = judged > 0,
            Judged = judged,
            InRange = signs.Count(static sign => sign.Status is HealthSignStatus.InRange),
            Signs = signs.Select(static sign => new HealthSignDTO
            {
                Key = sign.Key,
                Name = sign.Name,
                Value = sign.Value,
                Unit = sign.Unit,
                Typical = sign.Typical,
                Low = sign.Low,
                High = sign.High,
                Status = sign.Status,
                Decimals = sign.Decimals,
            }).ToList(),
        };
    }

    /// <summary>The cardio load on the last day of a list, with the ratio of the 8 weeks before.</summary>
    /// <param name="scores">Scored days, oldest first.</param>
    /// <returns>The load.</returns>
    internal static CardioLoadDTO Cardio(IReadOnlyList<DayScore> scores)
    {
        var load = CardioLoad.Of(scores);
        var history = new List<TrendPointDTO>();
        for (var end = Math.Max(CardioLoad.ChronicDays, scores.Count - 56); end <= scores.Count; end++)
        {
            var window = scores.Take(end).ToList();
            history.Add(new TrendPointDTO { Date = window[^1].Date, Value = CardioLoad.Of(window).Ratio });
        }

        return new CardioLoadDTO
        {
            Ready = load.Ratio is not null,
            Ratio = load.Ratio,
            Status = load.Status,
            AcuteLoad = load.Acute,
            ChronicLoad = load.Chronic,
            StrainLow = load.StrainLow,
            StrainHigh = load.StrainHigh,
            History = history,
        };
    }

    /// <summary>The minutes in heart-rate zone 2 or higher of a day.</summary>
    /// <param name="score">The day.</param>
    /// <returns>The minutes.</returns>
    internal static int ActiveMinutes(DayScore score) => (int)Math.Round((score.Load?.ZoneSeconds ?? string.Empty)
        .Split(',').Skip(1).Sum(static part => int.TryParse(part, out var seconds) ? seconds : 0) / 60.0);

    private static RecoveryDTO Recovery(DayScore score) => new()
    {
        Score = score.Recovery.Score,
        Zone = score.Recovery.Zone,
        Hrv = Round(score.Value(HealthMetric.Hrv), 0),
        HrvBaseline = Math.Round(score.HrvBaseline.Mean, 0),
        RestingHeartRate = Round(score.Value(HealthMetric.RestingHeartRate), 0),
        RestingBaseline = Math.Round(score.RestingBaseline.Mean, 0),
        RespiratoryRate = Round(score.Value(HealthMetric.RespiratoryRate), 1),
        RespiratoryBaseline = Math.Round(score.RespiratoryBaseline.Mean, 1),
        BloodOxygen = Round(score.Value(HealthMetric.BloodOxygen), 0),
        WristTemperatureChange = score.TemperatureBaseline.Count >= 4 && score.Value(HealthMetric.WristTemperature) is { } temperature
            ? Math.Round(temperature - score.TemperatureBaseline.Mean, 1)
            : null,
        SleepPerformance = score.SleepPerformance,
        TargetLow = score.Target.Low,
        TargetHigh = score.Target.High,
    };

    private static SleepDTO Sleep(DayScore score, SleepNeed tonightNeed, (DateTime Bedtime, DateTime WakeAt)? bedtime)
    {
        var sleep = new SleepDTO
        {
            HasNight = score.Night is not null,
            Performance = score.SleepPerformance,
            NeedMinutes = score.Need.TotalMinutes,
            NeedBaseMinutes = score.Need.BaseMinutes,
            NeedStrainMinutes = score.Need.StrainMinutes,
            NeedDebtMinutes = score.Need.DebtMinutes,
            Efficiency = score.SleepEfficiency,
            Consistency = score.SleepConsistency,
            DebtMinutes = score.ShortfallMinutes,
            RespiratoryRate = Round(score.Value(HealthMetric.RespiratoryRate), 1),
            TonightNeedMinutes = tonightNeed.TotalMinutes,
            TonightStrainMinutes = tonightNeed.StrainMinutes,
            TonightDebtMinutes = tonightNeed.DebtMinutes,
            Bedtime = bedtime?.Bedtime,
            WakeAt = bedtime?.WakeAt,
        };

        if (score.Night is { } night)
        {
            var segments = SleepScore.Segments(night);
            sleep.AsleepMinutes = score.AsleepMinutes ?? 0;
            sleep.InBedMinutes = (int)Math.Round((night.BedEnd - night.BedStart).TotalMinutes);
            sleep.BedStart = night.BedStart;
            sleep.BedEnd = night.BedEnd;
            sleep.AwakeMinutes = night.AwakeMinutes;
            sleep.RemMinutes = night.RemMinutes;
            sleep.CoreMinutes = night.CoreMinutes;
            sleep.DeepMinutes = night.DeepMinutes;
            sleep.Cycles = SleepScore.Cycles(segments);
            sleep.Segments = segments.Select(static segment => new SleepSegmentDTO { Stage = segment.Stage, Start = segment.Start, End = segment.End }).ToList();
        }

        return sleep;
    }

    private static StrainDTO Strain(DayScore score, ProfileFacts facts, List<HeartRateRow> samples, List<WorkoutRow> workouts, DateTime? now)
    {
        var zones = (score.Load?.ZoneSeconds ?? "0,0,0,0,0").Split(',').Select(static part => int.TryParse(part, out var seconds) ? (int)Math.Round(seconds / 60.0) : 0).ToList();
        while (zones.Count < 5)
        {
            zones.Add(0);
        }

        var active = score.Value(HealthMetric.ActiveEnergy);
        var resting = score.Value(HealthMetric.BasalEnergy)
            ?? (StrainScore.RestingCalories(score.Value(HealthMetric.BodyMass) ?? facts.WeightKg, facts.HeightCm, facts.Age, facts.Sex) is { } perDay
                ? perDay * (now is { } clock ? clock.TimeOfDay.TotalHours / 24 : 1)
                : null);
        var rest = score.Value(HealthMetric.RestingHeartRate) ?? score.RestingBaseline.Mean;

        return new StrainDTO
        {
            Score = score.Strain,
            TargetLow = score.Target.Low,
            TargetHigh = score.Target.High,
            ActiveKcal = Round(active, 0),
            TotalKcal = active is null && resting is null ? null : Math.Round((active ?? 0) + (resting ?? 0)),
            Steps = Round(score.Value(HealthMetric.Steps), 0),
            MaxBpm = score.Load?.MaxBpm is { } loadMax ? loadMax : samples.Count > 0 ? samples.Max(static s => (int)s.Bpm) : null,
            AvgBpm = score.Load?.AvgBpm is { } loadAvg ? loadAvg : samples.Count > 0 ? (int)Math.Round(samples.Average(static s => s.Bpm)) : null,
            MinBpm = samples.Count > 0 ? samples.Min(static s => (int)s.Bpm) : null,
            CurrentBpm = samples.Count > 0 ? samples[^1].Bpm : null,
            ZoneMinutes = zones.Take(5).ToList(),
            MaxHeartRate = facts.MaxHeartRate,
            Workouts = workouts.Select(workout =>
            {
                var inside = samples.Where(sample => sample.MinuteAt >= workout.StartedAt && sample.MinuteAt <= workout.EndedAt).ToList();
                var load = StrainScore.Load(inside, rest, facts.MaxHeartRate, facts.Sex);
                return new WorkoutDTO
                {
                    Kind = workout.Kind,
                    StartedAt = workout.StartedAt,
                    Minutes = (int)Math.Round((workout.EndedAt - workout.StartedAt).TotalMinutes),
                    ActiveKcal = workout.ActiveKcal is { } kcal ? (double)kcal : null,
                    DistanceKm = workout.DistanceKm is { } km ? (double)km : null,
                    AvgBpm = workout.AvgBpm ?? load.AvgBpm,
                    MaxBpm = workout.MaxBpm ?? load.MaxBpm,
                    Strain = inside.Count > 0 ? StrainScore.FromTrimp(load.Trimp) : null,
                    HeartRates = Thin(inside.Select(static sample => (int)sample.Bpm).ToList(), 90),
                };
            }).ToList(),
        };
    }

    /// <summary>The day's heart rate in 5-minute averages.</summary>
    private static List<HeartRatePointDTO> Curve(List<HeartRateRow> samples) => samples
        .GroupBy(static sample => new DateTime(sample.MinuteAt.Year, sample.MinuteAt.Month, sample.MinuteAt.Day, sample.MinuteAt.Hour, sample.MinuteAt.Minute / 5 * 5, 0))
        .Select(static group => new HeartRatePointDTO { At = group.Key, Bpm = (int)Math.Round(group.Average(static sample => sample.Bpm)) })
        .ToList();

    private static List<int> Thin(List<int> values, int max)
    {
        if (values.Count <= max)
        {
            return values;
        }

        var step = values.Count / (double)max;
        return Enumerable.Range(0, max).Select(index => values[(int)(index * step)]).ToList();
    }

    /// <summary>A day in short.</summary>
    /// <param name="score">The day.</param>
    /// <returns>The summary.</returns>
    internal static DaySummaryDTO Summary(DayScore score) => new()
    {
        Date = score.Date,
        Recovery = score.Recovery.Score,
        Zone = score.Recovery.Zone,
        Strain = score.Strain,
        SleepPerformance = score.SleepPerformance,
        AsleepMinutes = score.AsleepMinutes,
        Hrv = Round(score.Value(HealthMetric.Hrv), 0),
        RestingHeartRate = Round(score.Value(HealthMetric.RestingHeartRate), 0),
        RespiratoryRate = Round(score.Value(HealthMetric.RespiratoryRate), 1),
        BloodOxygen = Round(score.Value(HealthMetric.BloodOxygen), 0),
        WristTemperatureChange = score.TemperatureBaseline.Count >= 4 && score.Value(HealthMetric.WristTemperature) is { } temperature
            ? Math.Round(temperature - score.TemperatureBaseline.Mean, 1)
            : null,
        Vo2Max = Round(score.Value(HealthMetric.Vo2Max), 1),
        Steps = Round(score.Value(HealthMetric.Steps), 0),
        ActiveMinutes = ActiveMinutes(score),
        NeedMinutes = score.Need.TotalMinutes,
        BedStart = score.Night?.BedStart,
        HealthFlags = HealthMonitor.Of(score).Where(static sign => sign.Status is HealthSignStatus.Outside or HealthSignStatus.FarOutside).Select(static sign => sign.Key).ToList(),
    };

    private static double? Round(double? value, int digits) => value is { } number ? Math.Round(number, digits) : null;

    private async Task<Guid?> UserAsync(string userId) =>
        Guid.TryParse(userId, out var id) && await _userRepository.GetAsync(id) is not null ? id : null;
}
