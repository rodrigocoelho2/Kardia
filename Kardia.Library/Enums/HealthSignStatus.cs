namespace Kardia.Library.Enums;

/// <summary>Where one Health Monitor sign sits against your own typical range.</summary>
public enum HealthSignStatus
{
    /// <summary>No value for the day, or not enough days yet to know your range.</summary>
    None,

    /// <summary>Inside your typical range.</summary>
    InRange,

    /// <summary>Just outside it: worth watching for a few days.</summary>
    Outside,

    /// <summary>Far outside it.</summary>
    FarOutside,
}
