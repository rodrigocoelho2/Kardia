namespace Kardia.Library.Enums;

/// <summary>What the AI coach is asked to look at: everything, or the subject of one screen.</summary>
public enum AiCoachTopic
{
    /// <summary>Everything (Today and the AI coach screen).</summary>
    Overview,

    /// <summary>Recovery: what moved it and how hard to go.</summary>
    Recovery,

    /// <summary>Sleep: last night, the pattern, tonight.</summary>
    Sleep,

    /// <summary>Strain: today's load, workouts, cardio load.</summary>
    Strain,

    /// <summary>Biological age: the factors to work on.</summary>
    Age,

    /// <summary>Health Monitor: the overnight signs.</summary>
    Health,

    /// <summary>The weekly plan.</summary>
    Plan,

    /// <summary>Trends over 90 days.</summary>
    Trends,

    /// <summary>The weekly and monthly assessments.</summary>
    Report,

    /// <summary>The journal: which habits help or hurt.</summary>
    Journal,

    /// <summary>The stress monitor and energy bank.</summary>
    Stress,
}
