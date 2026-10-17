using System.Globalization;
using System.Text;
using System.Text.Json;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.AiCoach.Models.DTOs;
using Kardia.WebApi.Features.Ages.Models.DTOs;
using Kardia.WebApi.Features.Days.Models.DTOs;
using Kardia.WebApi.Features.Journals.Models.DTOs;
using Kardia.WebApi.Features.Plans.Models.DTOs;
using Kardia.WebApi.Features.Reports.Models.DTOs;

namespace Kardia.WebApi.Features.AiCoach.Services;

/// <summary>What the AI coach is shown: numbers only (no name, no account, no minute-by-minute data).</summary>
/// <param name="Age">Age in years, if known.</param>
/// <param name="Sex">Sex for reference values.</param>
/// <param name="Today">Today.</param>
/// <param name="Days">The last 30 days, oldest first.</param>
/// <param name="Plan">This week's plan.</param>
/// <param name="Week">This week's assessment.</param>
internal sealed record AiCoachInput(int? Age, BiologicalSex Sex, DayDTO Today, IReadOnlyList<DaySummaryDTO> Days, PlanDTO? Plan, ReportDTO? Week)
{
    /// <summary>Gets what the report looks at.</summary>
    public AiCoachTopic Topic { get; init; }

    /// <summary>Gets the biological age (the Age topic).</summary>
    public AgeDTO? BiologicalAge { get; init; }

    /// <summary>Gets the journal and what each habit does (the Journal topic).</summary>
    public JournalDayDTO? Journal { get; init; }

    /// <summary>Gets the Health Monitor over 30 days (the Health topic).</summary>
    public HealthReportDTO? HealthMonth { get; init; }

    /// <summary>Gets this month's assessment (the Report topic).</summary>
    public ReportDTO? Month { get; init; }
}

/// <summary>
/// Turns the numbers into the prompt, and the model's answer into a report. Pure, unit-tested.
/// </summary>
internal static class AiPrompt
{
    /// <summary>The instructions the model gets.</summary>
    /// <param name="language">The language to write in.</param>
    /// <param name="topic">The screen the report is for.</param>
    /// <returns>The system prompt.</returns>
    public static string System(string language, AiCoachTopic topic = AiCoachTopic.Overview) => Base(language) + Focus(topic);

    /// <summary>What to concentrate on for each screen.</summary>
    /// <param name="topic">The topic.</param>
    /// <returns>A paragraph for the instructions.</returns>
    public static string Focus(AiCoachTopic topic) => "\nFocus: " + topic switch
    {
        AiCoachTopic.Recovery => "this report is for the Recovery screen. Explain what moved the day's recovery (HRV, resting heart rate, respiratory rate, sleep, wrist temperature against the person's normal), how it compares with the last weeks, and how hard to train today.",
        AiCoachTopic.Sleep => "this report is for the Sleep screen. Look at the night (time asleep against the need, stages, efficiency, bedtime against their usual), the pattern of the last two weeks, sleep debt, and tonight's bedtime and sleep goal.",
        AiCoachTopic.Strain => "this report is for the Strain screen. Look at the day's strain against its target, the heart-rate zones and workouts, strain against recovery over the last weeks, the cardio load, and what to do today and this week.",
        AiCoachTopic.Age => "this report is for the Biological age screen. Explain which factors add years and which take years off, the pace of ageing, and which one or two factors would move it most.",
        AiCoachTopic.Health => "this report is for the Health Monitor screen. Look at which overnight signs are outside the typical range, since when, and together with which other changes (hard training, short or late sleep, journal habits like alcohol). Give everyday explanations only, never a diagnosis, and say when to see a doctor.",
        AiCoachTopic.Plan => "this report is for the Weekly plan screen. Look at the progress on each target with the days left, what's realistic to still hit and exactly how (which days, how many minutes or steps), and whether the targets fit the person.",
        AiCoachTopic.Trends => "this report is for the Trends screen, with 90 days of data. Describe the trends: what improved, what declined, any change of pace, and what explains them.",
        AiCoachTopic.Report => "this report is for the weekly and monthly assessment. Explain why the assessment came out as it did, the biggest differences with the period before, and what would raise it next week.",
        AiCoachTopic.Journal => "this report is for the Journal screen. Use the habits' effect on next-morning recovery: which help, which hurt, how confident that is (days logged), and which to change first.",
        AiCoachTopic.Stress => "this report is for the Stress & energy screen. Look at when in the day stress (heart rate above rest while still, 0-3) was high, how long, what may explain it (caffeine, work hours, short sleep, training the day before), how the energy bank was spent, and one or two ways to calm the evening (slow breathing, a walk, an earlier night).",
        _ => "an overview of how the person is doing across recovery, sleep, strain and health.",
    } + "\n";

