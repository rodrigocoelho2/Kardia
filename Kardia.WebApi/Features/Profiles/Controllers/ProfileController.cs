using System.Net;
using System.Net.Mime;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.Profiles.Models.DTOs;
using Kardia.WebApi.Features.Profiles.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.Profiles.Controllers;

/// <summary>
/// Your profile: what biological age, strain and sleep need are calculated from.
/// </summary>
/// <param name="profileService">Profile business logic.</param>
[Authorize]
[Route("profile")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class ProfileController(IProfileService profileService) : InternalControllerBase
{
    private readonly IProfileService _profileService = profileService;

    /// <summary>Gets the profile.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The profile.</returns>
    [HttpGet]
    [ProducesResponseType<ProfileDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ProfileGet", OperationId = "ProfileGet")]
    public async Task<ActionResult<ProfileDTO>> ProfileGetAsync(string userId) => (await _profileService.GetAsync(userId)).ToActionResult();

    /// <summary>Saves the profile.</summary>
    /// <param name="item">The profile.</param>
    /// <returns>The saved profile.</returns>
    [HttpPost]
    [ProducesResponseType<ProfileDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ProfileSave", OperationId = "ProfileSave")]
    public async Task<ActionResult<ProfileDTO>> ProfileSaveAsync(ProfileSaveRequest item) => (await _profileService.SaveAsync(item)).ToActionResult();
}
