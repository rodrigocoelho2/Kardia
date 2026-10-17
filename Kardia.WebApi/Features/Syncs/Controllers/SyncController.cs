using System.Net;
using System.Net.Mime;
using System.Text.Json;
using Kardia.Library.Constants;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.Syncs.Models.DTOs;
using Kardia.WebApi.Features.Syncs.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.Syncs.Controllers;

/// <summary>
/// The Apple Health connection. The Website forwards what Health Auto Export / the Shortcut send to its public
/// /api/sync address here, with the personal key in <see cref="ApiHeaders.SyncKey"/>.
/// </summary>
/// <param name="syncService">Sync business logic.</param>
[Authorize]
[Route("sync")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class SyncController(ISyncService syncService) : InternalControllerBase
{
    private readonly ISyncService _syncService = syncService;

    /// <summary>Gets the connection (hint only).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    [HttpGet("key")]
    [ProducesResponseType<SyncKeyDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "SyncKeyGet", OperationId = "SyncKeyGet")]
    public async Task<ActionResult<SyncKeyDTO>> SyncKeyGetAsync(string userId) => (await _syncService.KeyGetAsync(userId)).ToActionResult();

    /// <summary>Makes a new key (the old one stops working). The key is in this answer only.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The key.</returns>
    [HttpPost("key")]
    [ProducesResponseType<SyncKeyCreatedDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "SyncKeyCreate", OperationId = "SyncKeyCreate")]
    public async Task<ActionResult<SyncKeyCreatedDTO>> SyncKeyCreateAsync(string userId) => (await _syncService.KeyCreateAsync(userId)).ToActionResult();

    /// <summary>Revokes the key.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    [HttpDelete("key")]
    [ProducesResponseType<SyncKeyDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "SyncKeyRevoke", OperationId = "SyncKeyRevoke")]
    public async Task<ActionResult<SyncKeyDTO>> SyncKeyRevokeAsync(string userId) => (await _syncService.KeyRevokeAsync(userId)).ToActionResult();

    /// <summary>Imports an Apple Health export (Health Auto Export JSON v2 or the Kardia Shortcut's dictionary).</summary>
    /// <param name="body">The JSON body.</param>
    /// <returns>What was imported.</returns>
    [HttpPost("ingest")]
    [Consumes(MediaTypeNames.Application.Json)]
    [RequestSizeLimit(64 * 1024 * 1024)]
    [ProducesResponseType<SyncIngestResultDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [SwaggerOperation(Summary = "SyncIngest", OperationId = "SyncIngest")]
    public async Task<ActionResult<SyncIngestResultDTO>> SyncIngestAsync([FromBody] JsonElement body)
    {
        var key = Request.Headers.TryGetValue(ApiHeaders.SyncKey, out var header) ? header.ToString() : null;
        return (await _syncService.IngestAsync(key, body)).ToActionResult();
    }
}
