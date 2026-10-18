using System.Globalization;
using System.Text;
using Kardia.Library.Enums;
using Kardia.Sdk.Models;
using Microsoft.AspNetCore.Html;

namespace Kardia.Website.Helpers;

/// <summary>
/// The charts, drawn on the server as inline SVG (no chart library, nothing to load): the score rings, the heart rate
/// through the day, the hypnogram, trend lines and sparklines. Every SVG scales with its box; colours come from the
/// palettes in app.css.
/// </summary>
public static class Charts
{
    /// <summary>The circumference of the ring (r = 90).</summary>
    public const double RingLength = 565.49;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static int _ids;

    /// <summary>
    /// A score ring: a flat track and the value in the pillar's colour with round caps, plus (for strain) the target
    /// arc just outside it.
    /// </summary>
    /// <param name="fraction">How full, 0–1.</param>
    /// <param name="palette">rec, mid, low, slp, str or age.</param>
    /// <param name="target">An arc drawn outside the ring (e.g. the strain target), as fractions.</param>
    /// <param name="thin">A thinner ring for small sizes.</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent Ring(double fraction, string palette, (double From, double To)? target = null, bool thin = false)
    {
        var colour = palette switch
        {
            "mid" => "var(--mid)",
            "low" => "var(--low)",
            "slp" => "var(--slp)",
            "str" => "var(--str)",
            "age" => "var(--sky)",
            "nrg" => "var(--nrg)",
            _ => "var(--rec)",
        };
        var value = Math.Clamp(fraction, 0, 1);
        var length = value * RingLength;
        var width = thin ? 18 : 16;

        var svg = new StringBuilder();
        svg.Append(Invariant, $"<svg class=\"ring p-{palette}\" viewBox=\"0 0 220 220\" aria-hidden=\"true\" style=\"--ring:{colour}\">");
        svg.Append(Invariant, $"<circle cx=\"110\" cy=\"110\" r=\"90\" fill=\"none\" class=\"ring-track\" stroke-width=\"{width}\"/>");
        svg.Append("<g transform=\"rotate(-90 110 110)\">");
        if (target is { } arc && !thin)
        {
            const double outer = 2 * Math.PI * 106;
            var from = Math.Clamp(arc.From, 0, 1) * outer;
            var span = Math.Max(2, (Math.Clamp(arc.To, 0, 1) - Math.Clamp(arc.From, 0, 1)) * outer);
            svg.Append(Invariant, $"<circle cx=\"110\" cy=\"110\" r=\"106\" fill=\"none\" class=\"ring-target\" stroke-width=\"5\" stroke-linecap=\"round\" stroke-dasharray=\"{span:0.0} {outer:0.0}\" stroke-dashoffset=\"{-from:0.0}\"/>");
        }

        if (length > 0.5)
        {
            svg.Append(Invariant, $"<circle cx=\"110\" cy=\"110\" r=\"90\" fill=\"none\" class=\"ring-val\" stroke-width=\"{width}\" stroke-linecap=\"round\" stroke-dasharray=\"{length:0.0} {RingLength:0.0}\"/>");
        }

        svg.Append("</g></svg>");
        return new HtmlString(svg.ToString());
    }

    /// <summary>
    /// A small ring without glow (the week strip).
    /// </summary>
    /// <param name="fraction">How full, 0–1.</param>
    /// <param name="cssColor">The stroke colour (a CSS value).</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent MiniRing(double fraction, string cssColor)
    {
        var length = Math.Clamp(fraction, 0, 1) * 94.25;
        return new HtmlString(string.Create(Invariant,
            $"<svg class=\"mini-ring\" viewBox=\"0 0 40 40\" aria-hidden=\"true\" style=\"--ring:{cssColor}\"><circle cx=\"20\" cy=\"20\" r=\"15\" fill=\"none\" class=\"ring-track\" stroke-width=\"4\"/><circle cx=\"20\" cy=\"20\" r=\"15\" fill=\"none\" stroke-width=\"4\" stroke-linecap=\"round\" stroke-dasharray=\"{length:0.0} 94.25\" transform=\"rotate(-90 20 20)\" class=\"ring-val\"/></svg>"));
    }

