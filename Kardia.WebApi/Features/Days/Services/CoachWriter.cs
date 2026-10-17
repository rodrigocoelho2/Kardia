using System.Globalization;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Days.Models.DTOs;
using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Journals.Services;

namespace Kardia.WebApi.Features.Days.Services;

/// <summary>What the coach looks at.</summary>
/// <param name="Day">The day.</param>
/// <param name="Recent">The days before it (oldest first), up to 30.</param>
/// <param name="IsToday">Whether the day is today (tips about "later today" only then).</param>
/// <param name="Now">The local wall clock.</param>
/// <param name="TonightNeed">Tonight's sleep need.</param>
/// <param name="Bedtime">Tonight's bedtime, when known.</param>
/// <param name="Impact">The journal habit with the biggest effect, when there is one.</param>
internal sealed record CoachContext(DayScore Day, IReadOnlyList<DayScore> Recent, bool IsToday, DateTime Now, SleepNeed TonightNeed, DateTime? Bedtime, JournalImpact? Impact);

/// <summary>
/// The coach: a few plain-language tips from the day's numbers — what recovery means for training, sleep debt and
/// tonight's bedtime, how far today's strain is from its target, a resting heart rate that's creeping up, and the
/// journal habit that matters most. Rules, not AI: every sentence comes from a number on the screen. Pure, unit-tested.
/// </summary>
internal static class CoachWriter
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");

    /// <summary>
    /// Writes the tips.
    /// </summary>
    /// <param name="context">What to look at.</param>
    /// <returns>Up to four tips, most useful first.</returns>
    public static List<CoachTipDTO> Write(CoachContext context)
    {
        var tips = new List<CoachTipDTO>();
        var day = context.Day;
        var (low, high) = day.Target;

        // Recovery → training.
        if (day.Recovery.Score is { } recovery)
        {
            var hrv = day.Value(HealthMetric.Hrv);
            var hrvChange = hrv is { } h ? day.HrvBaseline.PercentChange(h) : 0;
            var hrvText = hrv is null ? string.Empty
                : Math.Abs(hrvChange) < 3 ? "Your HRV is right on your normal. "
                : $"Your HRV is {Math.Abs(hrvChange):0}% {(hrvChange > 0 ? "above" : "below")} your 30-day average. ";
            tips.Add(day.Recovery.Zone switch
            {
                RecoveryZone.High => Tip(CoachArea.Recovery, CoachTone.Good, "Ready to push",
                    $"{hrvText}A good day for intervals or a heavy session: aim for strain {F(low)}–{F(high)}."),
                RecoveryZone.Medium => Tip(CoachArea.Recovery, CoachTone.Neutral, "Train, but steady",
                    $"{hrvText}Your body is partly recovered. Keep it moderate today: strain {F(low)}–{F(high)}, mostly zone 2."),
                _ => Tip(CoachArea.Recovery, CoachTone.Caution, "Take it easy",
                    $"{hrvText}Recovery is low ({recovery}%). Walk, stretch, and keep strain under {F(high)}. An early night will do more than a workout."),
            });
        }

        // Strain left today.
        if (context.IsToday && day.Strain is { } strain && context.Now.Hour < 20)
        {
            if (strain < low - 0.3)
            {
                var minutes = (int)Math.Round(Math.Clamp((low - strain) * 12, 10, 60) / 5) * 5;
                tips.Add(Tip(CoachArea.Strain, CoachTone.Neutral, $"{F(low - strain)} to your target",
                    $"About {minutes} minutes in zone 2 (an easy run or ride) would take you from {F(strain)} into your target of {F(low)}–{F(high)}."));
            }
            else if (strain > high + 0.5)
            {
                tips.Add(Tip(CoachArea.Strain, CoachTone.Caution, "Past your target",
                    $"You're at {F(strain)}, above today's {F(low)}–{F(high)}. Hard days are fine; make tonight's sleep count."));
            }
        }

        // Sleep debt and tonight.
        var need = context.TonightNeed;
        var shortfall = context.Recent.TakeLast(2).Sum(static score => score.ShortfallMinutes) + day.ShortfallMinutes;
        if (context.Bedtime is { } bedtime && context.IsToday)
        {
            tips.Add(need.DebtMinutes >= 20
                ? Tip(CoachArea.Sleep, need.DebtMinutes >= 45 ? CoachTone.Caution : CoachTone.Neutral, "Pay back your sleep",
                    $"Your last three nights came up {Duration(shortfall)} short. Tonight you need {Duration(need.TotalMinutes)}, {need.DebtMinutes} min of it to catch up: be in bed by {bedtime:HH:mm}.")
                : Tip(CoachArea.Sleep, CoachTone.Neutral, $"Bed by {bedtime:HH:mm}",
                    $"To wake up fully recovered you'll need {Duration(need.TotalMinutes)} tonight{(need.StrainMinutes > 0 ? $", {need.StrainMinutes} min more than usual after today's strain" : string.Empty)}."));
        }
        else if (day.SleepPerformance is >= 95)
        {
            tips.Add(Tip(CoachArea.Sleep, CoachTone.Good, "Fully rested", "You slept everything you needed that night."));
        }

        // Resting heart rate creeping up (illness, overreaching, alcohol).
        var lastWeek = context.Recent.TakeLast(6).Append(day).Select(static score => score.Value(HealthMetric.RestingHeartRate)).Where(static value => value is not null).ToList();
        if (lastWeek.Count >= 4 && day.RestingBaseline.Count >= 10)
        {
            var rise = lastWeek.Average()!.Value - day.RestingBaseline.Mean;
            if (rise >= 3)
            {
                tips.Add(Tip(CoachArea.Recovery, CoachTone.Caution, "Resting heart rate is up",
                    $"It's averaged {rise:0} bpm above your normal this week. That often comes before a cold, or after too many hard days in a row. Consider a lighter couple of days."));
            }
            else if (rise <= -2)
            {
                tips.Add(Tip(CoachArea.Recovery, CoachTone.Good, "Resting heart rate is down",
                    $"{-rise:0} bpm below your normal this week: your fitness is moving the right way."));
            }
        }

        // The journal habit that matters most.
        if (context.Impact is { } impact && Math.Abs(impact.Difference) >= 4)
        {
            var name = JournalText.Name(impact.Tag);
            tips.Add(impact.Difference < 0
                ? Tip(CoachArea.Journal, CoachTone.Caution, $"{name} costs you", $"After days with {name.ToLowerInvariant()}, your recovery is {-impact.Difference:0} points lower on average ({impact.DaysWith} days logged).")
                : Tip(CoachArea.Journal, CoachTone.Good, $"{name} helps", $"After days with {name.ToLowerInvariant()}, your recovery is {impact.Difference:0} points higher on average ({impact.DaysWith} days logged)."));
        }

        return tips.Take(4).ToList();
    }

    /// <summary>
    /// "7h 40m" or "45 min".
    /// </summary>
    /// <param name="minutes">The minutes.</param>
    /// <returns>The text.</returns>
    public static string Duration(int minutes) => minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}m" : $"{minutes} min";

    private static string F(double value) => value.ToString("0.0", English);

    private static CoachTipDTO Tip(CoachArea area, CoachTone tone, string title, string text) => new() { Area = area, Tone = tone, Title = title, Text = text };
}

/// <summary>
/// The names of the journal habits, shared by the coach and the API.
/// </summary>
internal static class JournalText
{
    /// <summary>
    /// The name shown for a habit.
    /// </summary>
    /// <param name="tag">The habit.</param>
    /// <returns>The name.</returns>
    public static string Name(JournalTag tag) => tag switch
    {
        JournalTag.Alcohol => "Alcohol",
        JournalTag.LateCaffeine => "Caffeine after 2 pm",
        JournalTag.ScreensInBed => "Screens in bed",
        JournalTag.LateMeal => "A late meal",
        JournalTag.Stretching => "Stretching",
        JournalTag.Sauna => "Sauna",
        JournalTag.Meditation => "Meditation",
        JournalTag.ColdExposure => "Cold exposure",
        JournalTag.Magnesium => "Magnesium",
        JournalTag.Travel => "Travel",
        JournalTag.Stress => "Stress",
        JournalTag.Sick => "Feeling sick",
        _ => tag.ToString(),
    };
}
