using Microsoft.AspNetCore.Html;

namespace Kardia.Website.Helpers;

/// <summary>
/// The line icons (24×24, stroke = currentColor). Shipped with the app: nothing loads from the internet.
/// </summary>
public static class Icons
{
    private static readonly Dictionary<string, string> Paths = new(StringComparer.Ordinal)
    {
        ["today"] = "<circle cx=\"12\" cy=\"12\" r=\"8\"/><circle cx=\"12\" cy=\"12\" r=\"4\"/>",
        ["recovery"] = "<path d=\"M3 12h4l2-5 4 10 2-5h6\"/>",
        ["sleep"] = "<path d=\"M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5z\"/>",
        ["strain"] = "<path d=\"M12 3c1 3.5 5 5.5 5 10a5 5 0 0 1-10 0c0-2.5 1.5-4 2.5-5 .3 1.6 1.2 2.6 2.5 3 0-3-1-5.5 0-8z\"/>",
        ["age"] = "<path d=\"M7 3h10M7 21h10M8 3c0 4 8 5 8 9s-8 5-8 9M16 3c0 4-8 5-8 9s8 5 8 9\"/>",
        ["journal"] = "<path d=\"M6 3h10l3 3v15H6z\"/><path d=\"M9 9h7M9 13h7M9 17h4\"/>",
        ["settings"] = "<circle cx=\"12\" cy=\"12\" r=\"3\"/><path d=\"M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z\"/>",
        ["bell"] = "<path d=\"M6 16V11a6 6 0 0 1 12 0v5l1.5 2h-15z\"/><path d=\"M10 20.5a2 2 0 0 0 4 0\"/>",
        ["left"] = "<path d=\"M15 5l-7 7 7 7\"/>",
        ["right"] = "<path d=\"M9 5l7 7-7 7\"/>",
        ["arrow"] = "<path d=\"M5 12h14M13 6l6 6-6 6\"/>",
        ["down"] = "<path d=\"M12 5v14M6 13l6 6 6-6\"/>",
        ["plus"] = "<path d=\"M12 5v14M5 12h14\"/>",
        ["sparkle"] = "<path d=\"M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z\"/>",
        ["run"] = "<circle cx=\"14\" cy=\"4.5\" r=\"1.8\"/><path d=\"M7 21l3-6 3 2v5M10 15l1.5-5 3.5 2.5 3 .5M8 10l3.5-2\"/>",
        ["walk"] = "<circle cx=\"12\" cy=\"4.5\" r=\"1.8\"/><path d=\"M9 21l2-6 2 2v4M11 15l1-5 2 3h3M8 13l3-3\"/>",
        ["ride"] = "<circle cx=\"6\" cy=\"16\" r=\"3.5\"/><circle cx=\"18\" cy=\"16\" r=\"3.5\"/><path d=\"M6 16l4-7h5l3 7M10 9l3 7\"/>",
        ["lift"] = "<path d=\"M4 9v6M7 7v10M17 7v10M20 9v6M7 12h10\"/>",
        ["swim"] = "<path d=\"M3 17c2 0 2-1.5 4.5-1.5S10 17 12 17s2.5-1.5 4.5-1.5S19 17 21 17\"/><circle cx=\"16\" cy=\"7\" r=\"1.8\"/><path d=\"M6 13l4-4 3 2 3-2\"/>",
        ["other"] = "<circle cx=\"12\" cy=\"12\" r=\"8\"/><path d=\"M9 12l2 2 4-4\"/>",
        ["heart"] = "<path d=\"M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z\"/>",
        ["lungs"] = "<path d=\"M3 9h11a3 3 0 1 0-3-3M3 15h15a3 3 0 1 1-3 3\"/>",
        ["temp"] = "<path d=\"M10 14V5a2 2 0 1 1 4 0v9a4 4 0 1 1-4 0z\"/>",
        ["drop"] = "<path d=\"M12 3s6 6.5 6 11a6 6 0 0 1-12 0c0-4.5 6-11 6-11z\"/>",
        ["steps"] = "<path d=\"M7 17c-1.7 0-3-1.3-3-3.5S5 9 7 9s2.5 2.4 2.5 4.5S8.7 17 7 17zM16.5 12c-1.7 0-2.5-2.1-2.5-4.2S15 3 17 3s3 1.8 3 4.5-1.8 4.5-3.5 4.5zM6 19.5h2.5M15.5 14.5H18\"/>",
        ["kcal"] = "<path d=\"M12 3c1 3.5 5 5.5 5 10a5 5 0 0 1-10 0c0-2.5 1.5-4 2.5-5 .3 1.6 1.2 2.6 2.5 3 0-3-1-5.5 0-8z\"/>",
        ["clock"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 7v5l3 2\"/>",
        ["calendar"] = "<rect x=\"4\" y=\"5\" width=\"16\" height=\"15\" rx=\"3\"/><path d=\"M4 10h16M9 3v4M15 3v4\"/>",
        ["watch"] = "<rect x=\"6\" y=\"6\" width=\"12\" height=\"12\" rx=\"4\"/><path d=\"M9 6l1-3h4l1 3M9 18l1 3h4l1-3\"/>",
        ["key"] = "<circle cx=\"8\" cy=\"15\" r=\"4\"/><path d=\"M11 12l8-8M16 7l2 2M14 9l2 2\"/>",
        ["copy"] = "<rect x=\"8\" y=\"8\" width=\"12\" height=\"12\" rx=\"3\"/><path d=\"M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2\"/>",
        ["check"] = "<path d=\"M5 12.5l4.5 4.5L19 7.5\"/>",
        ["logout"] = "<path d=\"M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3M10 16l-4-4 4-4M6 12h10\"/>",
        ["info"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 11v6M12 7.5v.5\"/>",
        ["up"] = "<path d=\"M7 14l5-5 5 5\"/>",
        ["trend"] = "<path d=\"M4 19V5M4 19h16M8 15l4-4 3 3 5-6\"/>",
        ["user"] = "<circle cx=\"12\" cy=\"8.5\" r=\"4\"/><path d=\"M4.5 20a7.5 7.5 0 0 1 15 0\"/>",
        ["lock"] = "<rect x=\"4.5\" y=\"10.5\" width=\"15\" height=\"10\" rx=\"3\"/><path d=\"M8 10.5V8a4 4 0 0 1 8 0v2.5\"/>",
        ["eye"] = "<path d=\"M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12Z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
        ["shield"] = "<path d=\"M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6zM9 12l2 2 4-4\"/>",
        ["server"] = "<rect x=\"3.5\" y=\"4\" width=\"17\" height=\"7\" rx=\"2.5\"/><rect x=\"3.5\" y=\"13\" width=\"17\" height=\"7\" rx=\"2.5\"/><path d=\"M7.5 7.5h.01M7.5 16.5h.01\"/>",
        ["sync"] = "<path d=\"M20 11a8 8 0 0 0-14.5-4.5L4 8M4 4v4h4M4 13a8 8 0 0 0 14.5 4.5L20 16M20 20v-4h-4\"/>",
        ["trash"] = "<path d=\"M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13\"/>",
        ["grid"] = "<rect x=\"4\" y=\"4\" width=\"6.5\" height=\"6.5\" rx=\"2\"/><rect x=\"13.5\" y=\"4\" width=\"6.5\" height=\"6.5\" rx=\"2\"/><rect x=\"4\" y=\"13.5\" width=\"6.5\" height=\"6.5\" rx=\"2\"/><rect x=\"13.5\" y=\"13.5\" width=\"6.5\" height=\"6.5\" rx=\"2\"/>",
        ["sun"] = "<circle cx=\"12\" cy=\"12\" r=\"4\"/><path d=\"M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.3 5.3l1.4 1.4M17.3 17.3l1.4 1.4M5.3 18.7l1.4-1.4M17.3 6.7l1.4-1.4\"/>",
        ["moon"] = "<path d=\"M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5z\"/>",
        ["device"] = "<rect x=\"3\" y=\"4.5\" width=\"18\" height=\"12\" rx=\"2.5\"/><path d=\"M8.5 20h7M12 16.5V20\"/>",
        ["dna"] = "<path d=\"M7 3c0 6 10 6 10 12s-10 3-10 6M17 3c0 3-10 6-10 9s10 6 10 9M9 6h6M8 18h8M9.5 12h5\"/>",
    };

    /// <summary>
    /// An icon.
    /// </summary>
    /// <param name="name">Its name.</param>
    /// <param name="cssClass">An extra class.</param>
    /// <returns>The SVG (decorative: hidden from screen readers).</returns>
    public static IHtmlContent Get(string name, string? cssClass = null) =>
        new HtmlString($"<svg class=\"ic{(cssClass is null ? string.Empty : " " + cssClass)}\" viewBox=\"0 0 24 24\" aria-hidden=\"true\">{(Paths.TryGetValue(name, out var path) ? path : Paths["other"])}</svg>");

    /// <summary>
    /// The Kardia mark: a ring that's three-quarters full with a heartbeat across it.
    /// </summary>
    /// <param name="size">The size in px.</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent Logo(int size = 32) => new HtmlString(
        $"<svg class=\"logo-mark\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 32 32\" fill=\"none\" aria-hidden=\"true\"><circle cx=\"16\" cy=\"16\" r=\"13\" stroke=\"#5BE3A6\" stroke-width=\"3\" stroke-dasharray=\"62 82\" stroke-linecap=\"round\" transform=\"rotate(-90 16 16)\"/><path d=\"M8 16h4l2-4 4 8 2-4h4\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>");
}
