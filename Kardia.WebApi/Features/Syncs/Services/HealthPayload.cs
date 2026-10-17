using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kardia.Library.Enums;
using Kardia.WebApi.Features.Syncs.Models.Entities;

namespace Kardia.WebApi.Features.Syncs.Services;

/// <summary>What an export brought, read and converted to Kardia's rows (local times, metric units).</summary>
/// <param name="Metrics">One value per day and metric.</param>
/// <param name="HeartRates">Heart rate per minute.</param>
/// <param name="Sleeps">Nights.</param>
/// <param name="Workouts">Workouts.</param>
/// <param name="Error">What was wrong, or null.</param>
internal sealed record HealthPayloadData(
    IReadOnlyList<DailyMetricRow> Metrics,
    IReadOnlyList<HeartRateRow> HeartRates,
    IReadOnlyList<SleepNightRow> Sleeps,
    IReadOnlyList<WorkoutRow> Workouts,
    string? Error)
{
    /// <summary>
    /// Gets the timed readings of the night-measured values (<see cref="HealthPayload.SampledMetrics"/>). Their day values
    /// are not in <see cref="Metrics"/>: they are worked out from the readings taken during the night.
    /// </summary>
    public IReadOnlyList<MetricSampleRow> Samples { get; init; } = [];

    /// <summary>Gets a value indicating whether nothing can be imported.</summary>
    public bool IsEmpty => Metrics.Count == 0 && HeartRates.Count == 0 && Sleeps.Count == 0 && Workouts.Count == 0 && Samples.Count == 0;

    /// <summary>Gets every day the export touched.</summary>
    public IEnumerable<DateOnly> Days =>
        Metrics.Select(static row => DateOnly.FromDateTime(row.DayDate))
            .Concat(HeartRates.Select(static row => DateOnly.FromDateTime(row.MinuteAt)))
            .Concat(Sleeps.Select(static row => DateOnly.FromDateTime(row.NightDate)))
            .Concat(Workouts.Select(static row => DateOnly.FromDateTime(row.StartedAt)))
            .Concat(Samples.Select(static row => DateOnly.FromDateTime(row.SampleAt)))
            .Distinct()
            .Order();
}

/// <summary>
/// Reads what Apple Health data arrives as. Two shapes:
/// <list type="bullet">
/// <item><b>Health Auto Export</b> (REST API automation, JSON, export version 2):
/// <c>{ "data": { "metrics": [ { "name": "heart_rate_variability", "units": "ms", "data": [ { "date": "2026-10-16 07:00:00 +0100", "qty": 68 } ] } ], "workouts": [ … ] } }</c>.
/// Heart rate comes as <c>{ date, Min, Avg, Max }</c>; sleep either per stage (<c>{ startDate, endDate, value: "Deep" }</c>)
/// or as nightly totals (<c>{ sleepStart, sleepEnd, core, deep, rem, awake }</c> in hours).</item>
/// <item><b>The Kardia Shortcut</b>: a flat dictionary (<c>hrv</c>, <c>restingHeartRate</c>, <c>steps</c>…, <c>sleep</c>,
/// <c>heartRates</c>, <c>workouts</c>). Shortcuts is loose with types, so numbers may carry units ("68 ms") and decimal commas.</item>
/// </list>
/// Values are converted to Kardia's units and checked against sane ranges; anything out of range is dropped.
/// Pure, unit-tested.
/// </summary>
internal static partial class HealthPayload
{
    /// <summary>
    /// The values the Watch measures mostly while you sleep. With a time on each reading, a day's value is the average of
    /// that night's readings (like WHOOP), so readings later in the day can't change a morning's recovery.
    /// </summary>
    public static readonly HashSet<HealthMetric> SampledMetrics = [HealthMetric.Hrv, HealthMetric.RespiratoryRate, HealthMetric.BloodOxygen, HealthMetric.WristTemperature];

    /// <summary>
    /// Totals of the day (steps, energy, exercise, daylight). Their timed readings are kept too, so the day's total is the
    /// sum of every reading ever received (a "since last sync" export carries only the new ones), and the stress monitor
    /// knows when you were moving.
    /// </summary>
    public static readonly HashSet<HealthMetric> SummedMetrics = [HealthMetric.Steps, HealthMetric.ActiveEnergy, HealthMetric.BasalEnergy, HealthMetric.ExerciseMinutes, HealthMetric.Daylight];

