using Kardia.Library.Enums;
using Kardia.WebApi.Features.Days.Services.Scoring;

namespace Kardia.WebApi.Features.Journals.Services;

/// <summary>What one journal habit does to the next morning's recovery.</summary>
/// <param name="Tag">The habit.</param>
/// <param name="DaysWith">Days marked with it (that have a recovery the morning after).</param>
/// <param name="DaysWithout">Days without it.</param>
/// <param name="RecoveryWith">Average recovery the morning after a day with it.</param>
/// <param name="RecoveryWithout">Average recovery the morning after a day without it.</param>
internal sealed record JournalImpact(JournalTag Tag, int DaysWith, int DaysWithout, double RecoveryWith, double RecoveryWithout)
{
    /// <summary>Gets the difference in recovery points (positive = it helps).</summary>
    public double Difference => Math.Round(RecoveryWith - RecoveryWithout, 1);
}

/// <summary>
/// Compares the morning after each journal habit with the mornings after days without it. A habit needs at least
/// <see cref="MinimumDays"/> days each way before Kardia says anything about it. Pure, unit-tested.
/// </summary>
internal static class JournalImpacts
{
    /// <summary>The fewest days with and without a habit before it is compared.</summary>
    public const int MinimumDays = 3;

    /// <summary>
    /// Works out the effect of every habit.
    /// </summary>
    /// <param name="entries">The journal (day and tag name).</param>
    /// <param name="scores">Day scores covering the journal days and the mornings after.</param>
    /// <returns>One impact per habit with enough days, the biggest effect first.</returns>
    public static List<JournalImpact> Calculate(IEnumerable<(DateOnly Day, string Tag)> entries, IReadOnlyList<DayScore> scores)
    {
        var recovery = scores.Where(static score => score.Recovery.Score is not null).ToDictionary(static score => score.Date, static score => (double)score.Recovery.Score!.Value);
        var marked = entries
            .Where(static entry => Enum.TryParse<JournalTag>(entry.Tag, out _))
            .GroupBy(static entry => Enum.Parse<JournalTag>(entry.Tag))
            .ToDictionary(static group => group.Key, static group => group.Select(static entry => entry.Day).ToHashSet());

        // Only days the journal was used at all count as "without": an empty journal says nothing.
        var journalDays = marked.Values.SelectMany(static days => days).ToHashSet();
        var impacts = new List<JournalImpact>();
        foreach (var (tag, days) in marked)
        {
            var with = days.Where(day => recovery.ContainsKey(day.AddDays(1))).Select(day => recovery[day.AddDays(1)]).ToList();
            var without = journalDays.Where(day => !days.Contains(day) && recovery.ContainsKey(day.AddDays(1))).Select(day => recovery[day.AddDays(1)]).ToList();
            if (with.Count >= MinimumDays && without.Count >= MinimumDays)
            {
                impacts.Add(new JournalImpact(tag, with.Count, without.Count, Math.Round(with.Average(), 1), Math.Round(without.Average(), 1)));
            }
        }

        return [.. impacts.OrderByDescending(static impact => Math.Abs(impact.Difference))];
    }
}
