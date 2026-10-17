using System.Net;
using System.Net.Mime;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.Ages.Models.DTOs;
using Kardia.WebApi.Features.Ages.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.Ages.Controllers;

/// <summary>
/// Biological age.
/// </summary>
/// <param name="ageService">Age business logic.</param>
[Authorize]
[Route("age")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class AgeController(IAgeService ageService) : InternalControllerBase
{
    private readonly IAgeService _ageService = ageService;

    /// <summary>Gets your biological age.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The age.</returns>
    [HttpGet]
    [ProducesResponseType<AgeDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "AgeGet", OperationId = "AgeGet")]
    public async Task<ActionResult<AgeDTO>> AgeGetAsync(string userId) => (await _ageService.GetAsync(userId)).ToActionResult();
}
