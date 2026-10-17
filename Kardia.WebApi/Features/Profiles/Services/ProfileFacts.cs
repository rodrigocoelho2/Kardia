using Kardia.Library.Enums;
using Kardia.WebApi.Features.Profiles.Models.Entities;

namespace Kardia.WebApi.Features.Profiles.Services;

/// <summary>
/// The values the scores read from a profile, with the fallbacks filled in. Pure, unit-tested.
/// </summary>
/// <param name="Age">Age in years (fractional), or null without a date of birth.</param>
/// <param name="Sex">The sex for the reference values.</param>
/// <param name="MaxHeartRate">The maximum heart rate used.</param>
/// <param name="SleepNeedMinutes">The base sleep need used.</param>
/// <param name="HeightCm">Height, when known.</param>
/// <param name="WeightKg">Weight, when known.</param>
internal sealed record ProfileFacts(double? Age, BiologicalSex Sex, int MaxHeartRate, int SleepNeedMinutes, double? HeightCm, double? WeightKg)
{
    /// <summary>The base sleep need when none is given (7 h 30).</summary>
    public const int DefaultSleepNeedMinutes = 450;

    /// <summary>The maximum heart rate when neither it nor the age is known.</summary>
    public const int DefaultMaxHeartRate = 190;

    /// <summary>
    /// Reads a profile.
    /// </summary>
    /// <param name="profile">The profile, or null.</param>
    /// <param name="today">Today's date.</param>
    /// <returns>The facts.</returns>
    public static ProfileFacts From(UserProfile? profile, DateOnly today)
    {
        double? age = profile?.BirthDate is { } birth ? AgeOn(DateOnly.FromDateTime(birth), today) : null;
        var max = profile?.MaxHeartRate is { } given and > 0
            ? given
            : age is { } years ? (int)Math.Round(208 - 0.7 * years) : DefaultMaxHeartRate;
        return new ProfileFacts(
            age,
            profile is null ? BiologicalSex.Unspecified : (BiologicalSex)profile.Sex,
            max,
            profile?.SleepNeedMinutes is { } need and > 0 ? need : DefaultSleepNeedMinutes,
            profile?.HeightCm is { } height ? (double)height : null,
            profile?.WeightKg is { } weight ? (double)weight : null);
    }

    /// <summary>
    /// The age in years on a day, with the fraction of the year.
    /// </summary>
    /// <param name="birth">The date of birth.</param>
    /// <param name="on">The day.</param>
    /// <returns>The age.</returns>
    public static double AgeOn(DateOnly birth, DateOnly on)
    {
        var years = on.Year - birth.Year;
        if (birth.AddYears(years) > on)
        {
            years--;
        }

        var last = birth.AddYears(years);
        var next = birth.AddYears(years + 1);
        return years + (on.DayNumber - last.DayNumber) / (double)(next.DayNumber - last.DayNumber);
    }
}
