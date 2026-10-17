namespace Kardia.Library.Enums;

/// <summary>
/// Picks the reference values (VO2 max, resting heart rate, training impulse) used for biological age and strain.
/// </summary>
public enum BiologicalSex
{
    /// <summary>Not given: the average of both is used.</summary>
    Unspecified = 0,

    /// <summary>Male reference values.</summary>
    Male = 1,

    /// <summary>Female reference values.</summary>
    Female = 2,
}
