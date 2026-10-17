using Kardia.Library.Enums;

namespace Kardia.Sdk.Models;

/// <summary>Your profile, with the values Kardia actually uses where you left a field empty.</summary>
public sealed class ProfileDTO
{
    /// <summary>Gets or sets the account.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the date of birth.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>Gets or sets the age in whole years (null without a date of birth).</summary>
    public int? Age { get; set; }

    /// <summary>Gets or sets the sex for the reference values.</summary>
    public BiologicalSex Sex { get; set; }

    /// <summary>Gets or sets the height in cm.</summary>
    public decimal? HeightCm { get; set; }

    /// <summary>Gets or sets the weight in kg.</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>Gets or sets the measured maximum heart rate, when given.</summary>
    public int? MaxHeartRate { get; set; }

    /// <summary>Gets or sets the maximum heart rate Kardia uses (given, or 208 − 0.7 × age).</summary>
    public int MaxHeartRateUsed { get; set; }

    /// <summary>Gets or sets the personal base sleep need in minutes, when given.</summary>
    public int? SleepNeedMinutes { get; set; }

    /// <summary>Gets or sets the base sleep need Kardia uses, in minutes.</summary>
    public int SleepNeedMinutesUsed { get; set; }
}

/// <summary>Saves your profile.</summary>
public sealed class ProfileSaveRequest
{
    /// <summary>Gets or sets the account.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the date of birth.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>Gets or sets the sex for the reference values.</summary>
    public BiologicalSex Sex { get; set; }

    /// <summary>Gets or sets the height in cm (100–250).</summary>
    public decimal? HeightCm { get; set; }

    /// <summary>Gets or sets the weight in kg (30–300).</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>Gets or sets a measured maximum heart rate (120–230).</summary>
    public int? MaxHeartRate { get; set; }

    /// <summary>Gets or sets a personal base sleep need in minutes (360–600).</summary>
    public int? SleepNeedMinutes { get; set; }
}
