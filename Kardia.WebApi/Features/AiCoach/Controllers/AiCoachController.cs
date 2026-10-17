using System.Net;
using System.Net.Mime;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.AiCoach.Models.DTOs;
using Kardia.WebApi.Features.AiCoach.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.AiCoach.Controllers;

/// <summary>The AI coach: a short report on the last 30 days, written by the AI provider set up on the server.</summary>
/// <param name="aiCoachService">AI coach business logic.</param>
[Authorize]
[Route("aicoach")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class AiCoachController(IAiCoachService aiCoachService) : InternalControllerBase
{
    /// <summary>Gets whether the AI coach is set up, whether it's working, and the last report.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    [HttpGet]
    [ProducesResponseType<AiCoachDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "AiCoachGet", OperationId = "AiCoachGet")]
    public async Task<ActionResult<AiCoachDTO>> AiCoachGetAsync(string userId) => (await aiCoachService.GetAsync(userId)).ToActionResult();

    /// <summary>Starts an analysis of the last 30 days (poll GET for the report).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    [HttpPost("analyse")]
    [ProducesResponseType<AiCoachDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "AiCoachAnalyse", OperationId = "AiCoachAnalyse")]
    public async Task<ActionResult<AiCoachDTO>> AiCoachAnalyseAsync(string userId) => (await aiCoachService.AnalyseAsync(userId)).ToActionResult();
}
