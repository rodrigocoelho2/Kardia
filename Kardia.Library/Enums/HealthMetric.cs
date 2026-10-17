namespace Kardia.Library.Enums;

/// <summary>
/// The daily values Kardia keeps from Apple Health (one number per day each). The numbers are stored in the
/// database: never renumber them.
/// </summary>
public enum HealthMetric
{
    /// <summary>Heart rate variability, SDNN, in ms (the day's average; Apple measures it mostly at night).</summary>
    Hrv = 1,

    /// <summary>Resting heart rate, in bpm.</summary>
    RestingHeartRate = 2,

    /// <summary>Respiratory rate while asleep, in breaths per minute.</summary>
    RespiratoryRate = 3,

    /// <summary>Blood oxygen, in %.</summary>
    BloodOxygen = 4,

    /// <summary>Sleeping wrist temperature, in °C (absolute; Kardia compares it with your own baseline).</summary>
    WristTemperature = 5,

    /// <summary>Steps.</summary>
    Steps = 6,

    /// <summary>Active energy, in kcal.</summary>
    ActiveEnergy = 7,

    /// <summary>Resting (basal) energy, in kcal.</summary>
    BasalEnergy = 8,

    /// <summary>Cardio fitness (VO2 max), in ml/kg/min.</summary>
    Vo2Max = 9,

    /// <summary>Walking heart rate average, in bpm.</summary>
    WalkingHeartRate = 10,

    /// <summary>Body weight, in kg.</summary>
    BodyMass = 11,

    /// <summary>Apple exercise minutes.</summary>
    ExerciseMinutes = 12,
}