    /// <summary>Gap between sleep segments that starts a new night.</summary>
    public static readonly TimeSpan NightGap = TimeSpan.FromHours(2);

    /// <summary>Sessions shorter than this that end in the afternoon are naps, and are not nights.</summary>
    public static readonly TimeSpan ShortestNight = TimeSpan.FromHours(3);

    private static readonly CultureInfo[] Cultures =
        [CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("en-GB"), CultureInfo.GetCultureInfo("pt-PT")];

    /// <summary>How each metric name maps to a <see cref="HealthMetric"/>, and whether a day's values add up (true) or average.</summary>
    private static readonly Dictionary<string, (HealthMetric Metric, bool Sum)> MetricNames = new(StringComparer.Ordinal)
    {
        ["heartratevariability"] = (HealthMetric.Hrv, false),
        ["hrv"] = (HealthMetric.Hrv, false),
        ["heartratevariabilitysdnn"] = (HealthMetric.Hrv, false),
        ["restingheartrate"] = (HealthMetric.RestingHeartRate, false),
        ["rhr"] = (HealthMetric.RestingHeartRate, false),
        ["respiratoryrate"] = (HealthMetric.RespiratoryRate, false),
        ["bloodoxygensaturation"] = (HealthMetric.BloodOxygen, false),
        ["oxygensaturation"] = (HealthMetric.BloodOxygen, false),
        ["bloodoxygen"] = (HealthMetric.BloodOxygen, false),
        ["spo"] = (HealthMetric.BloodOxygen, false),
        ["applesleepingwristtemperature"] = (HealthMetric.WristTemperature, false),
        ["sleepingwristtemperature"] = (HealthMetric.WristTemperature, false),
        ["wristtemperature"] = (HealthMetric.WristTemperature, false),
        ["stepcount"] = (HealthMetric.Steps, true),
        ["steps"] = (HealthMetric.Steps, true),
        ["activeenergy"] = (HealthMetric.ActiveEnergy, true),
        ["activeenergyburned"] = (HealthMetric.ActiveEnergy, true),
        ["basalenergyburned"] = (HealthMetric.BasalEnergy, true),
        ["basalenergy"] = (HealthMetric.BasalEnergy, true),
        ["restingenergy"] = (HealthMetric.BasalEnergy, true),
        ["vomax"] = (HealthMetric.Vo2Max, false),
        ["cardiofitness"] = (HealthMetric.Vo2Max, false),
        ["walkingheartrateaverage"] = (HealthMetric.WalkingHeartRate, false),
        ["walkingheartrate"] = (HealthMetric.WalkingHeartRate, false),
        ["weightbodymass"] = (HealthMetric.BodyMass, false),
        ["bodymass"] = (HealthMetric.BodyMass, false),
        ["weight"] = (HealthMetric.BodyMass, false),
        ["appleexercisetime"] = (HealthMetric.ExerciseMinutes, true),
        ["exercisetime"] = (HealthMetric.ExerciseMinutes, true),
        ["exerciseminutes"] = (HealthMetric.ExerciseMinutes, true),
        ["timeindaylight"] = (HealthMetric.Daylight, true),
        ["daylight"] = (HealthMetric.Daylight, true),
        ["daylightminutes"] = (HealthMetric.Daylight, true),
    };

    /// <summary>The range each metric must fall in (in Kardia's units) to be kept.</summary>
    private static readonly Dictionary<HealthMetric, (double Min, double Max)> Ranges = new()
    {
        [HealthMetric.Hrv] = (3, 300),
        [HealthMetric.RestingHeartRate] = (25, 150),
        [HealthMetric.RespiratoryRate] = (4, 45),
        [HealthMetric.BloodOxygen] = (70, 100),
        [HealthMetric.WristTemperature] = (30, 42),
        [HealthMetric.Steps] = (0, 200_000),
        [HealthMetric.ActiveEnergy] = (0, 20_000),
        [HealthMetric.BasalEnergy] = (0, 10_000),
        [HealthMetric.Vo2Max] = (10, 95),
        [HealthMetric.WalkingHeartRate] = (40, 200),
        [HealthMetric.BodyMass] = (25, 400),
        [HealthMetric.ExerciseMinutes] = (0, 1_440),
        [HealthMetric.Daylight] = (0, 1_440),
    };

