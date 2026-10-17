using System.Net;
using System.Net.Mime;
using Kardia.Library.Enums;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.Reports.Models.DTOs;
using Kardia.WebApi.Features.Reports.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.Reports.Controllers;

/// <summary>Weekly and monthly performance assessments.</summary>
/// <param name="reportService">Report business logic.</param>
[Authorize]
[Route("report")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class ReportController(IReportService reportService) : InternalControllerBase
{
    /// <summary>Gets the report of the week or month around a day.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="period">Week or Month.</param>
    /// <param name="date">A day inside it, yyyy-MM-dd (today when empty).</param>
    /// <returns>The report.</returns>
    [HttpGet]
    [ProducesResponseType<ReportDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ReportGet", OperationId = "ReportGet")]
    public async Task<ActionResult<ReportDTO>> ReportGetAsync(string userId, ReportPeriod period = ReportPeriod.Week, DateOnly? date = null) =>
        (await reportService.GetAsync(userId, period, date)).ToActionResult();
}
