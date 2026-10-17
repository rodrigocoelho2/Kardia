namespace Kardia.Sdk.Models;

/// <summary>The weekly plan: this week's targets (Monday to Sunday), how far along you are, and last week's recap.</summary>
public sealed class PlanDTO
{
    /// <summary>Gets or sets the Monday of the week.</summary>
    public DateOnly WeekStart { get; set; }

    /// <summary>Gets or sets the Sunday of the week.</summary>
    public DateOnly WeekEnd { get; set; }

    /// <summary>Gets or sets today.</summary>
    public DateOnly Today { get; set; }

    /// <summary>Gets or sets the days left in the week, today included.</summary>
    public int DaysLeft { get; set; }

    /// <summary>Gets or sets the sleep goal: the share of the need aimed for (100, 85 or 70).</summary>
    public int SleepGoalPercent { get; set; }

    /// <summary>Gets or sets your usual bedtime (median of the last 14 nights).</summary>
    public TimeOnly? UsualBedtime { get; set; }

    /// <summary>Gets or sets how many of this week's goals are already met.</summary>
    public int GoalsMet { get; set; }

    /// <summary>Gets or sets this week's goals.</summary>
    public List<PlanGoalDTO> Goals { get; set; } = [];

    /// <summary>Gets or sets last week's goals, against the same targets.</summary>
    public List<PlanGoalDTO> LastWeek { get; set; } = [];
}

/// <summary>One goal of the week.</summary>
public sealed class PlanGoalDTO
{
    /// <summary>Gets or sets the key: sleep, bedtime, active or steps.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the unit of target and value: nights, min or steps.</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Gets or sets the target.</summary>
    public double Target { get; set; }

    /// <summary>Gets or sets where you are (nights done, minutes so far, average steps a day).</summary>
    public double Value { get; set; }

    /// <summary>Gets or sets the progress, 0–100.</summary>
    public int Percent { get; set; }

    /// <summary>Gets or sets a value indicating whether the target is reached.</summary>
    public bool Met { get; set; }

    /// <summary>Gets or sets the target your last four weeks suggest.</summary>
    public double Suggested { get; set; }

    /// <summary>Gets or sets what it takes from here ("2 more nights at 85 % of your need").</summary>
    public string Hint { get; set; } = string.Empty;

    /// <summary>Gets or sets each day of the week.</summary>
    public List<PlanDayDTO> Days { get; set; } = [];
}

/// <summary>One day of a goal.</summary>
public sealed class PlanDayDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the day's value (minutes, steps; 1 or 0 for the nightly goals), or null without data.</summary>
    public double? Value { get; set; }

    /// <summary>Gets or sets whether the day counted towards the goal (nightly goals), or null.</summary>
    public bool? Met { get; set; }

    /// <summary>Gets or sets a value indicating whether the day is still to come.</summary>
    public bool Future { get; set; }
}
