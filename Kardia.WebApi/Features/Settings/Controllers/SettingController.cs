using System.Net;
using System.Net.Mime;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.Settings.Models.DTOs;
using Kardia.WebApi.Features.Settings.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.Settings.Controllers;

/// <summary>Goals (the weekly plan and the sleep goal) and notification choices.</summary>
/// <param name="settingService">The settings service.</param>
[Authorize]
[Route("setting")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class SettingController(ISettingService settingService) : InternalControllerBase
{
    /// <summary>Gets an account's settings.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The settings.</returns>
    [HttpGet]
    [ProducesResponseType<SettingDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "SettingGet", OperationId = "SettingGet")]
    public async Task<ActionResult<SettingDTO>> SettingGetAsync(string userId) => (await settingService.GetAsync(userId)).ToActionResult();

    /// <summary>Saves an account's settings.</summary>
    /// <param name="item">The settings.</param>
    /// <returns>The saved settings.</returns>
    [HttpPost]
    [ProducesResponseType<SettingDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "SettingSave", OperationId = "SettingSave")]
    public async Task<ActionResult<SettingDTO>> SettingSaveAsync(SettingDTO item) => (await settingService.SaveAsync(item)).ToActionResult();
}
