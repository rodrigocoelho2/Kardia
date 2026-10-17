using Kardia.WebApi.Features;
using Microsoft.Extensions.Options;

namespace Kardia.WebApi.Extensions.Time;

/// <summary>
/// The wall clock in the app's time zone (<see cref="KardiaOptions.TimeZone"/>). Everything Kardia keeps is a local
/// time in this zone, so a day always runs from midnight to midnight on your clock.
/// </summary>
internal interface IAppClock
{
    /// <summary>Gets the app's time zone.</summary>
    TimeZoneInfo Zone { get; }

    /// <summary>Gets the local wall-clock time now.</summary>
    DateTime Now { get; }

    /// <summary>Gets today's date.</summary>
    DateOnly Today { get; }

    /// <summary>
    /// Converts an instant to the app's wall clock.
    /// </summary>
    /// <param name="instant">The instant.</param>
    /// <returns>The local time (Kind Unspecified).</returns>
    DateTime ToLocal(DateTimeOffset instant);
}

/// <summary>
/// <see cref="IAppClock"/> over a <see cref="TimeProvider"/>.
/// </summary>
/// <param name="timeProvider">The clock.</param>
/// <param name="options">The module options (time zone).</param>
internal sealed class AppClock(TimeProvider timeProvider, IOptions<KardiaOptions> options) : IAppClock
{
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc />
    public TimeZoneInfo Zone { get; } = FindZone(options.Value.TimeZone);

    /// <inheritdoc />
    public DateTime Now => ToLocal(_timeProvider.GetUtcNow());

    /// <inheritdoc />
    public DateOnly Today => DateOnly.FromDateTime(Now);

    /// <inheritdoc />
    public DateTime ToLocal(DateTimeOffset instant) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(instant, Zone).DateTime, DateTimeKind.Unspecified);

    /// <summary>
    /// Finds a time zone by IANA (or Windows) name; unknown names fall back to UTC.
    /// </summary>
    /// <param name="name">The zone name.</param>
    /// <returns>The zone.</returns>
    public static TimeZoneInfo FindZone(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name) && TimeZoneInfo.TryFindSystemTimeZoneById(name.Trim(), out var zone))
        {
            return zone;
        }

        return TimeZoneInfo.Utc;
    }
}
