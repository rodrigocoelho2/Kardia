using System.Globalization;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Days.Models.DTOs;
using Kardia.WebApi.Features.Reports.Models.DTOs;
using Kardia.WebApi.Features.Settings.Models.Entities;

namespace Kardia.WebApi.Features.Push.Services;

/// <summary>A notification due now, with the key that makes it go out once.</summary>
/// <param name="Key">The claim key ("morning:2026-10-16").</param>
/// <param name="Message">The notification.</param>
internal sealed record Notification(string Key, PushMessage Message);

/// <summary>
/// Which notifications are due (WHOOP's set): the morning recovery once last night is in, a Health Monitor alert when
/// two or more signs leave their range, the bedtime reminder before tonight's bedtime, optionally the strain target,
/// and the weekly (Sunday evening) and monthly (the 1st) assessments. Pure, unit-tested.
/// </summary>
internal static class NotificationPlanner
{
    /// <summary>The first hour the morning recovery and health alert go out.</summary>
    public const int MorningFrom = 5;

    /// <summary>The hour after which a missed morning recovery is no longer sent.</summary>
    public const int MorningUntil = 14;

    /// <summary>Minutes after bedtime a missed reminder is still sent.</summary>
    public const int BedtimeGrace = 45;

    /// <summary>The hour the weekly assessment goes out on Sundays.</summary>
    public const int WeeklyHour = 19;

    /// <summary>The hour the monthly assessment goes out on the 1st.</summary>
    public const int MonthlyHour = 10;

    /// <summary>
    /// The daily notifications due now.
    /// </summary>
    /// <param name="now">Local time now.</param>
    /// <param name="day">Today.</param>
    /// <param name="setting">What the account wants.</param>
    /// <returns>The notifications, in the order to send.</returns>
    public static List<Notification> Daily(DateTime now, DayDTO day, UserSetting setting)
    {
        var list = new List<Notification>();
        var date = Iso(day.Date);
        var recovery = day.Recovery;

        if (setting.NotifyMorning && recovery.Score is { } score && day.Sleep.HasNight && now.Hour >= MorningFrom && now.Hour < MorningUntil)
        {
            var parts = new List<string>();
            if (recovery.Hrv is { } hrv)
            {
                parts.Add($"HRV {hrv:0} ms");
            }

            if (recovery.RestingHeartRate is { } rhr)
            {
                parts.Add($"RHR {rhr:0} bpm");
            }

            parts.Add($"slept {Duration(day.Sleep.AsleepMinutes)}{(day.Sleep.Performance is { } p ? $" ({p}%)" : string.Empty)}");
            var body = string.Join(" · ", parts) + $". Strain target {F(recovery.TargetLow)}–{F(recovery.TargetHigh)}.";
            list.Add(new Notification($"morning:{date}", new PushMessage($"Recovery {score}% · {Zone(recovery.Zone)}", body, "/", "morning")));
        }

        var flagged = day.Health.Signs.Where(static sign => sign.Status is HealthSignStatus.Outside or HealthSignStatus.FarOutside).ToList();
        if (setting.NotifyHealth && flagged.Count >= 2 && now.Hour >= MorningFrom && now.Hour < 22)
        {
            var body = string.Join("; ", flagged.Select(static sign => $"{sign.Name} {Value(sign)} (usually {Number(sign.Low, sign.Decimals)}–{Number(sign.High, sign.Decimals)})"))
                + ". Worth taking it easy and seeing how you feel.";
            list.Add(new Notification($"health:{date}", new PushMessage($"Health Monitor: {flagged.Count} signs outside your range", body, "/health", "health")));
        }

        if (setting.NotifyStrain && day.IsToday && day.Strain.Score is { } strain && recovery.Score is { } todayScore && now.Hour >= 7 && now.Hour < 23)
        {
            if (recovery.Zone == RecoveryZone.Low && strain > recovery.TargetHigh)
            {
                list.Add(new Notification($"strain:{date}", new PushMessage("Past today's strain target",
                    $"Strain {F(strain)}, above {F(recovery.TargetLow)}–{F(recovery.TargetHigh)} for a {todayScore}% recovery. Easy from here helps tomorrow.", "/strain", "strain")));
            }
            else if (strain >= recovery.TargetLow)
            {
                list.Add(new Notification($"strain:{date}", new PushMessage("Strain target reached",
                    $"Strain {F(strain)}: today's target was {F(recovery.TargetLow)}–{F(recovery.TargetHigh)} for a {todayScore}% recovery.", "/strain", "strain")));
            }
        }

        if (setting.NotifyBedtime && day.IsToday && day.Sleep.Bedtime is { } bedtime
            && now >= bedtime.AddMinutes(-setting.BedtimeLeadMinutes) && now < bedtime.AddMinutes(BedtimeGrace))
        {
            var minutesLeft = (int)Math.Round((bedtime - now).TotalMinutes);
            var title = minutesLeft >= 5 ? $"Bedtime in {minutesLeft} min" : "Time for bed";
            var body = $"In bed by {bedtime:HH:mm} to sleep {Duration(day.Sleep.TonightGoalMinutes)} ({GoalName(day.Sleep.GoalPercent)}, {day.Sleep.GoalPercent}% of your need)"
                + (day.Sleep.WakeAt is { } wake ? $" and wake at {wake:HH:mm}." : ".");
            list.Add(new Notification($"bedtime:{date}", new PushMessage(title, body, "/sleep", "bedtime")));
        }

        return list;
    }

