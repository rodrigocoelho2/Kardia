using System.Globalization;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Days.Services.Scoring;
using Kardia.WebApi.Features.Plans.Models.DTOs;
using Kardia.WebApi.Features.Settings.Models.Entities;

namespace Kardia.WebApi.Features.Plans.Services;

/// <summary>
/// The weekly plan (WHOOP's Weekly Plan): four targets for Monday to Sunday — nights on the sleep goal, nights in bed
/// on time, minutes in heart-rate zone 2 or higher, and steps a day — with what your last four weeks suggest. Pure,
/// unit-tested.
/// </summary>
internal static class WeeklyPlan
{
    /// <summary>Minutes either side of your usual bedtime that still count as on time.</summary>
    public const int OnTimeMinutes = 30;

    /// <summary>Nights your usual bedtime is the median of.</summary>
    public const int BedtimeNights = 14;

    /// <summary>Days of history the plan needs before the week (four weeks plus the bedtime nights).</summary>
    public const int HistoryDays = 28 + BedtimeNights;

    /// <summary>The Monday of a date's week.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The Monday.</returns>
    public static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>
    /// Builds the plan.
    /// </summary>
    /// <param name="scores">Scored days, oldest first, from at least <see cref="HistoryDays"/> before last week's Monday to today.</param>
    /// <param name="setting">The targets.</param>
    /// <param name="today">Today.</param>
    /// <returns>The plan.</returns>
    public static PlanDTO Build(IReadOnlyList<DayScore> scores, UserSetting setting, DateOnly today)
    {
        var monday = Monday(today);
        var byDate = scores.ToDictionary(static score => score.Date);
        var suggestions = Suggest(scores, monday, setting.SleepGoalPercent);
        var goals = Goals(byDate, monday, today, setting, suggestions);
        return new PlanDTO
        {
            WeekStart = monday,
            WeekEnd = monday.AddDays(6),
            Today = today,
            DaysLeft = monday.AddDays(6).DayNumber - today.DayNumber + 1,
            SleepGoalPercent = setting.SleepGoalPercent,
            UsualBedtime = UsualBedtime(byDate, today.AddDays(1)) is { } minutes ? TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Round((minutes + 720) % 1440 / 5) * 5 % 1440)) : null,
            GoalsMet = goals.Count(static goal => goal.Met),
            Goals = goals,
            LastWeek = Goals(byDate, monday.AddDays(-7), monday.AddDays(-1), setting, suggestions),
        };
    }

    /// <summary>Whether a night reached the sleep goal.</summary>
    /// <param name="score">The day the night ended on.</param>
    /// <param name="goalPercent">The goal.</param>
    /// <returns>True, false, or null without a night.</returns>
    public static bool? SleepMet(DayScore score, int goalPercent) =>
        score.AsleepMinutes is { } asleep ? asleep >= SleepScore.Goal(score.Need.TotalMinutes, goalPercent) : null;

    /// <summary>Whether a night started within <see cref="OnTimeMinutes"/> of your usual bedtime.</summary>
    /// <param name="byDate">Scored days by date.</param>
    /// <param name="date">The day the night ended on.</param>
    /// <returns>True, false, or null without the night or enough nights before it.</returns>
    public static bool? OnTime(IReadOnlyDictionary<DateOnly, DayScore> byDate, DateOnly date)
    {
        if (!byDate.TryGetValue(date, out var day) || day.Night is null || UsualBedtime(byDate, date) is not { } usual)
        {
            return null;
        }

        var difference = Math.Abs(NoonMinutes(day.Night.BedStart) - usual);
        return difference <= OnTimeMinutes;
    }

    /// <summary>The median bedtime of the nights before a date, in minutes after noon (so 23:00 and 01:00 sit together).</summary>
    /// <param name="byDate">Scored days by date.</param>
    /// <param name="date">The first night not counted.</param>
    /// <returns>The minutes after noon, or null with fewer than 5 nights.</returns>
    public static double? UsualBedtime(IReadOnlyDictionary<DateOnly, DayScore> byDate, DateOnly date)
    {
        var nights = Enumerable.Range(1, BedtimeNights)
            .Select(back => byDate.TryGetValue(date.AddDays(-back), out var day) ? day.Night : null)
            .Where(static night => night is not null)
            .Select(static night => NoonMinutes(night!.BedStart))
            .Order()
            .ToList();
        return nights.Count < 5 ? null : nights[nights.Count / 2];
    }

    private static double NoonMinutes(DateTime time) => (time.TimeOfDay.TotalMinutes + 720) % 1440;

    private static (double Sleep, double Bedtime, double Active, double Steps) Suggest(IReadOnlyList<DayScore> scores, DateOnly monday, int goalPercent)
    {
        var byDate = scores.ToDictionary(static score => score.Date);
        var weeks = Enumerable.Range(1, 4).Select(back => monday.AddDays(-7 * back)).ToList();
        var sleepNights = new List<int>();
        var onTimeNights = new List<int>();
        var active = new List<int>();
        var steps = new List<double>();
        foreach (var start in weeks)
        {
            var days = Enumerable.Range(0, 7).Select(offset => byDate.TryGetValue(start.AddDays(offset), out var day) ? day : null).Where(static day => day is not null).Select(static day => day!).ToList();
            if (days.Count(static day => day.HasData) < 4)
            {
                continue;
            }

            sleepNights.Add(days.Count(day => SleepMet(day, goalPercent) == true));
            onTimeNights.Add(days.Count(day => OnTime(byDate, day.Date) == true));
            active.Add(days.Sum(DayService.ActiveMinutes));
            steps.AddRange(days.Select(static day => day.Value(HealthMetric.Steps)).Where(static value => value is not null).Select(static value => value!.Value));
        }

        static double Nights(List<int> values) => values.Count == 0 ? 5 : Math.Clamp(Math.Round(values.Average()) + 1, 3, 7);
        return (
            Nights(sleepNights),
            Nights(onTimeNights),
            active.Count == 0 ? 150 : Math.Clamp(Math.Round(active.Average() * 1.1 / 10) * 10, 60, 600),
            steps.Count == 0 ? 8000 : Math.Clamp(Math.Round(steps.Average() * 1.05 / 500) * 500, 4000, 20000));
    }

    private static List<PlanGoalDTO> Goals(IReadOnlyDictionary<DateOnly, DayScore> byDate, DateOnly monday, DateOnly last, UserSetting setting,
        (double Sleep, double Bedtime, double Active, double Steps) suggested)
    {
        var dates = Enumerable.Range(0, 7).Select(offset => monday.AddDays(offset)).ToList();
        var past = dates.Where(date => date <= last).ToList();
        var left = dates.Count - past.Count;
        DayScore? Day(DateOnly date) => byDate.TryGetValue(date, out var day) ? day : null;

        // Sleep: nights that reached the goal.
        var sleepDays = dates.Select(date => date > last ? Future(date) : Night(date, Day(date) is { } day ? SleepMet(day, setting.SleepGoalPercent) : null)).ToList();
        var sleepDone = sleepDays.Count(static day => day.Met == true);
        var sleep = Count("sleep", $"Sleep at {setting.SleepGoalPercent}% of your need", setting.SleepNightsWeek, sleepDone, suggested.Sleep, sleepDays, left,
            $"at {setting.SleepGoalPercent}% of your need");

        // Bedtime: nights in bed within 30 minutes of your usual time.
        var bedDays = dates.Select(date => date > last ? Future(date) : Night(date, OnTime(byDate, date))).ToList();
        var bed = Count("bedtime", "In bed on time", setting.BedtimeNightsWeek, bedDays.Count(static day => day.Met == true), suggested.Bedtime, bedDays, left,
            "within 30 min of your usual bedtime");

        // Zone 2+: minutes.
        var activeDays = dates.Select(date => new PlanDayDTO { Date = date, Future = date > last, Value = date > last ? null : Day(date) is { HasData: true } day ? DayService.ActiveMinutes(day) : null }).ToList();
        var activeDone = activeDays.Sum(static day => day.Value ?? 0);
        var activeTarget = Math.Max(0, (double)setting.ActiveMinutesWeek);
        var activeToGo = Math.Max(0, activeTarget - activeDone);
        var active = new PlanGoalDTO
        {
            Key = "active",
            Name = "Zone 2+ minutes",
            Unit = "min",
            Target = activeTarget,
            Value = activeDone,
            Percent = Percent(activeDone, activeTarget),
            Met = activeDone >= activeTarget,
            Suggested = suggested.Active,
            Hint = activeDone >= activeTarget ? "Done for the week. Extra easy minutes still count."
                : left == 0 ? $"Finished {activeToGo:0} min short."
                : $"{activeToGo:0} min to go: about {Math.Ceiling(activeToGo / left):0} a day.",
            Days = activeDays,
        };

        // Steps: the average of the days so far.
        var stepDays = dates.Select(date => new PlanDayDTO { Date = date, Future = date > last, Value = date > last ? null : Day(date)?.Value(HealthMetric.Steps) is { } value ? Math.Round(value) : null }).ToList();
        var measured = stepDays.Where(static day => day.Value is not null).Select(static day => day.Value!.Value).ToList();
        var average = measured.Count == 0 ? 0 : Math.Round(measured.Average());
        var stepsTarget = (double)setting.StepsDay;
        var needed = left == 0 || measured.Count == 0 ? 0 : (stepsTarget * (measured.Count + left) - measured.Sum()) / left;
        var steps = new PlanGoalDTO
        {
            Key = "steps",
            Name = "Steps a day",
            Unit = "steps",
            Target = stepsTarget,
            Value = average,
            Percent = Percent(average, stepsTarget),
            Met = measured.Count > 0 && average >= stepsTarget,
            Suggested = suggested.Steps,
            Hint = measured.Count == 0 ? "No steps yet this week."
                : average >= stepsTarget ? $"Averaging {N(average)} a day."
                : left == 0 ? $"Averaged {N(average)} a day."
                : $"Averaging {N(average)}: {N(Math.Ceiling(needed / 100) * 100)} a day from here gets you there.",
            Days = stepDays,
        };

        return [sleep, bed, active, steps];
    }

    private static PlanGoalDTO Count(string key, string name, int target, int done, double suggested, List<PlanDayDTO> days, int left, string what)
    {
        var toGo = Math.Max(0, target - done);
        return new PlanGoalDTO
        {
            Key = key,
            Name = name,
            Unit = "nights",
            Target = target,
            Value = done,
            Percent = Percent(done, target),
            Met = done >= target,
            Suggested = suggested,
            Hint = done >= target ? $"{done} of {target} nights: done."
                : toGo > left ? $"Out of reach this week ({left} {(left == 1 ? "night" : "nights")} left), but every night {what} still helps."
                : $"{toGo} more {(toGo == 1 ? "night" : "nights")} {what}.",
            Days = days,
        };
    }

    private static PlanDayDTO Night(DateOnly date, bool? met) => new() { Date = date, Met = met, Value = met is { } on ? (on ? 1 : 0) : null };

    private static PlanDayDTO Future(DateOnly date) => new() { Date = date, Future = true };

    private static int Percent(double value, double target) => target <= 0 ? 100 : (int)Math.Clamp(Math.Round(value * 100 / target), 0, 100);

    private static string N(double value) => value.ToString("#,0", CultureInfo.InvariantCulture);
}
