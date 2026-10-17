namespace Kardia.Website.Helpers;

/// <summary>
/// The appearance (Settings › Appearance): dark (the default), light, or follow the device. Kept in a cookie so the
/// page is drawn in the right colours from the first byte, on every device separately.
/// </summary>
public static class Theme
{
    /// <summary>The cookie name.</summary>
    public const string CookieName = "kardia-theme";

    /// <summary>The choices.</summary>
    public static readonly string[] Choices = ["system", "dark", "light"];

    /// <summary>The appearance of this request.</summary>
    /// <param name="context">The request.</param>
    /// <returns>system, dark or light.</returns>
    public static string Of(HttpContext context) =>
        context.Request.Cookies.TryGetValue(CookieName, out var value) && Choices.Contains(value) ? value : "dark";

    /// <summary>Saves the appearance for this browser (one year).</summary>
    /// <param name="context">The request.</param>
    /// <param name="value">system, dark or light.</param>
    public static void Save(HttpContext context, string? value) =>
        context.Response.Cookies.Append(CookieName, Choices.Contains(value) ? value! : "dark", new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
        });
}
