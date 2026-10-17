namespace Kardia.WebApi.Features.Settings.Models.DTOs;

/// <summary>An account's goals and notification choices.</summary>
public sealed class SettingDTO
{
    /// <summary>Gets or sets the account.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the share of the sleep need to aim for each night: 100, 85 or 70.</summary>
    public int SleepGoalPercent { get; set; } = 100;

    /// <summary>Gets or sets the minutes in zone 2 or higher to reach each week.</summary>
    public int ActiveMinutesWeek { get; set; } = 150;

    /// <summary>Gets or sets the daily steps to average over the week.</summary>
    public int StepsDay { get; set; } = 8000;

    /// <summary>Gets or sets how many nights a week should reach the sleep goal.</summary>
    public int SleepNightsWeek { get; set; } = 5;

    /// <summary>Gets or sets how many nights a week should start within 30 minutes of your usual bedtime.</summary>
    public int BedtimeNightsWeek { get; set; } = 5;

    /// <summary>Gets or sets a value indicating whether to send the morning recovery.</summary>
    public bool NotifyMorning { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to send the bedtime reminder.</summary>
    public bool NotifyBedtime { get; set; } = true;

    /// <summary>Gets or sets how many minutes before bedtime the reminder goes out.</summary>
    public int BedtimeLeadMinutes { get; set; } = 30;

    /// <summary>Gets or sets a value indicating whether to warn when Health Monitor signs leave their range.</summary>
    public bool NotifyHealth { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to say when the strain target is reached.</summary>
    public bool NotifyStrain { get; set; }

    /// <summary>Gets or sets a value indicating whether to send the weekly and monthly assessments.</summary>
    public bool NotifyReports { get; set; } = true;
}
