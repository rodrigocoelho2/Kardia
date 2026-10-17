namespace Kardia.Library.Enums;

/// <summary>
/// The band a recovery score falls in.
/// </summary>
public enum RecoveryZone
{
    /// <summary>No score yet (no night or no heart data).</summary>
    None,

    /// <summary>0–33: a day to go easy.</summary>
    Low,

    /// <summary>34–66: train, but not at full gas.</summary>
    Medium,

    /// <summary>67–100: ready to push.</summary>
    High,
}
