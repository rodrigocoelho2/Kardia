using System.Net;
using System.Net.Mime;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.Plans.Models.DTOs;
using Kardia.WebApi.Features.Plans.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.Plans.Controllers;

/// <summary>The weekly plan: sleep, bedtime, zone 2+ minutes and steps for Monday to Sunday.</summary>
/// <param name="planService">Plan business logic.</param>
[Authorize]
[Route("plan")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class PlanController(IPlanService planService) : InternalControllerBase
{
    /// <summary>Gets this week's plan.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The plan.</returns>
    [HttpGet]
    [ProducesResponseType<PlanDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PlanGet", OperationId = "PlanGet")]
    public async Task<ActionResult<PlanDTO>> PlanGetAsync(string userId) => (await planService.GetAsync(userId)).ToActionResult();
}
