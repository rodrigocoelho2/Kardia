using Kardia.Library.Enums;

namespace Kardia.WebApi.Features.Days.Services.Scoring;

/// <summary>The acute:chronic heart load of one day.</summary>
/// <param name="Ratio">Mean daily TRIMP of the last 7 days over that of the last 28, or null without enough data.</param>
/// <param name="Status">The band the ratio falls in.</param>
/// <param name="Acute">Mean daily TRIMP, last 7 days.</param>
/// <param name="Chronic">Mean daily TRIMP, last 28 days.</param>
/// <param name="StrainLow">Daily strain that keeps the ratio productive (ratio 1.0).</param>
/// <param name="StrainHigh">Daily strain at the top of productive (ratio 1.3).</param>
internal sealed record CardioLoadResult(double? Ratio, CardioLoadStatus Status, double Acute, double Chronic, double? StrainLow, double? StrainHigh);

/// <summary>
/// Cardio load: how the last week's heart load (Banister TRIMP, the same load strain is made of) compares with what
/// your body is used to over four weeks. Below 0.8 you're detraining, 1.0–1.3 is productive, above 1.5 load jumped
/// too fast. Pure, unit-tested.
/// </summary>
internal static class CardioLoad
{
    /// <summary>Days of the acute window.</summary>
    public const int AcuteDays = 7;

    /// <summary>Days of the chronic window.</summary>
    public const int ChronicDays = 28;

    /// <summary>Days with heart data the chronic window needs.</summary>
    public const int MinimumDays = 21;

    /// <summary>
    /// Works out the cardio load on the last day of a list.
    /// </summary>
    /// <param name="scores">Scored days, oldest first, ending on the day.</param>
    /// <returns>The load.</returns>
    public static CardioLoadResult Of(IReadOnlyList<DayScore> scores)
    {
        var chronicDays = scores.TakeLast(ChronicDays).ToList();
        var withData = chronicDays.Count(static day => day.Load is not null);
        double Trimp(DayScore day) => day.Load is { } load ? (double)load.Trimp : 0;
        var acute = scores.TakeLast(AcuteDays).Sum(Trimp) / AcuteDays;
        var chronic = chronicDays.Sum(Trimp) / ChronicDays;
        if (withData < MinimumDays || chronic <= 0.5)
        {
            return new CardioLoadResult(null, CardioLoadStatus.None, Math.Round(acute, 1), Math.Round(chronic, 1), null, null);
        }

        var ratio = Math.Round(acute / chronic, 2);
        return new CardioLoadResult(ratio, StatusOf(ratio), Math.Round(acute, 1), Math.Round(chronic, 1),
            StrainScore.FromTrimp(chronic * 1.0), StrainScore.FromTrimp(chronic * 1.3));
    }

    /// <summary>
    /// The band of a ratio.
    /// </summary>
    /// <param name="ratio">The acute:chronic ratio.</param>
    /// <returns>The band.</returns>
    public static CardioLoadStatus StatusOf(double ratio) => ratio switch
    {
        < 0.8 => CardioLoadStatus.Detraining,
        < 1.0 => CardioLoadStatus.Maintaining,
        <= 1.3 => CardioLoadStatus.Productive,
        <= 1.5 => CardioLoadStatus.Overreaching,
        _ => CardioLoadStatus.Overtraining,
    };
}