    private static string Base(string language) => $$"""
        You are the coach inside Kardia, a self-hosted app like WHOOP that turns Apple Watch data into recovery (0-100 %,
        from HRV, resting heart rate, breathing and sleep against the person's own 30-day normal), sleep performance
        (sleep against the night's need), strain (0-21, heart load of the day) and cardio load (last 7 days of heart load
        divided by the last 28: below 0.8 detraining, 1.0-1.3 productive, above 1.5 too fast).

        Read the numbers you're given and write a short, plain report for the person. Rules:
        - Write in {{language}}. Talk to the person directly ("you"). Be specific: quote their numbers and dates.
        - Look for what changed and why it might have: sleep against need, late or irregular bedtimes, strain against
          recovery, HRV and resting heart rate trends, cardio load, the weekly plan.
        - Never diagnose or name an illness. If several Health Monitor signs are outside their range, suggest an easy
          day and, if they feel unwell or it lasts, talking to a doctor.
        - Don't invent data you weren't given. If there's too little data, say so.
        - Answer with JSON only, no other text, in this shape:
          {"headline": "at most 10 words", "summary": "2 or 3 sentences", "wentWell": ["up to 3 short points"],
           "watch": ["up to 3 short points"], "actions": ["exactly 3 concrete things to do in the next 7 days"]}
        """;

    /// <summary>The numbers, as compact text.</summary>
    /// <param name="input">What the coach is shown.</param>
    /// <returns>The user message.</returns>
    public static string Data(AiCoachInput input)
    {
        var inv = CultureInfo.InvariantCulture;
        var today = input.Today;
        var text = new StringBuilder();
        text.AppendLine(inv, $"Day analysed: {today.Date:yyyy-MM-dd} ({today.Date.DayOfWeek}){(today.IsToday ? ", today" : ", a past day: \"today\" below means that day")}.");
        text.AppendLine(inv, $"Person: {(input.Age is { } age ? $"{age} years" : "age unknown")}, {(input.Sex == BiologicalSex.Unspecified ? "sex not given" : input.Sex.ToString().ToLowerInvariant())}.");

        var rec = today.Recovery;
        text.AppendLine(inv, $"Recovery today: {N(rec.Score)} % ({rec.Zone}); HRV {N(rec.Hrv)} ms (normal {rec.HrvBaseline:0}); resting HR {N(rec.RestingHeartRate)} bpm (normal {rec.RestingBaseline:0}); respiratory rate {N(rec.RespiratoryRate, 1)} (normal {rec.RespiratoryBaseline:0.0}).");
        var sleep = today.Sleep;
        if (sleep.HasNight)
        {
            text.AppendLine(inv, $"Last night: asleep {H(sleep.AsleepMinutes)} of {H(sleep.NeedMinutes)} needed ({N(sleep.Performance)} %), in bed {sleep.BedStart:HH:mm}-{sleep.BedEnd:HH:mm}, deep {sleep.DeepMinutes} min, REM {sleep.RemMinutes} min, efficiency {N(sleep.Efficiency)} %, consistency {N(sleep.Consistency)} %.");
        }

