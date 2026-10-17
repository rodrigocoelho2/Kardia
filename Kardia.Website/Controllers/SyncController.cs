using Kardia.Library.Constants;
using Kardia.Sdk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kardia.Website.Controllers;

/// <summary>
/// The public address Apple Health data is sent to: Health Auto Export's REST API automation or the Kardia Shortcut.
/// No cookie: the personal key from Settings › Apple Watch goes in the X-Kardia-Key header (or "Authorization: Bearer …",
/// or "key" in the body). The body is passed to the WebApi as it came.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="logger">Logger (never logs the key).</param>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class SyncController(KardiaClient apiClient, ILogger<SyncController> logger) : Controller
{
    /// <summary>Imports an Apple Health export.</summary>
    /// <returns>What was imported, or the problem.</returns>
    [HttpPost("/api/sync")]
    [EnableRateLimiting(KardiaWebsiteStartup.SyncRateLimit)]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public async Task<IActionResult> IngestAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        var key = Request.Headers.TryGetValue(ApiHeaders.SyncKey, out var header) ? header.ToString()
            : authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization[7..].Trim()
            : null;

        try
        {
            // Read the whole export first, then hand it to the WebApi in one go (with its length), and don't cancel that hand-over
            // if the iPhone stops waiting: the WebApi finishes saving either way.
            using var buffer = new MemoryStream();
            await Request.Body.CopyToAsync(buffer, HttpContext.RequestAborted);
            buffer.Position = 0;
            return Json(await apiClient.SyncIngestAsync(key, buffer, CancellationToken.None));
        }
        catch (KardiaApiException exception)
        {
            return StatusCode((int)exception.StatusCode, new { error = exception.ToValidationErrors().FirstOrDefault()?.Message ?? exception.Message });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Sync: the WebApi didn't answer.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Kardia's engine is not answering. Try again in a minute." });
        }
    }

    /// <summary>A friendly answer for someone opening the address in a browser.</summary>
    /// <returns>A short explanation.</returns>
    [HttpGet("/api/sync")]
    public IActionResult Explain() =>
        Json(new { message = "This is where Health Auto Export or the Kardia Shortcut send Apple Health data (POST, JSON). Set it up in Kardia › Settings › Apple Watch." });
}
