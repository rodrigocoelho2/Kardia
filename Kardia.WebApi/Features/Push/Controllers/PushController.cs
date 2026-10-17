using System.Net;
using System.Net.Mime;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.Push.Models.DTOs;
using Kardia.WebApi.Features.Push.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.Push.Controllers;

/// <summary>Push notifications: an account's devices, turning a browser on and off, and a test.</summary>
/// <param name="pushService">Push business logic.</param>
[Authorize]
[Route("push")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class PushController(IPushService pushService) : InternalControllerBase
{
    /// <summary>Whether notifications are on, the key to subscribe with and the account's devices.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    [HttpGet]
    [ProducesResponseType<PushDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PushGet", OperationId = "PushGet")]
    public async Task<ActionResult<PushDTO>> PushGetAsync(string userId) => (await pushService.GetAsync(userId)).ToActionResult();

    /// <summary>Turns notifications on for a browser.</summary>
    /// <param name="request">The browser's subscription.</param>
    /// <returns>The state.</returns>
    [HttpPost("subscribe")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<PushDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PushSubscribe", OperationId = "PushSubscribe")]
    public async Task<ActionResult<PushDTO>> PushSubscribeAsync([FromBody] PushSubscribeRequest request) => (await pushService.SubscribeAsync(request)).ToActionResult();

    /// <summary>Turns notifications off for a device.</summary>
    /// <param name="request">The device (id or endpoint).</param>
    /// <returns>The state.</returns>
    [HttpPost("unsubscribe")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<PushDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PushUnsubscribe", OperationId = "PushUnsubscribe")]
    public async Task<ActionResult<PushDTO>> PushUnsubscribeAsync([FromBody] PushUnsubscribeRequest request) => (await pushService.UnsubscribeAsync(request)).ToActionResult();

    /// <summary>Sends a test notification to the account's devices.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>How many devices it reached.</returns>
    [HttpPost("test")]
    [ProducesResponseType<PushTestResultDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PushTest", OperationId = "PushTest")]
    public async Task<ActionResult<PushTestResultDTO>> PushTestAsync(string userId) => (await pushService.TestAsync(userId)).ToActionResult();
}
