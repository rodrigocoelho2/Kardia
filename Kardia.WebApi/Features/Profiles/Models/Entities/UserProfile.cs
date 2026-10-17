namespace Kardia.WebApi.Features.Profiles.Models.Entities;

/// <summary>
/// What the scores need to know about you (dbo.UserProfile). Every value is optional: Kardia falls back to
/// estimates (maximum heart rate from age, sleep need of 7 h 30).
/// </summary>
public sealed class UserProfile
{
    /// <summary>Gets or sets the date of birth (biological age and maximum heart rate need it).</summary>
    public DateTime? BirthDate { get; set; }

    /// <summary>Gets or sets the sex for the reference values (<see cref="Library.Enums.BiologicalSex"/>).</summary>
    public byte Sex { get; set; }

    /// <summary>Gets or sets the height in cm.</summary>
    public decimal? HeightCm { get; set; }

    /// <summary>Gets or sets the weight in kg (a newer Apple Health weight wins).</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>Gets or sets a measured maximum heart rate, when known.</summary>
    public short? MaxHeartRate { get; set; }

    /// <summary>Gets or sets a personal base sleep need in minutes, when known.</summary>
    public short? SleepNeedMinutes { get; set; }
}
