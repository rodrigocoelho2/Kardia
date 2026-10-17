using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Kardia.Sdk;
using Kardia.Website.Security;
using Microsoft.AspNetCore.HttpOverrides;

namespace Kardia.Website;

/// <summary>
/// Service registration and request pipeline of the Website.
/// </summary>
internal static class KardiaWebsiteStartup
{
    /// <summary>The rate limit of the public Apple Health address (/api/sync).</summary>
    public const string SyncRateLimit = "sync";

    /// <summary>
    /// Registers MVC, anti-forgery, the sign-in (cookie), the sync rate limit and the WebApi client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKardiaWebsite(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WebsiteOptions>(configuration.GetSection(WebsiteOptions.SectionName));
        Helpers.Clock.Use(configuration.GetSection(WebsiteOptions.SectionName).Get<WebsiteOptions>()?.TimeZone ?? "Europe/Lisbon");
        services.AddKardiaAuthentication(configuration);

        services.AddControllersWithViews()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // The page scripts send the token in this header on every POST.
        services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

        services.AddKardiaClient(configuration);

        // Health Auto Export sends every few minutes at most; this only stops a runaway loop or someone guessing keys.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static (context, _) =>
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Kardia.Website.Sync")
                    .LogWarning("Sync refused (429): more than 30 requests a minute from {Address}.", context.HttpContext.Connection.RemoteIpAddress);
                return ValueTask.CompletedTask;
            };
            options.AddPolicy(SyncRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                static _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        // Behind a reverse proxy (Nginx Proxy Manager, Cloudflare) the request arrives as plain HTTP: take the real scheme
        // and client from X-Forwarded-Proto / X-Forwarded-For, so links and the cookie are HTTPS.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    /// <summary>
    /// Configures the request pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseKardiaWebsite(this WebApplication app)
    {
        app.UseForwardedHeaders();

        var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

        // Static files are public (the landing page, the sign-in form and the app icons need them).
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypes,
            // Revalidate on every load so a new version shows up right after a redeploy.
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
        });

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(name: "default", pattern: "{controller=Dashboard}/{action=Index}");

        return app;
    }
}