    /// <summary>
    /// The assessments due now.
    /// </summary>
    /// <param name="now">Local time now.</param>
    /// <param name="setting">What the account wants.</param>
    /// <returns>The period, a day inside it and the claim key of each.</returns>
    public static List<(ReportPeriod Period, DateOnly Date, string Key)> ReportsDue(DateTime now, UserSetting setting)
    {
        var due = new List<(ReportPeriod, DateOnly, string)>();
        if (!setting.NotifyReports)
        {
            return due;
        }

        var today = DateOnly.FromDateTime(now);
        if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour >= WeeklyHour)
        {
            due.Add((ReportPeriod.Week, today, $"week:{Iso(today.AddDays(-6))}"));
        }

        if (now.Day == 1 && now.Hour >= MonthlyHour)
        {
            var lastMonth = today.AddDays(-1);
            due.Add((ReportPeriod.Month, lastMonth, $"month:{lastMonth:yyyy-MM}"));
        }

        return due;
    }

    /// <summary>
    /// The notification of an assessment.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <returns>The notification, or null when the period had no data.</returns>
    public static PushMessage? Report(ReportDTO report)
    {
        if (report.DaysWithData == 0)
        {
            return null;
        }

        var noun = report.Period == ReportPeriod.Month ? "month" : "week";
        var title = report.Assessment.Length > 0 ? $"Your {noun}: {report.Assessment} ({report.Score})" : $"Your {noun} in Kardia";
        var body = string.Join(" ", report.Highlights.Take(2));
        return new PushMessage(title, body.Length > 0 ? body : $"Your {noun}ly assessment is ready.", $"/report?period={noun}&date={Iso(report.From)}", noun);
    }

    /// <summary>The name WHOOP gives a sleep goal.</summary>
    /// <param name="percent">100, 85 or 70.</param>
    /// <returns>Peak, Perform or Get by.</returns>
    public static string GoalName(int percent) => percent >= 100 ? "Peak" : percent >= 85 ? "Perform" : "Get by";

    private static string Zone(RecoveryZone zone) => zone switch
    {
        RecoveryZone.High => "Ready to push",
        RecoveryZone.Medium => "Steady day",
        _ => "Take it easy",
    };

    private static string Value(HealthSignDTO sign) => sign.Value is not { } value ? "–"
        : sign.Key == "temperature" ? $"{(value >= 0 ? "+" : "−")}{Number(Math.Abs(value), sign.Decimals)} °C"
        : $"{Number(value, sign.Decimals)} {sign.Unit}";

    private static string Number(double value, int decimals) => value.ToString(decimals == 0 ? "0" : "0.0", CultureInfo.InvariantCulture);

    private static string F(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    private static string Duration(int minutes) => $"{minutes / 60}h {minutes % 60:00}m";

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