        text.AppendLine(inv, $"Tonight: needs {H(sleep.TonightNeedMinutes)}; sleep goal {sleep.GoalPercent} % = {H(sleep.TonightGoalMinutes)}{(sleep.Bedtime is { } bed ? $", bedtime {bed:HH:mm}" : string.Empty)}.");
        text.AppendLine(inv, $"Strain today so far: {N(today.Strain.Score, 1)} (target {today.Strain.TargetLow:0.0}-{today.Strain.TargetHigh:0.0}), zone 2+ {today.Strain.ZoneMinutes.Skip(1).Sum()} min, steps {N(today.Strain.Steps)}.");
        if (today.Stress.Ready)
        {
            var highs = today.Stress.Blocks.Where(static b => b.Level >= 2).Select(static b => b.At.ToString("HH:mm", CultureInfo.InvariantCulture)).ToList();
            text.AppendLine(inv, $"Stress (0-3, heart rate above rest {today.Stress.RestingBpm} bpm while still): average {N(today.Stress.Average, 1)}, low {today.Stress.LowMinutes} min, medium {today.Stress.MediumMinutes} min, high {today.Stress.HighMinutes} min{(highs.Count > 0 ? ", high at " + string.Join(" ", highs.Take(24)) : string.Empty)}; moving {today.Stress.ActiveMinutes} min.");
        }

        if (today.Energy.Ready)
        {
            text.AppendLine(inv, $"Energy bank (0-100): woke with {N(today.Energy.Start)}, now {N(today.Energy.Now)}; spent moving {today.Energy.ActivityDrain}, on stress {today.Energy.StressDrain}, being awake {today.Energy.BaseDrain}.");
        }

        if (today.Strain.Workouts.Any(static w => w.RecoveryOneMinute is not null) || today.Strain.RecoveryAverage is not null)
        {
            var hrr = string.Join("; ", today.Strain.Workouts.Where(static w => w.RecoveryOneMinute is not null).Select(w => $"{w.Kind} -{w.RecoveryOneMinute} bpm after 1 min, -{N(w.RecoveryTwoMinutes)} after 2"));
            text.AppendLine(inv, $"Heart-rate recovery after workouts: {(hrr.Length > 0 ? hrr : "none today")}; 60-day average {N(today.Strain.RecoveryAverage)} bpm after 1 min.");
        }

        if (today.Health.Ready)
        {
            var signs = today.Health.Signs.Select(sign => $"{sign.Name} {N(sign.Value, sign.Decimals)}{sign.Unit} (typical {sign.Low.ToString(sign.Decimals == 0 ? "0" : "0.0", inv)}-{sign.High.ToString(sign.Decimals == 0 ? "0" : "0.0", inv)}, {sign.Status})");
            text.AppendLine(inv, $"Health Monitor: {today.Health.InRange} of {today.Health.Judged} in range. {string.Join("; ", signs)}.");
        }

        var load = today.CardioLoad;
        if (load.Ready)
        {
            text.AppendLine(inv, $"Cardio load: {load.Ratio:0.00} ({load.Status}); productive daily strain {load.StrainLow:0.0}-{load.StrainHigh:0.0}.");
        }
        else
        {
            text.AppendLine("Cardio load: not enough days yet.");
        }

        if (input.Plan is { } plan)
        {
            var goals = string.Join("; ", plan.Goals.Select(goal => string.Create(inv, $"{goal.Name} {goal.Value:0}/{goal.Target:0} {goal.Unit}")));
            text.AppendLine(inv, $"Weekly plan ({plan.DaysLeft} days left): {goals}.");
        }

        if (input.Week is { DaysWithData: > 0 } week)
        {
            text.AppendLine(inv, $"This week's assessment: {(week.Score is { } score ? $"{score}/100 {week.Assessment}" : "not enough days")}. {string.Join(" ", week.Highlights)}");
        }

        if (input.Month is { DaysWithData: > 0 } month)
        {
            text.AppendLine(inv, $"This month's assessment: {(month.Score is { } score ? $"{score}/100 {month.Assessment}" : "not enough days")}. {string.Join(" ", month.Highlights)}");
        }

