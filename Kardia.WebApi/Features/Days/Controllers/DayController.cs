using System.Net;
using System.Net.Mime;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.Days.Models.DTOs;
using Kardia.WebApi.Features.Days.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.Days.Controllers;

/// <summary>
/// Days: recovery, sleep, strain and the coach for one day, and ranges of days for charts.
/// </summary>
/// <param name="dayService">Day business logic.</param>
[Authorize]
[Route("day")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class DayController(IDayService dayService) : InternalControllerBase
{
    private readonly IDayService _dayService = dayService;

    /// <summary>Gets one day.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The day, yyyy-MM-dd (today when empty).</param>
    /// <returns>The day.</returns>
    [HttpGet]
    [ProducesResponseType<DayDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "DayGet", OperationId = "DayGet")]
    public async Task<ActionResult<DayDTO>> DayGetAsync(string userId, DateOnly? date = null) => (await _dayService.GetAsync(userId, date)).ToActionResult();

    /// <summary>Gets a range of days in short.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day.</param>
    /// <returns>One summary per day.</returns>
    [HttpGet("range")]
    [ProducesResponseType<List<DaySummaryDTO>>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [SwaggerOperation(Summary = "DayRange", OperationId = "DayRange")]
    public async Task<ActionResult<List<DaySummaryDTO>>> DayRangeAsync(string userId, DateOnly from, DateOnly to) => (await _dayService.RangeAsync(userId, from, to)).ToActionResult();
}
