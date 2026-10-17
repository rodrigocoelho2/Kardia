namespace Kardia.Library.Enums;

/// <summary>The last 7 days of heart load against the last 28 (acute:chronic ratio).</summary>
public enum CardioLoadStatus
{
    /// <summary>Not enough weeks of data yet.</summary>
    None,

    /// <summary>Below 0.8: doing less than you're used to; fitness slowly fades.</summary>
    Detraining,

    /// <summary>0.8–1.0: holding your fitness.</summary>
    Maintaining,

    /// <summary>1.0–1.3: building fitness at a sustainable pace.</summary>
    Productive,

    /// <summary>1.3–1.5: more than your body is used to; fine for a short block, then ease off.</summary>
    Overreaching,

    /// <summary>Above 1.5: a sharp jump in load; injury and illness risk go up.</summary>
    Overtraining,
}
