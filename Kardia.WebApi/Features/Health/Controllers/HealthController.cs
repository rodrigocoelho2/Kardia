using System.Net;
using System.Net.Mime;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Features.Users.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using Kardia.WebApi.Features.Health.Models.DTOs;

namespace Kardia.WebApi.Features.Health.Controllers;

/// <summary>
/// The health check: no key needed, tells nothing private.
/// </summary>
/// <param name="userRepository">Accounts (a count proves the database answers).</param>
/// <param name="options">Module options.</param>
/// <param name="logger">Logger.</param>
[AllowAnonymous]
[Route("health")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
internal sealed class HealthController(IUserRepository userRepository, IOptions<KardiaOptions> options, ILogger<HealthController> logger) : InternalControllerBase
{
    /// <summary>Checks the WebApi and the database.</summary>
    /// <returns>200 when healthy, 503 otherwise.</returns>
    [HttpGet]
    [ProducesResponseType<HealthDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType<HealthDTO>((int)HttpStatusCode.ServiceUnavailable)]
    [SwaggerOperation(Summary = "HealthGet", OperationId = "HealthGet")]
    public async Task<ActionResult<HealthDTO>> HealthGetAsync()
    {
        try
        {
            await userRepository.CountAsync();
            return Ok(new HealthDTO { Database = true, DemoMode = options.Value.DemoMode });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Health check: the database didn't answer.");
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new HealthDTO { Status = "database unavailable", DemoMode = options.Value.DemoMode });
        }
    }
}