        if (input.HealthMonth is { } health)
        {
            var signs = string.Join("; ", health.Signs.Select(sign =>
                $"{sign.Name}: average {N(sign.Average, sign.Decimals)}{sign.Unit}, {sign.InRangeDays} nights in range, {sign.OutsideDays} outside"));
            text.AppendLine(inv, $"Health Monitor, last 30 days: {health.DaysFlagged} nights with 2+ signs outside. {signs}.");
        }

        if (input.BiologicalAge is { } bio)
        {
            text.AppendLine(bio.Ready
                ? string.Format(inv, "Biological age: {0:0.0} (calendar {1:0.0}), pace of ageing {2}. Factors (years added, + is older): {3}.",
                    bio.BiologicalAge, bio.ChronologicalAge, bio.Pace is { } pace ? pace.ToString("0.00", inv) + "x" : "-",
                    string.Join("; ", bio.Factors.Select(factor => string.Format(inv, "{0} {1}{2} (typical {3}) {4:+0.0;-0.0;0}", factor.Name, N(factor.Value, 1), factor.Unit, N(factor.Typical, 1), factor.Years))))
                : $"Biological age: not ready ({bio.DaysWithData} of {bio.DaysNeeded} days{(bio.HasBirthDate ? string.Empty : ", no date of birth")}).");
        }

        if (input.Journal is { } journal)
        {
            text.AppendLine(inv, $"Journal: {journal.DaysLogged} days logged; today: {string.Join(", ", journal.Tags.Where(static t => t.On).Select(static t => t.Name).DefaultIfEmpty("nothing"))}.");
            if (journal.Impacts.Count > 0)
            {
                text.AppendLine("Habits and next-morning recovery: " + string.Join("; ", journal.Impacts.Select(impact =>
                    string.Format(inv, "{0}: {1:0} % after vs {2:0} % without ({3:+0;-0} points, {4} days)", impact.Name, impact.RecoveryWith, impact.RecoveryWithout, impact.Difference, impact.DaysWith))) + ".");
            }
        }

        text.AppendLine();
        text.AppendLine($"Last {input.Days.Count} days (date, recovery %, strain, asleep h, sleep %, bedtime, HRV ms, resting HR, steps, zone 2+ min, daylight min, Health Monitor flags):");
        foreach (var day in input.Days.Where(static d => d.Recovery is not null || d.Strain is not null || d.AsleepMinutes is not null))
        {
            text.AppendLine(inv, $"{day.Date:MM-dd},{N(day.Recovery)},{N(day.Strain, 1)},{(day.AsleepMinutes is { } asleep ? (asleep / 60.0).ToString("0.0", inv) : "-")},{N(day.SleepPerformance)},{(day.BedStart is { } start ? start.ToString("HH:mm", inv) : "-")},{N(day.Hrv)},{N(day.RestingHeartRate)},{N(day.Steps)},{day.ActiveMinutes},{N(day.DaylightMinutes)},{(day.HealthFlags.Count > 0 ? string.Join("+", day.HealthFlags) : "-")}");
        }

        return text.ToString();
    }

    /// <summary>Reads the model's answer (JSON, possibly wrapped in a code fence or text) into a report.</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The report (the whole answer as the summary when it isn't JSON).</returns>
    public static AiReportDTO Parse(string answer)
    {
        var text = answer.Trim();
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                using var document = JsonDocument.Parse(text[start..(end + 1)]);
                var root = document.RootElement;
                return new AiReportDTO
                {
                    Headline = Text(root, "headline"),
                    Summary = Text(root, "summary"),
                    WentWell = List(root, "wentWell"),
                    Watch = List(root, "watch"),
                    Actions = List(root, "actions"),
                };
            }
            catch (JsonException)
            {
                // Not JSON after all: show it as it is.
            }
        }

        return new AiReportDTO { Headline = "Your coach's report", Summary = text };
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : string.Empty;

    private static List<string> List(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.String).Select(static item => item.GetString()!.Trim()).Where(static item => item.Length > 0).Take(5).ToList()
            : [];

    private static string N(double? value, int decimals = 0) => value is { } number ? number.ToString(decimals == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) : "-";

    private static string N(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private static string H(int minutes) => $"{minutes / 60}h{minutes % 60:00}";
}
