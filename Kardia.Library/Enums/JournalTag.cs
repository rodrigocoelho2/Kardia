namespace Kardia.Library.Enums;

/// <summary>
/// What you can mark in the journal for a day. Kardia compares the next morning's recovery with and without each one.
/// Stored by name: never rename a member.
/// </summary>
public enum JournalTag
{
    /// <summary>Drank alcohol.</summary>
    Alcohol,

    /// <summary>Caffeine after 2 pm.</summary>
    LateCaffeine,

    /// <summary>Screens in bed.</summary>
    ScreensInBed,

    /// <summary>Ate within two hours of bed.</summary>
    LateMeal,

    /// <summary>Stretched or did mobility work.</summary>
    Stretching,

    /// <summary>Sauna.</summary>
    Sauna,

    /// <summary>Meditated or did breathwork.</summary>
    Meditation,

    /// <summary>Cold shower or plunge.</summary>
    ColdExposure,

    /// <summary>Took magnesium.</summary>
    Magnesium,

    /// <summary>Travelled.</summary>
    Travel,

    /// <summary>A stressful day.</summary>
    Stress,

    /// <summary>Felt sick.</summary>
    Sick,
}