    /// <summary>
    /// Reads an export.
    /// </summary>
    /// <param name="root">The JSON body.</param>
    /// <param name="zone">The app's time zone (times are converted to it; times without an offset are taken as local).</param>
    /// <param name="now">The wall clock in that zone (flat values without a date belong to today).</param>
    /// <returns>The rows, or an error.</returns>
    public static HealthPayloadData Parse(JsonElement root, TimeZoneInfo zone, DateTime now)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Empty("Send a JSON dictionary.");
        }

        var collector = new Collector(zone, now);
        var fields = Fields(root);
        var data = Child(fields, "data") ?? fields;

        if (data.TryGetValue("metrics", out var metrics) && metrics.ValueKind == JsonValueKind.Array)
        {
            foreach (var metric in metrics.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.Object))
            {
                collector.Metric(Fields(metric));
            }
        }

        if (data.TryGetValue("workouts", out var workouts) && workouts.ValueKind == JsonValueKind.Array)
        {
            foreach (var workout in workouts.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.Object).Take(500))
            {
                collector.Workout(Fields(workout));
            }
        }

        // The Shortcut's flat dictionary.
        collector.Flat(fields);
        return collector.Build();
    }

    private static HealthPayloadData Empty(string error) => new([], [], [], [], error);

    /// <summary>
    /// Joins a night already saved with the same night from a new export. "Since last sync" exports often carry only part
    /// of a night (its last hour, or the iPhone's segments), so a night is never replaced by a shorter one: the stages of
    /// both are laid on one timeline (each minute once) and the time in bed covers both.
    /// </summary>
    /// <param name="saved">The night already saved.</param>
    /// <param name="incoming">The same night from the new export.</param>
    /// <returns>The joined night.</returns>
    public static SleepNightRow Merge(SleepNightRow saved, SleepNightRow incoming)
    {
        static int Asleep(SleepNightRow night) => night.CoreMinutes + night.DeepMinutes + night.RemMinutes;

        // Totals only (no stages) on either side: keep the fuller night.
        if (saved.Stages.Length == 0 || incoming.Stages.Length == 0)
        {
            var savedAsleep = Asleep(saved);
            var incomingAsleep = Asleep(incoming);
            return incomingAsleep > savedAsleep || (incomingAsleep == savedAsleep && incoming.BedEnd - incoming.BedStart > saved.BedEnd - saved.BedStart) ? incoming : saved;
        }

        // The new export's stages win where both have a minute (they're the Watch's latest word on it).
        var session = new List<(SleepStage? Stage, DateTime Start, DateTime End)>
        {
            (null, saved.BedStart, saved.BedEnd),
            (null, incoming.BedStart, incoming.BedEnd),
        };
        session.AddRange(Days.Services.Scoring.SleepScore.Segments(saved).Select(static segment => ((SleepStage?)segment.Stage, segment.Start, segment.End)));
        session.AddRange(Days.Services.Scoring.SleepScore.Segments(incoming).Select(static segment => ((SleepStage?)segment.Stage, segment.Start, segment.End)));
        var merged = Collector.Night(session);
        return merged is null || Asleep(merged) < Math.Max(Asleep(saved), Asleep(incoming)) - 1 ? (Asleep(incoming) > Asleep(saved) ? incoming : saved) : merged;
    }

    /// <summary>
    /// The keys of a JSON object, lower case with only letters (so "heart_rate_variability", "heartRateVariability"
    /// and "Heart Rate Variability" are the same field).
    /// </summary>
    private static Dictionary<string, JsonElement> Fields(JsonElement element)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            fields.TryAdd(Key(property.Name), property.Value);
        }

        return fields;
    }

    // Exports repeat the same few field names hundreds of thousands of times ("date", "qty", "source"): normalise each once.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Keys = new(StringComparer.Ordinal);

    private static string Key(string name) =>
        name.Length > 64 ? Normalise(name) : Keys.Count < 4096 ? Keys.GetOrAdd(name, Normalise) : Keys.TryGetValue(name, out var known) ? known : Normalise(name);

    private static string Normalise(string name) => new([.. name.ToLowerInvariant().Where(char.IsAsciiLetterLower)]);

    private static Dictionary<string, JsonElement>? Child(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Object ? Fields(value) : null;

    private static JsonElement? First(Dictionary<string, JsonElement> fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>A number, from a JSON number, a string with units ("68 ms", "82,4 kg") or a { qty } object.</summary>
    internal static double? Number(JsonElement? element)
    {
        if (element is not { } value)
        {
            return null;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                return value.GetDouble();
            case JsonValueKind.String:
                var match = NumberPattern().Match(value.GetString() ?? string.Empty);
                if (!match.Success)
                {
                    return null;
                }

                var text = match.Value.Replace(" ", string.Empty, StringComparison.Ordinal);
                // "1.234,5" / "1,234.5" / "82,4": the last separator is the decimal one.
                var lastComma = text.LastIndexOf(',');
                var lastDot = text.LastIndexOf('.');
                text = lastComma > lastDot
                    ? text.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.')
                    : text.Replace(",", string.Empty, StringComparison.Ordinal);
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
            case JsonValueKind.Object:
                return Number(First(Fields(value), "qty", "value", "avg", "quantity"));
            default:
                return null;
        }
    }

    /// <summary>A unit string from an element or a { units } field.</summary>
    private static string Units(JsonElement? element) => element is { ValueKind: JsonValueKind.String } value ? (value.GetString() ?? string.Empty).Trim().ToLowerInvariant() : string.Empty;

    /// <summary>
    /// A local time in the app's zone from "2026-10-16 07:00:00 +0100" (Health Auto Export), ISO 8601, or the phone's
    /// own format ("16 Oct 2026 at 07:00").
    /// </summary>
    internal static DateTime? Time(JsonElement? element, TimeZoneInfo zone)
    {
        if (element is not { ValueKind: JsonValueKind.String } value || string.IsNullOrWhiteSpace(value.GetString()))
        {
            return null;
        }

        // Fast path for Health Auto Export's own format, "2026-10-16 07:00:00 +0100" (almost every sample).
        if (FastHae(value.GetString()!) is { } fast)
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(fast, zone).DateTime, DateTimeKind.Unspecified);
        }

        var text = value.GetString()!.Trim().Replace(" at ", " ", StringComparison.OrdinalIgnoreCase).Replace(" às ", " ", StringComparison.OrdinalIgnoreCase);
        var hae = HaeTimePattern().Match(text);
        if (hae.Success)
        {
            text = $"{hae.Groups["d"].Value}T{hae.Groups["t"].Value}{hae.Groups["h"].Value}:{hae.Groups["m"].Value}";
        }

        if (HasOffsetPattern().IsMatch(text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset))
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(offset, zone).DateTime, DateTimeKind.Unspecified);
        }

        foreach (var culture in Cultures)
        {
            if (DateTime.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out var local))
            {
                return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            }
        }

        return null;
    }

    /// <summary>Reads "yyyy-MM-dd HH:mm:ss ±hhmm" without regular expressions or culture lookups.</summary>
    private static DateTimeOffset? FastHae(string text)
    {
        var t = text.AsSpan();
        if (t.Length != 25 || t[4] != '-' || t[7] != '-' || t[10] != ' ' || t[13] != ':' || t[16] != ':' || t[19] != ' ' || (t[20] != '+' && t[20] != '-'))
        {
            return null;
        }

        static bool Digits(ReadOnlySpan<char> part, out int value) => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        if (!Digits(t[..4], out var year) || !Digits(t[5..7], out var month) || !Digits(t[8..10], out var day)
            || !Digits(t[11..13], out var hour) || !Digits(t[14..16], out var minute) || !Digits(t[17..19], out var second)
            || !Digits(t[21..23], out var offsetHours) || !Digits(t[23..25], out var offsetMinutes))
        {
            return null;
        }

        try
        {
            var offset = new TimeSpan(offsetHours, offsetMinutes, 0);
            return new DateTimeOffset(year, month, day, hour, minute, second, t[20] == '-' ? -offset : offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static double? InRange(HealthMetric metric, double? value) =>
        value is { } number && double.IsFinite(number) && Ranges.TryGetValue(metric, out var range)
            && ((number >= range.Min && number <= range.Max) || (metric == HealthMetric.WristTemperature && Math.Abs(number) <= 5))
            ? number : null;

    /// <summary>Converts a value to Kardia's unit for the metric.</summary>
    private static double? Convert(HealthMetric metric, double? value, string units)
    {
        if (value is not { } number)
        {
            return null;
        }

        return metric switch
        {
            HealthMetric.ActiveEnergy or HealthMetric.BasalEnergy when units is "kj" => number / 4.184,
            HealthMetric.BodyMass when units is "lb" or "lbs" => number * 0.45359237,
            // Absolute (°F) or, as some exports send it, the change from the Watch's own baseline (°F).
            HealthMetric.WristTemperature when units.Contains('f', StringComparison.Ordinal) => Math.Abs(number) < 15 ? number * 5 / 9 : (number - 32) * 5 / 9,
            HealthMetric.BloodOxygen when number <= 1 => number * 100,
            HealthMetric.ExerciseMinutes when units is "s" or "sec" => number / 60,
            HealthMetric.ExerciseMinutes when units is "hr" or "h" => number * 60,
            _ => number,
        };
    }

    private static SleepStage? Stage(string? text)
    {
        var key = Key(text ?? string.Empty);
        return key switch
        {
            "awake" or "awakeinbed" => SleepStage.Awake,
            "rem" or "asleeprem" => SleepStage.Rem,
            "deep" or "asleepdeep" => SleepStage.Deep,
            "core" or "light" or "asleepcore" or "asleep" or "asleepunspecified" or "unspecified" => SleepStage.Core,
            _ => null,
        };
    }

    [GeneratedRegex(@"[-+]?\d[\d\s.,]*")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^(?<d>\d{4}-\d{2}-\d{2})[ T](?<t>\d{2}:\d{2}(:\d{2})?(\.\d+)?)\s*(?<h>[+-]\d{2}):?(?<m>\d{2})$")]
    private static partial Regex HaeTimePattern();

    [GeneratedRegex(@"(Z|[+-]\d{2}:?\d{2})$")]
    private static partial Regex HasOffsetPattern();

    /// <summary>Gathers everything an export holds, then builds the rows.</summary>
    /// <param name="zone">The app's time zone.</param>
    /// <param name="now">The local wall clock.</param>
    private sealed class Collector(TimeZoneInfo zone, DateTime now)
    {
        private readonly Dictionary<(DateTime Day, HealthMetric Metric), List<double>> _metrics = [];
        private readonly HashSet<HealthMetric> _sums = [];
        private readonly Dictionary<DateTime, List<double>> _heartRates = [];
        private readonly List<(SleepStage? Stage, DateTime Start, DateTime End)> _segments = [];
        private readonly Dictionary<(HealthMetric Metric, DateTime At), decimal> _samples = [];
        private readonly List<SleepNightRow> _nights = [];
        private readonly Dictionary<DateTime, WorkoutRow> _workouts = [];

        /// <summary>One Health Auto Export metric with its samples.</summary>
        public void Metric(Dictionary<string, JsonElement> metric)
        {
            var name = First(metric, "name") is { ValueKind: JsonValueKind.String } n ? Key(n.GetString()!) : string.Empty;
            var units = Units(First(metric, "units", "unit"));
            if (First(metric, "data") is not { ValueKind: JsonValueKind.Array } samples)
            {
                return;
            }

            if (name == "heartrate")
            {
                foreach (var sample in samples.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.Object))
                {
                    var f = Fields(sample);
                    HeartRate(Time(First(f, "date", "start", "startdate"), zone), Number(First(f, "avg", "qty", "value", "max")));
                }

                return;
            }

            if (name == "sleepanalysis")
            {
                foreach (var sample in samples.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.Object))
                {
                    Sleep(Fields(sample));
                }

                return;
            }

            if (!MetricNames.TryGetValue(name, out var target))
            {
                return;
            }

            foreach (var sample in samples.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.Object))
            {
                var f = Fields(sample);
                var at = Time(First(f, "date", "start", "startdate"), zone);
                var value = Convert(target.Metric, Number(First(f, "qty", "value", "avg")), units);
                if (at is { } time && SampledMetrics.Contains(target.Metric))
                {
                    Sample(target.Metric, time, value);
                    continue;
                }

                if (at is { } when && target.Sum)
                {
                    SumSample(target.Metric, when, value);
                }

                Add(target.Metric, target.Sum, at?.Date ?? now.Date, value);
            }
        }

        /// <summary>One Health Auto Export workout.</summary>
        public void Workout(Dictionary<string, JsonElement> w)
        {
            var start = Time(First(w, "start", "startdate", "startedat"), zone);
            var end = Time(First(w, "end", "enddate", "endedat"), zone);
            if (start is null)
            {
                return;
            }

            if (end is null && Number(First(w, "duration", "durationseconds")) is { } seconds and > 0)
            {
                end = start.Value.AddSeconds(seconds);
            }

            if (end is null || end <= start || end - start > TimeSpan.FromHours(12))
            {
                return;
            }

            var name = First(w, "name", "type", "workouttype", "activity") is { ValueKind: JsonValueKind.String } n ? n.GetString()!.Trim() : "Workout";
            if (First(w, "isindoor") is { ValueKind: JsonValueKind.True } && !name.Contains("indoor", StringComparison.OrdinalIgnoreCase))
            {
                name = "Indoor " + name;
            }

            var energy = First(w, "activeenergyburned", "activeenergy", "activecalories", "kcal");
            var kcal = Number(energy);
            if (kcal is { } k && energy is { ValueKind: JsonValueKind.Object } energyObject && Units(First(Fields(energyObject), "units")) == "kj")
            {
                kcal = k / 4.184;
            }

            var distanceElement = First(w, "distance", "distancekm");
            var distance = Number(distanceElement);
            var distanceUnits = distanceElement is { ValueKind: JsonValueKind.Object } distanceObject ? Units(First(Fields(distanceObject), "units")) : string.Empty;
            if (distance is { } d)
            {
                distance = distanceUnits switch { "mi" => d * 1.609344, "m" => d / 1000, _ => d };
            }

            var heart = Child(w, "heartrate");
            var avg = Number(heart is not null ? First(heart, "avg") : First(w, "avgheartrate", "averageheartrate", "avgbpm"));
            var max = Number(heart is not null ? First(heart, "max") : First(w, "maxheartrate", "maxbpm"));

            _workouts[start.Value] = new WorkoutRow
            {
                StartedAt = start.Value,
                EndedAt = end.Value,
                Kind = name.Length > 60 ? name[..60] : name,
                ActiveKcal = kcal is { } kc and >= 0 and < 10_000 ? Math.Round((decimal)kc, 1) : null,
                DistanceKm = distance is { } km and > 0 and < 1_000 ? Math.Round((decimal)km, 2) : null,
                AvgBpm = avg is { } a and >= 30 and <= 250 ? (short)Math.Round(a) : null,
                MaxBpm = max is { } m and >= 30 and <= 250 ? (short)Math.Round(m) : null,
            };

            // The workout's own heart-rate curve, and the minutes after it (heart-rate recovery), also feed the day's minutes.
            foreach (var key in new[] { "heartratedata", "heartraterecovery" })
            {
                if (First(w, key) is not { ValueKind: JsonValueKind.Array } curve)
                {
                    continue;
                }

                foreach (var sample in curve.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.Object))
                {
                    var f = Fields(sample);
                    HeartRate(Time(First(f, "date"), zone), Number(First(f, "avg", "qty", "max")));
                }
            }
        }

        /// <summary>The Shortcut's flat dictionary (or extra fields next to "data").</summary>
        public void Flat(Dictionary<string, JsonElement> fields)
        {
            var day = Time(First(fields, "date", "day"), zone)?.Date ?? now.Date;
            foreach (var (name, value) in fields)
            {
                if (MetricNames.TryGetValue(name, out var target) && value.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                {
                    var units = value.ValueKind == JsonValueKind.String ? UnitOf(value.GetString()!) : string.Empty;
                    Add(target.Metric, target.Sum, day, Convert(target.Metric, Number(value), units));
                }
            }

            if (First(fields, "heartrates", "heartrate") is { ValueKind: JsonValueKind.Array } rates)
            {
                foreach (var sample in rates.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.Object))
                {
                    var f = Fields(sample);
                    HeartRate(Time(First(f, "date", "t", "time", "start"), zone), Number(First(f, "bpm", "avg", "value", "qty", "b")));
                }
            }

            switch (First(fields, "sleep"))
            {
                case { ValueKind: JsonValueKind.Array } segments:
                    foreach (var segment in segments.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.Object))
                    {
                        Sleep(Fields(segment));
                    }

                    break;
                case { ValueKind: JsonValueKind.Object } night:
                    var nightFields = Fields(night);
                    if (First(nightFields, "stages") is { ValueKind: JsonValueKind.Array } stages)
                    {
                        foreach (var stage in stages.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.Object))
                        {
                            Sleep(Fields(stage));
                        }

                        // The night's own start and end bound it (time in bed before falling asleep).
                        if (Time(First(nightFields, "start", "inbedstart", "sleepstart"), zone) is { } bedStart
                            && Time(First(nightFields, "end", "inbedend", "sleepend"), zone) is { } bedEnd && bedEnd > bedStart)
                        {
                            _segments.Add((null, bedStart, bedEnd));
                        }
                    }
                    else
                    {
                        Sleep(nightFields);
                    }

                    break;
            }

            // A single workout (a list under "workouts" is read by Parse).
            if (Child(fields, "workout") is { } workout)
            {
                Workout(workout);
            }
        }

        /// <summary>A sleep sample: one stage segment, or one night of totals.</summary>
        private void Sleep(Dictionary<string, JsonElement> f)
        {
            // Totals for a night (Health Auto Export "aggregate sleep"): hours per stage.
            if (First(f, "totalsleep", "asleep", "core", "deep", "rem") is not null && First(f, "sleepstart", "inbedstart", "sleepend", "inbedend") is not null)
            {
                var start = Time(First(f, "inbedstart", "sleepstart"), zone);
                var end = Time(First(f, "inbedend", "sleepend"), zone);
                if (start is null || end is null || end <= start || end - start > TimeSpan.FromHours(18))
                {
                    return;
                }

                int Minutes(params string[] names) => (int)Math.Round((Number(First(f, names)) ?? 0) * 60);
                var core = Minutes("core");
                var deep = Minutes("deep");
                var rem = Minutes("rem");
                var awake = Minutes("awake");
                if (core + deep + rem == 0)
                {
                    core = Minutes("asleep", "totalsleep");
                }

                if (core + deep + rem == 0)
                {
                    return;
                }

                _nights.Add(new SleepNightRow
                {
                    NightDate = end.Value.Date,
                    BedStart = start.Value,
                    BedEnd = end.Value,
                    AwakeMinutes = (short)Math.Clamp(awake, 0, 1_000),
                    CoreMinutes = (short)Math.Clamp(core, 0, 1_000),
                    DeepMinutes = (short)Math.Clamp(deep, 0, 1_000),
                    RemMinutes = (short)Math.Clamp(rem, 0, 1_000),
                });
                return;
            }

            // One stage segment.
            var from = Time(First(f, "startdate", "start", "from"), zone);
            var to = Time(First(f, "enddate", "end", "to"), zone);
            var value = First(f, "value", "stage", "type") is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;
            if (from is null || to is null || to <= from || to - from > TimeSpan.FromHours(16))
            {
                return;
            }

            var isInBed = Key(value ?? string.Empty) == "inbed";
            var stage = Stage(value);
            if (stage is null && !isInBed)
            {
                return;
            }

            _segments.Add((stage, from.Value, to.Value));
        }

        private void HeartRate(DateTime? at, double? bpm)
        {
            if (at is not { } time || bpm is not { } value || value < 25 || value > 250)
            {
                return;
            }

            var minute = new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, DateTimeKind.Unspecified);
            if (!_heartRates.TryGetValue(minute, out var list))
            {
                list = [];
                _heartRates[minute] = list;
            }

            list.Add(value);
        }

        private void Sample(HealthMetric metric, DateTime at, double? value)
        {
            if (value is { } number && double.IsFinite(number) && InRange(metric, number) is { } kept)
            {
                _samples[(metric, new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, at.Second))] = Math.Round((decimal)kept, 3);
            }
        }

        private void SumSample(HealthMetric metric, DateTime at, double? value)
        {
            if (value is not { } number || !double.IsFinite(number) || number < 0 || number > 100_000)
            {
                return;
            }

            var key = (metric, new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, at.Second));
            _samples[key] = Math.Round((_samples.TryGetValue(key, out var old) ? old : 0) + (decimal)number, 3);
        }

        private void Add(HealthMetric metric, bool sum, DateTime day, double? value)
        {
            // Averaged values must each be sane; summed ones (steps, energy) only non-negative, the day's total is checked in Build.
            if (value is not { } number || !double.IsFinite(number) || (number < 0 && metric != HealthMetric.WristTemperature) || (!sum && InRange(metric, number) is null))
            {
                return;
            }

            if (sum)
            {
                _sums.Add(metric);
            }

            var key = (day.Date, metric);
            if (!_metrics.TryGetValue(key, out var list))
            {
                list = [];
                _metrics[key] = list;
            }

            list.Add(number);
        }

        public HealthPayloadData Build()
        {
            var metrics = new List<DailyMetricRow>();
            foreach (var ((day, metric), values) in _metrics.OrderBy(static pair => pair.Key.Day).ThenBy(static pair => pair.Key.Metric))
            {
                var value = _sums.Contains(metric) ? values.Sum() : values.Average();
                if (InRange(metric, value) is { } kept)
                {
                    metrics.Add(new DailyMetricRow { DayDate = day, Metric = (int)metric, Value = Math.Round((decimal)kept, 3) });
                }
            }

            var heartRates = _heartRates
                .OrderBy(static pair => pair.Key)
                .Select(static pair => new HeartRateRow { MinuteAt = pair.Key, Bpm = (short)Math.Round(pair.Value.Average()) })
                .ToList();

            var nights = _nights.Concat(Nights()).GroupBy(static night => night.NightDate)
                .Select(static group => group.OrderByDescending(static night => night.Stages.Length).ThenByDescending(static night => night.BedEnd - night.BedStart).First())
                .OrderBy(static night => night.NightDate)
                .ToList();

            var samples = _samples.OrderBy(static pair => pair.Key.At)
                .Select(static pair => new MetricSampleRow { Metric = (int)pair.Key.Metric, SampleAt = pair.Key.At, Value = pair.Value })
                .ToList();
            var result = new HealthPayloadData(metrics, heartRates, nights, _workouts.Values.OrderBy(static workout => workout.StartedAt).ToList(), null) { Samples = samples };
            return result.IsEmpty ? result with { Error = "Nothing Kardia keeps was in this export." } : result;
        }

        /// <summary>Stage segments grouped into nights: a gap of more than <see cref="NightGap"/> starts a new one.</summary>
        private IEnumerable<SleepNightRow> Nights()
        {
            var ordered = _segments.OrderBy(static segment => segment.Start).ToList();
            var session = new List<(SleepStage? Stage, DateTime Start, DateTime End)>();
            foreach (var segment in ordered)
            {
                if (session.Count > 0 && segment.Start - session.Max(static item => item.End) > NightGap)
                {
                    if (Night(session) is { } night)
                    {
                        yield return night;
                    }

                    session = [];
                }

                session.Add(segment);
            }

            if (Night(session) is { } last)
            {
                yield return last;
            }
        }

        internal static SleepNightRow? Night(List<(SleepStage? Stage, DateTime Start, DateTime End)> session)
        {
            var staged = session.Where(static segment => segment.Stage is not null).OrderBy(static segment => segment.Start).ToList();
            if (staged.Count == 0)
            {
                return null;
            }

            var start = session.Min(static segment => segment.Start);
            var end = session.Max(static segment => segment.End);
            var asleepEnd = staged.Max(static segment => segment.End);
            if (end - start < ShortestNight && asleepEnd.Hour is >= 10 and < 20)
            {
                return null;   // a nap
            }

            // Overlapping sources (iPhone and Watch) are flattened: each minute counts once, the later segment wins.
            var minutes = (int)Math.Ceiling((end - start).TotalMinutes);
            var timeline = new SleepStage?[minutes];
            foreach (var (stage, from, to) in staged)
            {
                var a = Math.Clamp((int)Math.Floor((from - start).TotalMinutes), 0, minutes);
                var b = Math.Clamp((int)Math.Ceiling((to - start).TotalMinutes), 0, minutes);
                for (var minute = a; minute < b; minute++)
                {
                    timeline[minute] = stage;
                }
            }

            var stages = new StringBuilder();
            var counts = new int[4];
            var runStart = 0;
            for (var minute = 1; minute <= minutes; minute++)
            {
                if (minute < minutes && timeline[minute] == timeline[runStart])
                {
                    continue;
                }

                if (timeline[runStart] is { } runStage)
                {
                    counts[(int)runStage] += minute - runStart;
                    stages.Append(stages.Length > 0 ? ";" : string.Empty)
                        .Append(CultureInfo.InvariantCulture, $"{(int)runStage},{runStart},{minute}");
                }

                runStart = minute;
            }

            return new SleepNightRow
            {
                NightDate = asleepEnd.Date,
                BedStart = start,
                BedEnd = end,
                AwakeMinutes = (short)counts[(int)SleepStage.Awake],
                RemMinutes = (short)counts[(int)SleepStage.Rem],
                CoreMinutes = (short)counts[(int)SleepStage.Core],
                DeepMinutes = (short)counts[(int)SleepStage.Deep],
                Stages = stages.ToString(),
            };
        }

        private static string UnitOf(string text)
        {
            var match = Regex.Match(text, @"[A-Za-z°]+\s*$");
            return match.Success ? match.Value.Trim().ToLowerInvariant() : string.Empty;
        }
    }
}
