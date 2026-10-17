namespace Kardia.Sdk.Models;

/// <summary>Your biological age, what moves it, and how it changed.</summary>
public sealed class AgeDTO
{
    /// <summary>Gets or sets a value indicating whether a date of birth is set (without it there's no age to compare with).</summary>
    public bool HasBirthDate { get; set; }

    /// <summary>Gets or sets a value indicating whether there are enough days of data for an age.</summary>
    public bool Ready { get; set; }

    /// <summary>Gets or sets the days with data in the last 30.</summary>
    public int DaysWithData { get; set; }

    /// <summary>Gets or sets the days with data needed before an age is given.</summary>
    public int DaysNeeded { get; set; }

    /// <summary>Gets or sets the biological age.</summary>
    public double? BiologicalAge { get; set; }

    /// <summary>Gets or sets the age by the calendar.</summary>
    public double? ChronologicalAge { get; set; }

    /// <summary>Gets or sets the difference (negative = younger than the calendar).</summary>
    public double? Difference { get; set; }

    /// <summary>Gets or sets the pace of ageing (1.0 = one year per year), when a month of history allows it.</summary>
    public double? Pace { get; set; }

    /// <summary>Gets or sets what moves the age, biggest effect first.</summary>
    public List<AgeFactorDTO> Factors { get; set; } = [];

    /// <summary>Gets or sets the age worked out at the end of each of the last six months (oldest first).</summary>
    public List<AgePointDTO> History { get; set; } = [];
}

/// <summary>One marker of biological age.</summary>
public sealed class AgeFactorDTO
{
    /// <summary>Gets or sets a stable key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets your value (30-day average).</summary>
    public double? Value { get; set; }

    /// <summary>Gets or sets the unit.</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Gets or sets the typical value for your age and sex.</summary>
    public double? Typical { get; set; }

    /// <summary>Gets or sets the years it adds (positive) or takes off (negative).</summary>
    public double Years { get; set; }
}

/// <summary>Biological age on one day.</summary>
public sealed class AgePointDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the biological age.</summary>
    public double BiologicalAge { get; set; }

    /// <summary>Gets or sets the age by the calendar.</summary>
    public double ChronologicalAge { get; set; }
}
