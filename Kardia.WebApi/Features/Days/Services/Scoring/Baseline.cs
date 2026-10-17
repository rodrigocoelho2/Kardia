namespace Kardia.WebApi.Features.Days.Services.Scoring;

/// <summary>
/// Your normal for one value: the mean and spread of the last 30 days. With fewer than <c>minCount</c> days it falls back
/// to population values, so the first week still scores sensibly. Pure, unit-tested.
/// </summary>
/// <param name="Mean">The mean.</param>
/// <param name="Sd">The standard deviation (never below a floor, so a very steady baseline doesn't make small changes look huge).</param>
/// <param name="Count">How many days it was worked out from (0 = population values).</param>
internal readonly record struct Baseline(double Mean, double Sd, int Count)
{
    /// <summary>The days a baseline looks back.</summary>
    public const int Days = 30;

    /// <summary>
    /// Works out a baseline.
    /// </summary>
    /// <param name="values">The values of the previous days.</param>
    /// <param name="fallbackMean">The population mean.</param>
    /// <param name="fallbackSd">The population spread.</param>
    /// <param name="minCount">Days needed before your own values are used.</param>
    /// <returns>The baseline.</returns>
    public static Baseline Of(IEnumerable<double> values, double fallbackMean, double fallbackSd, int minCount = 4)
    {
        var list = values.Where(double.IsFinite).ToList();
        if (list.Count < minCount)
        {
            return new Baseline(list.Count > 0 ? (list.Average() + fallbackMean) / 2 : fallbackMean, fallbackSd, list.Count);
        }

        var mean = list.Average();
        var sd = Math.Sqrt(list.Sum(value => (value - mean) * (value - mean)) / (list.Count - 1));
        return new Baseline(mean, Math.Max(sd, fallbackSd * 0.4), list.Count);
    }

    /// <summary>
    /// How many spreads a value is from the mean.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The z-score.</returns>
    public double Z(double value) => Sd <= 0 ? 0 : (value - Mean) / Sd;

    /// <summary>
    /// The change from the mean in percent.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The change, e.g. 11.2 for 11 % above.</returns>
    public double PercentChange(double value) => Mean == 0 ? 0 : (value - Mean) / Mean * 100;
}
