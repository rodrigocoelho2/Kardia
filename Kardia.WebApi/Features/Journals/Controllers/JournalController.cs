using System.Net;
using System.Net.Mime;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.Journals.Models.DTOs;
using Kardia.WebApi.Features.Journals.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.Journals.Controllers;

/// <summary>
/// The journal: habits per day and their effect on recovery.
/// </summary>
/// <param name="journalService">Journal business logic.</param>
[Authorize]
[Route("journal")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class JournalController(IJournalService journalService) : InternalControllerBase
{
    private readonly IJournalService _journalService = journalService;

    /// <summary>Gets a day's journal.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The day (today when empty).</param>
    /// <returns>The journal.</returns>
    [HttpGet]
    [ProducesResponseType<JournalDayDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "JournalGet", OperationId = "JournalGet")]
    public async Task<ActionResult<JournalDayDTO>> JournalGetAsync(string userId, DateOnly? date = null) => (await _journalService.GetAsync(userId, date)).ToActionResult();

    /// <summary>Marks or unmarks a habit.</summary>
    /// <param name="item">The habit, day and state.</param>
    /// <returns>The day's journal.</returns>
    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<JournalDayDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "JournalSet", OperationId = "JournalSet")]
    public async Task<ActionResult<JournalDayDTO>> JournalSetAsync(JournalSetRequest item) => (await _journalService.SetAsync(item)).ToActionResult();
}
