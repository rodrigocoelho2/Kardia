namespace Kardia.Library.Enums;

/// <summary>
/// The sleep stages Apple Watch records.
/// </summary>
public enum SleepStage
{
    /// <summary>Awake in bed.</summary>
    Awake = 0,

    /// <summary>REM (dreaming) sleep.</summary>
    Rem = 1,

    /// <summary>Core sleep (shown as "Light").</summary>
    Core = 2,

    /// <summary>Deep (slow-wave) sleep.</summary>
    Deep = 3,
}