    /// <summary>
    /// The heart rate through a day, with the night and the workouts shaded and "now" marked.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <param name="points">5-minute heart-rate points.</param>
    /// <param name="sleepEnd">When last night ended (the sleep band runs from midnight to it).</param>
    /// <param name="workouts">The workouts.</param>
    /// <param name="now">The current time (today only).</param>
    /// <param name="label">What a screen reader hears.</param>
    /// <param name="width">The drawing width (wider for big cards, so the text keeps its size).</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent DayHeart(DateOnly day, IReadOnlyList<HeartRatePointDTO> points, DateTime? sleepEnd, IReadOnlyList<WorkoutDTO> workouts, DateTime? now, string label, double width = 350)
    {
        const double top = 8, bottom = 110;
        var start = day.ToDateTime(TimeOnly.MinValue);
        double X(DateTime time) => Math.Clamp((time - start).TotalMinutes / 1440 * width, 0, width);
        var low = Math.Min(40, points.Count > 0 ? points.Min(static p => p.Bpm) - 5 : 40);
        var high = Math.Max(150, points.Count > 0 ? points.Max(static p => p.Bpm) + 8 : 150);
        double Y(double bpm) => bottom - (bpm - low) / (high - low) * (bottom - top - 6);

        var svg = new StringBuilder();
        var id = NextId();
        svg.Append(Invariant, $"<svg class=\"chart-day\" viewBox=\"0 0 {width:0} 128\" role=\"img\" aria-label=\"{Escape(label)}\"><defs><linearGradient id=\"{id}\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#FF8A47\" stop-opacity=\"0.45\"/><stop offset=\"1\" stop-color=\"#FF8A47\" stop-opacity=\"0\"/></linearGradient></defs>");
        if (sleepEnd is { } wake && wake > start)
        {
            var w = X(wake);
            svg.Append(Invariant, $"<rect x=\"0\" y=\"{top}\" width=\"{w:0.0}\" height=\"{bottom - top}\" rx=\"10\" class=\"band-sleep\"/>");
            if (w > 40)
            {
                svg.Append(Invariant, $"<text x=\"8\" y=\"22\" class=\"band-label sleep\">Sleep</text>");
            }
        }

        foreach (var workout in workouts)
        {
            var x = X(workout.StartedAt);
            var w = Math.Max(6, X(workout.StartedAt.AddMinutes(workout.Minutes)) - x);
            svg.Append(Invariant, $"<rect x=\"{x:0.0}\" y=\"{top}\" width=\"{w:0.0}\" height=\"{bottom - top}\" rx=\"6\" class=\"band-workout\"/>");
        }

        if (points.Count > 1)
        {
            var line = string.Join(" L", points.Select(p => string.Create(Invariant, $"{X(p.At):0.0},{Y(p.Bpm):0.0}")));
            var last = points[^1];
            svg.Append(Invariant, $"<path d=\"M{X(points[0].At):0.0},{bottom} L{line} L{X(last.At):0.0},{bottom} Z\" fill=\"url(#{id})\"/>");
            svg.Append(Invariant, $"<path d=\"M{line}\" class=\"line-heart draw\" pathLength=\"1\"/>");
            if (now is { } clock)
            {
                var x = X(clock < last.At.AddMinutes(10) ? clock : last.At);
                svg.Append(Invariant, $"<line x1=\"{x:0.0}\" y1=\"{top}\" x2=\"{x:0.0}\" y2=\"{bottom}\" class=\"now-line\"/>");
                svg.Append(Invariant, $"<circle cx=\"{X(last.At):0.0}\" cy=\"{Y(last.Bpm):0.0}\" r=\"5\" class=\"now-dot\"/>");
            }
        }

        foreach (var (hour, text) in new[] { (0, "00h"), (6, "06h"), (12, "12h"), (18, "18h"), (24, "24h") })
        {
            var x = hour / 24.0 * width;
            var anchor = hour == 0 ? "start" : hour == 24 ? "end" : "middle";
            svg.Append(Invariant, $"<text x=\"{x:0.0}\" y=\"124\" text-anchor=\"{anchor}\" class=\"axis\">{text}</text>");
        }

        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    /// <summary>
    /// The stress monitor: one bar per 10-minute block from waking up, as tall as the stress (0–3) and coloured low,
    /// medium or high; blocks you were moving show as a short striped bar.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <param name="blocks">The blocks.</param>
    /// <param name="label">What a screen reader hears.</param>
    /// <param name="width">The drawing width.</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent Stress(DateOnly day, IReadOnlyList<StressBlockDTO> blocks, string label, double width = 520)
    {
        const double top = 8, bottom = 104, height = 124;
        var start = day.ToDateTime(TimeOnly.MinValue);
        var from = blocks.Count > 0 ? Math.Min(6, blocks[0].At.Hour) : 6;
        double X(DateTime time) => Math.Clamp(((time - start).TotalMinutes - from * 60) / ((24 - from) * 60) * width, 0, width);
        double Y(double level) => bottom - level / 3 * (bottom - top);
        var bar = Math.Max(1.2, X(start.AddHours(from).AddMinutes(10)) - X(start.AddHours(from)) - 0.8);

        var svg = new StringBuilder();
        svg.Append(Invariant, $"<svg class=\"chart-stress\" viewBox=\"0 0 {width:0} {height:0}\" role=\"img\" aria-label=\"{Escape(label)}\">");
        foreach (var level in new[] { 1, 2, 3 })
        {
            svg.Append(Invariant, $"<line x1=\"0\" y1=\"{Y(level):0.0}\" x2=\"{width:0}\" y2=\"{Y(level):0.0}\" class=\"lane\"/>");
        }

        var index = 0;
        foreach (var block in blocks)
        {
            var x = X(block.At);
            if (block.Active)
            {
                svg.Append(Invariant, $"<rect x=\"{x:0.0}\" y=\"{bottom - 10:0.0}\" width=\"{bar:0.0}\" height=\"10\" rx=\"1.5\" class=\"st-active\"/>");
            }
            else if (block.Level is { } level)
            {
                var y = Y(Math.Max(0.12, level));
                var css = level >= 2 ? "st-high" : level >= 1 ? "st-mid" : "st-low";
                svg.Append(Invariant, $"<rect x=\"{x:0.0}\" y=\"{y:0.0}\" width=\"{bar:0.0}\" height=\"{bottom - y:0.0}\" rx=\"1.5\" class=\"{css}\" style=\"--i:{index++ % 60}\"><title>{block.At:HH:mm} · {level.ToString("0.0", Invariant)}{(block.Bpm is { } bpm ? $" · {bpm} bpm" : string.Empty)}</title></rect>");
            }
        }

        svg.Append(Invariant, $"<line x1=\"0\" y1=\"{bottom:0}\" x2=\"{width:0}\" y2=\"{bottom:0}\" class=\"lane\"/>");
        for (var hour = from; hour <= 24; hour += 3)
        {
            var x = X(start.AddHours(hour));
            var anchor = hour == from ? "start" : hour >= 24 ? "end" : "middle";
            svg.Append(Invariant, $"<text x=\"{x:0.0}\" y=\"{height - 4:0}\" text-anchor=\"{anchor}\" class=\"axis\">{hour:00}h</text>");
        }

        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    /// <summary>The energy bank through the day: the charge from waking up, 0–100.</summary>
    /// <param name="day">The day.</param>
    /// <param name="points">The charge.</param>
    /// <param name="label">What a screen reader hears.</param>
    /// <param name="width">The drawing width.</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent Energy(DateOnly day, IReadOnlyList<TrendPointTimeDTO> points, string label, double width = 520)
    {
        const double top = 10, bottom = 104, height = 124;
        var start = day.ToDateTime(TimeOnly.MinValue);
        var from = points.Count > 0 ? Math.Min(6, points[0].At.Hour) : 6;
        double X(DateTime time) => Math.Clamp(((time - start).TotalMinutes - from * 60) / ((24 - from) * 60) * width, 0, width);
        double Y(double value) => bottom - value / 100 * (bottom - top);

        var id = NextId();
        var svg = new StringBuilder();
        svg.Append(Invariant, $"<svg class=\"chart-series ser-sky chart-energy\" viewBox=\"0 0 {width:0} {height:0}\" role=\"img\" aria-label=\"{Escape(label)}\"><defs><linearGradient id=\"{id}\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" class=\"stop-a\"/><stop offset=\"1\" class=\"stop-b\"/></linearGradient></defs>");
        foreach (var value in new[] { 25, 50, 75 })
        {
            svg.Append(Invariant, $"<line x1=\"0\" y1=\"{Y(value):0.0}\" x2=\"{width - 22:0}\" y2=\"{Y(value):0.0}\" class=\"lane\"/><text x=\"{width - 2:0}\" y=\"{Y(value) + 3.5:0.0}\" text-anchor=\"end\" class=\"axis\">{value}</text>");
        }

        if (points.Count > 1)
        {
            var line = string.Join(" L", points.Select(p => string.Create(Invariant, $"{X(p.At):0.0},{Y(p.Value):0.0}")));
            svg.Append(Invariant, $"<path d=\"M{X(points[0].At):0.0},{bottom} L{line} L{X(points[^1].At):0.0},{bottom} Z\" fill=\"url(#{id})\"/>");
            svg.Append(Invariant, $"<path d=\"M{line}\" class=\"series-line draw\" pathLength=\"1\"/>");
            svg.Append(Invariant, $"<circle cx=\"{X(points[^1].At):0.0}\" cy=\"{Y(points[^1].Value):0.0}\" r=\"4.5\" class=\"series-dot last\"/>");
        }

        for (var hour = from; hour <= 24; hour += 3)
        {
            var x = X(start.AddHours(hour));
            var anchor = hour == from ? "start" : hour >= 24 ? "end" : "middle";
            svg.Append(Invariant, $"<text x=\"{x:0.0}\" y=\"{height - 4:0}\" text-anchor=\"{anchor}\" class=\"axis\">{hour:00}h</text>");
        }

        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    /// <summary>
    /// The hypnogram: one lane per stage, the night from bedtime to getting up.
    /// </summary>
    /// <param name="sleep">The night.</param>
    /// <param name="width">The drawing width (wider for big cards, so the text keeps its size).</param>
    /// <returns>The SVG (empty when only totals were synced).</returns>
    public static IHtmlContent Hypnogram(SleepDTO sleep, double width = 360)
    {
        if (sleep.Segments.Count == 0 || sleep.BedStart is not { } start || sleep.BedEnd is not { } end || end <= start)
        {
            return HtmlString.Empty;
        }

        const double left = 60;
        var right = width - 8;
        var total = (end - start).TotalMinutes;
        double X(DateTime time) => left + Math.Clamp((time - start).TotalMinutes / total, 0, 1) * (right - left);
        static double Lane(SleepStage stage) => stage switch { SleepStage.Awake => 8, SleepStage.Rem => 40, SleepStage.Core => 72, _ => 104 };
        static string Css(SleepStage stage) => stage switch { SleepStage.Awake => "st-awake", SleepStage.Rem => "st-rem", SleepStage.Core => "st-core", _ => "st-deep" };

        var svg = new StringBuilder();
        svg.Append(Invariant, $"<svg class=\"chart-hypno\" viewBox=\"0 0 {width:0} 150\" role=\"img\" aria-label=\"Sleep stages from {start:HH:mm} to {end:HH:mm}, {sleep.Cycles} cycles\">");
        foreach (var (stage, name) in new[] { (SleepStage.Awake, "Awake"), (SleepStage.Rem, "REM"), (SleepStage.Core, "Light"), (SleepStage.Deep, "Deep") })
        {
            var y = Lane(stage);
            svg.Append(Invariant, $"<text x=\"0\" y=\"{y + 15}\" class=\"axis\">{name}</text><line x1=\"{left}\" y1=\"{y + 11}\" x2=\"{right}\" y2=\"{y + 11}\" class=\"lane\"/>");
        }

        var path = new StringBuilder();
        for (var index = 1; index < sleep.Segments.Count; index++)
        {
            var x = X(sleep.Segments[index].Start);
            path.Append(Invariant, $"M{x:0.0},{Lane(sleep.Segments[index - 1].Stage) + 11} V{Lane(sleep.Segments[index].Stage) + 11} ");
        }

        svg.Append(Invariant, $"<path d=\"{path}\" class=\"hypno-link\"/>");
        foreach (var segment in sleep.Segments)
        {
            var x = X(segment.Start);
            var w = Math.Max(2, X(segment.End) - x);
            svg.Append(Invariant, $"<rect x=\"{x:0.0}\" y=\"{Lane(segment.Stage)}\" width=\"{w:0.0}\" height=\"22\" rx=\"{Math.Min(5, w / 2):0.0}\" class=\"{Css(segment.Stage)}\"/>");
        }

        var middle = start.AddMinutes(total / 2);
        svg.Append(Invariant, $"<text x=\"{left}\" y=\"146\" class=\"axis\">{start:HH:mm}</text><text x=\"{(left + right) / 2:0.0}\" y=\"146\" text-anchor=\"middle\" class=\"axis\">{middle:HH:mm}</text><text x=\"{right}\" y=\"146\" text-anchor=\"end\" class=\"axis\">{end:HH:mm}</text>");
        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    /// <summary>
    /// A sparkline (stretches with its box; the line keeps its width).
    /// </summary>
    /// <param name="values">The values, oldest first (gaps allowed).</param>
    /// <param name="cssClass">The line's class (picks the colour).</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent Sparkline(IEnumerable<double?> values, string cssClass)
    {
        var list = values.ToList();
        var known = list.Where(static v => v is not null).Select(static v => v!.Value).ToList();
        if (known.Count < 2)
        {
            return new HtmlString("<svg class=\"spark\" viewBox=\"0 0 100 30\" preserveAspectRatio=\"none\" aria-hidden=\"true\"></svg>");
        }

        var (min, max) = (known.Min(), known.Max());
        var spread = Math.Max(max - min, Math.Abs(max) * 0.04 + 0.5);
        var points = list.Select((v, i) => (v, i)).Where(static p => p.v is not null)
            .Select(p => string.Create(Invariant, $"{p.i * 100.0 / (list.Count - 1):0.0},{26 - (p.v!.Value - min) / spread * 22:0.0}"));
        return new HtmlString($"<svg class=\"spark\" viewBox=\"0 0 100 30\" preserveAspectRatio=\"none\" aria-hidden=\"true\"><polyline points=\"{string.Join(' ', points)}\" class=\"{cssClass}\" vector-effect=\"non-scaling-stroke\"/></svg>");
    }

    /// <summary>
    /// A 30-day trend with an area under it, a band for your normal and the last point marked.
    /// </summary>
    /// <param name="points">The points, oldest first.</param>
    /// <param name="baseline">Your normal.</param>
    /// <param name="cssClass">The colour class (line-hrv).</param>
    /// <param name="label">What a screen reader hears.</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent Trend(IReadOnlyList<TrendPointDTO> points, double baseline, string cssClass, string label)
    {
        var known = points.Where(static p => p.Value is not null).ToList();
        if (known.Count < 2)
        {
            return HtmlString.Empty;
        }

        var low = Math.Min(known.Min(static p => p.Value!.Value), baseline) * 0.92;
        var high = Math.Max(known.Max(static p => p.Value!.Value), baseline) * 1.06;
        double X(int index) => index * 346.0 / Math.Max(1, points.Count - 1);
        double Y(double v) => 92 - (v - low) / (high - low) * 80;
        var id = NextId();
        var line = string.Join(" L", points.Select((p, i) => (p, i)).Where(static x => x.p.Value is not null).Select(x => string.Create(Invariant, $"{X(x.i):0.0},{Y(x.p.Value!.Value):0.0}")));
        var lastIndex = points.Count - 1 - points.Reverse().TakeWhile(static p => p.Value is null).Count();
        var last = points[lastIndex].Value!.Value;
        var firstIndex = points.ToList().FindIndex(static p => p.Value is not null);

        var svg = new StringBuilder();
        svg.Append(Invariant, $"<svg class=\"chart-trend {cssClass}\" viewBox=\"0 0 350 100\" role=\"img\" aria-label=\"{Escape(label)}\"><defs><linearGradient id=\"{id}\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" class=\"stop-a\"/><stop offset=\"1\" class=\"stop-b\"/></linearGradient></defs>");
        svg.Append(Invariant, $"<rect x=\"0\" y=\"{Y(baseline) - 10:0.0}\" width=\"350\" height=\"20\" rx=\"6\" class=\"band-normal\"/><line x1=\"0\" y1=\"{Y(baseline):0.0}\" x2=\"350\" y2=\"{Y(baseline):0.0}\" class=\"baseline\"/>");
        svg.Append(Invariant, $"<path d=\"M{line} L{X(lastIndex):0.0},100 L{X(firstIndex):0.0},100 Z\" fill=\"url(#{id})\"/>");
        svg.Append(Invariant, $"<path d=\"M{line}\" class=\"trend-line draw\" pathLength=\"1\"/>");
        svg.Append(Invariant, $"<circle cx=\"{X(lastIndex):0.0}\" cy=\"{Y(last):0.0}\" r=\"9\" class=\"trend-halo\"/><circle cx=\"{X(lastIndex):0.0}\" cy=\"{Y(last):0.0}\" r=\"4.5\" class=\"trend-dot\"/>");
        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    /// <summary>
    /// The strain line drawn over the 14-day recovery bars (stretches with its box).
    /// </summary>
    /// <param name="strains">The strain of each day.</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent StrainLine(IReadOnlyList<double?> strains)
    {
        var width = strains.Count * 100;
        var points = strains.Select((s, i) => (s, i)).Where(static p => p.s is not null)
            .Select(p => string.Create(Invariant, $"{p.i * 100 + 50},{200 - p.s!.Value / 21 * 180:0.0}"));
        return new HtmlString(string.Create(Invariant,
            $"<svg class=\"strain-overlay\" viewBox=\"0 0 {width} 200\" preserveAspectRatio=\"none\" aria-hidden=\"true\"><polyline points=\"{string.Join(' ', points)}\" class=\"line-strain\" vector-effect=\"non-scaling-stroke\"/></svg>"));
    }

    /// <summary>
    /// Biological age against the calendar over the last months.
    /// </summary>
    /// <param name="history">Points, oldest first.</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent AgeHistory(IReadOnlyList<AgePointDTO> history)
    {
        if (history.Count < 2)
        {
            return HtmlString.Empty;
        }

        var values = history.SelectMany(static p => new[] { p.BiologicalAge, p.ChronologicalAge }).ToList();
        var low = Math.Floor(values.Min() - 1);
        var high = Math.Ceiling(values.Max() + 1);
        double X(int index) => 24 + index * 316.0 / (history.Count - 1);
        double Y(double v) => 112 - (v - low) / (high - low) * 96;
        var id = NextId();
        var bio = string.Join(" L", history.Select((p, i) => string.Create(Invariant, $"{X(i):0.0},{Y(p.BiologicalAge):0.0}")));
        var calendar = string.Join(" L", history.Select((p, i) => string.Create(Invariant, $"{X(i):0.0},{Y(p.ChronologicalAge):0.0}")));

        var svg = new StringBuilder();
        svg.Append(Invariant, $"<svg class=\"chart-age\" viewBox=\"0 0 350 136\" role=\"img\" aria-label=\"Biological age over the last months, from {history[0].BiologicalAge:0.0} to {history[^1].BiologicalAge:0.0}\"><defs><linearGradient id=\"{id}\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\"><stop offset=\"0\" stop-color=\"#5BE3A6\"/><stop offset=\"1\" stop-color=\"#8E8CFF\"/></linearGradient><linearGradient id=\"{id}a\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#7CC7FF\" stop-opacity=\"0.28\"/><stop offset=\"1\" stop-color=\"#7CC7FF\" stop-opacity=\"0\"/></linearGradient></defs>");
        foreach (var tick in new[] { low, (low + high) / 2, high })
        {
            svg.Append(Invariant, $"<line x1=\"24\" y1=\"{Y(tick):0.0}\" x2=\"340\" y2=\"{Y(tick):0.0}\" class=\"lane\"/><text x=\"0\" y=\"{Y(tick) + 3:0.0}\" class=\"axis\">{tick:0}</text>");
        }

        svg.Append(Invariant, $"<path d=\"M{calendar}\" class=\"line-calendar\"/>");
        svg.Append(Invariant, $"<path d=\"M{bio} L{X(history.Count - 1):0.0},120 L{X(0):0.0},120 Z\" fill=\"url(#{id}a)\"/>");
        svg.Append(Invariant, $"<path d=\"M{bio}\" fill=\"none\" stroke=\"url(#{id})\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
        for (var index = 0; index < history.Count; index++)
        {
            svg.Append(Invariant, $"<circle cx=\"{X(index):0.0}\" cy=\"{Y(history[index].BiologicalAge):0.0}\" r=\"{(index == history.Count - 1 ? 5.5 : 3.5)}\" class=\"age-dot\"/>");
        }

        foreach (var (point, index) in history.Select((p, i) => (p, i)).Where(x => x.i == 0 || x.i == history.Count - 1 || x.i == history.Count / 2))
        {
            svg.Append(Invariant, $"<text x=\"{X(index):0.0}\" y=\"134\" text-anchor=\"middle\" class=\"axis\">{point.Date.ToString("MMM", CultureInfo.GetCultureInfo("en-IE"))}</text>");
        }

        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    /// <summary>One point of a <see cref="Series"/>.</summary>
    /// <param name="Date">The day (or the first day of the week it stands for).</param>
    /// <param name="Value">The value, or null for a gap.</param>
    /// <param name="Low">The bottom of the band behind the point (your typical range), if any.</param>
    /// <param name="High">The top of the band, if any.</param>
    /// <param name="Status">A class for the point's dot (in, out, far), if any.</param>
    public sealed record SeriesPoint(DateOnly Date, double? Value, double? Low = null, double? High = null, string? Status = null);

    /// <summary>
    /// A line over days or weeks: the area under it, an optional band behind it (your typical range, day by day), dots
    /// coloured by status, the lowest and highest value on the right and a few dates under it. Used by trends, reports
    /// and the Health Monitor.
    /// </summary>
    /// <param name="points">The points, oldest first.</param>
    /// <param name="cssClass">The colour class (ser-rec, ser-slp, ser-str, ser-sky, ser-rose, ser-mid).</param>
    /// <param name="label">What a screen reader hears.</param>
    /// <param name="decimals">Decimals of the axis labels.</param>
    /// <param name="width">The drawing width.</param>
    /// <param name="height">The drawing height.</param>
    /// <param name="dots">Whether to draw a dot on every point.</param>
    /// <param name="floor">A value the axis must include (0 for strain, for example).</param>
    /// <param name="smooth">Points of a rolling average drawn over a faded daily line (0 = none).</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent Series(IReadOnlyList<SeriesPoint> points, string cssClass, string label, int decimals = 0, double width = 640, double height = 150, bool dots = false, double? floor = null, int smooth = 0)
    {
        var known = points.Where(static p => p.Value is not null).ToList();
        if (known.Count < 2)
        {
            return new HtmlString("<p class=\"muted small-note\">Not enough days yet to draw this.</p>");
        }

        const double top = 10, left = 4, right = 40, bottomPad = 22;
        var values = known.Select(static p => p.Value!.Value)
            .Concat(points.Where(static p => p.Low is not null).Select(static p => p.Low!.Value))
            .Concat(points.Where(static p => p.High is not null).Select(static p => p.High!.Value))
            .Concat(floor is { } f ? [f] : [])
            .ToList();
        var low = values.Min();
        var high = values.Max();
        var pad = Math.Max((high - low) * 0.12, Math.Abs(high) * 0.02 + 0.5);
        low = floor is { } floorValue && floorValue >= low - pad ? Math.Min(low, floorValue) : low - pad;
        high += pad;
        var plotRight = width - right;
        var bottom = height - bottomPad;
        double X(int index) => left + index * (plotRight - left) / Math.Max(1, points.Count - 1);
        double Y(double value) => bottom - (value - low) / (high - low) * (bottom - top);
        string Fmt(double value) => value.ToString(decimals == 0 ? "0" : "0.0", Invariant);

        var id = NextId();
        var svg = new StringBuilder();
        svg.Append(Invariant, $"<svg class=\"chart-series {cssClass}\" viewBox=\"0 0 {width:0} {height:0}\" role=\"img\" aria-label=\"{Escape(label)}\"><defs><linearGradient id=\"{id}\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" class=\"stop-a\"/><stop offset=\"1\" class=\"stop-b\"/></linearGradient></defs>");

        foreach (var tick in new[] { high - pad, (high + low) / 2, low + pad })
        {
            svg.Append(Invariant, $"<line x1=\"{left:0}\" y1=\"{Y(tick):0.0}\" x2=\"{plotRight:0}\" y2=\"{Y(tick):0.0}\" class=\"lane\"/><text x=\"{width - 2:0}\" y=\"{Y(tick) + 3.5:0.0}\" text-anchor=\"end\" class=\"axis\">{Fmt(tick)}</text>");
        }

        // The band: your typical range, which moves day by day.
        var banded = points.Select((p, i) => (p, i)).Where(static x => x.p.Low is not null && x.p.High is not null).ToList();
        if (banded.Count > 1)
        {
            var upper = string.Join(" L", banded.Select(x => string.Create(Invariant, $"{X(x.i):0.0},{Y(x.p.High!.Value):0.0}")));
            var lower = string.Join(" L", banded.AsEnumerable().Reverse().Select(x => string.Create(Invariant, $"{X(x.i):0.0},{Y(x.p.Low!.Value):0.0}")));
            svg.Append(Invariant, $"<path d=\"M{upper} L{lower} Z\" class=\"band-typical\"/>");
        }

        // The line breaks at gaps of more than a few points; the area follows the line.
        var segments = new List<List<(double X, double Y)>>();
        var current = new List<(double X, double Y)>();
        var missing = 0;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Value is not { } value)
            {
                missing++;
                continue;
            }

            if (missing > 3 && current.Count > 0)
            {
                segments.Add(current);
                current = [];
            }

            missing = 0;
            current.Add((X(i), Y(value)));
        }

        if (current.Count > 0)
        {
            segments.Add(current);
        }

        foreach (var segment in segments)
        {
            var line = string.Join(" L", segment.Select(p => string.Create(Invariant, $"{p.X:0.0},{p.Y:0.0}")));
            if (banded.Count <= 1 && segment.Count > 1)
            {
                svg.Append(Invariant, $"<path d=\"M{line} L{segment[^1].X:0.0},{bottom:0.0} L{segment[0].X:0.0},{bottom:0.0} Z\" fill=\"url(#{id})\"/>");
            }

            svg.Append(Invariant, $"<path d=\"M{line}\" class=\"series-line draw{(smooth > 1 ? " faded" : string.Empty)}\" pathLength=\"1\"/>");
        }

        if (smooth > 1)
        {
            var averaged = points.Select((p, i) =>
            {
                var window = points.Skip(Math.Max(0, i - smooth + 1)).Take(Math.Min(smooth, i + 1)).Where(static w => w.Value is not null).Select(static w => w.Value!.Value).ToList();
                return (i, Value: window.Count >= Math.Max(2, smooth / 2) ? window.Average() : (double?)null);
            }).Where(static x => x.Value is not null).ToList();
            if (averaged.Count > 1)
            {
                svg.Append(Invariant, $"<path d=\"M{string.Join(" L", averaged.Select(x => string.Create(Invariant, $"{X(x.i):0.0},{Y(x.Value!.Value):0.0}")))}\" class=\"series-line smooth draw\" pathLength=\"1\"/>");
            }
        }

        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Value is not { } value)
            {
                continue;
            }

            var isLast = i == points.Count - 1 || points.Skip(i + 1).All(static p => p.Value is null);
            if (dots || isLast || points[i].Status is "out" or "far")
            {
                var status = points[i].Status is { } st ? " " + st : string.Empty;
                svg.Append(Invariant, $"<circle cx=\"{X(i):0.0}\" cy=\"{Y(value):0.0}\" r=\"{(isLast ? 4.5 : 3)}\" class=\"series-dot{status}{(isLast ? " last" : string.Empty)}\"/>");
            }
        }

        // A few dates under the line.
        var labels = points.Count <= 1 ? [0] : new[] { 0, points.Count / 4, points.Count / 2, points.Count * 3 / 4, points.Count - 1 }.Distinct();
        var span = points.Count > 1 ? points[^1].Date.DayNumber - points[0].Date.DayNumber : 0;
        foreach (var index in labels)
        {
            var text = span > 200 ? points[index].Date.ToString("MMM", Invariant) : points[index].Date.ToString("d MMM", Invariant);
            var anchor = index == 0 ? "start" : index == points.Count - 1 ? "end" : "middle";
            svg.Append(Invariant, $"<text x=\"{X(index):0.0}\" y=\"{height - 4:0}\" text-anchor=\"{anchor}\" class=\"axis\">{text}</text>");
        }

        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    private static string NextId() => "k" + Interlocked.Increment(ref _ids).ToString(Invariant);

    private static string Escape(string text) => System.Net.WebUtility.HtmlEncode(text);
}
