using System.Net;
using System.Net.Mime;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Services;
using Kardia.WebApi.Features.Users.Models.DTOs;
using Kardia.WebApi.Features.Users.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kardia.WebApi.Features.Users.Controllers;

/// <summary>
/// The accounts. Only the Website calls this (x-api-key); passwords never appear in logs.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="userService">Account business logic.</param>
[Authorize]
[Route("user")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class UserController(
    ILogger<UserController> logger,
    IUserService userService) : InternalControllerBase
{
    private readonly ILogger<UserController> _logger = logger;
    private readonly IUserService _userService = userService;

    /// <summary>How many accounts exist, and the demo account in demo mode.</summary>
    /// <returns>The status.</returns>
    [HttpGet("status")]
    [ProducesResponseType<UserStatusDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "UserStatus", OperationId = "UserStatus")]
    public async Task<ActionResult<UserStatusDTO>> UserStatusAsync() => (await _userService.StatusAsync()).ToActionResult();

    /// <summary>Lists every account.</summary>
    /// <returns>The accounts.</returns>
    [HttpGet("list")]
    [ProducesResponseType<List<UserDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "UserList", OperationId = "UserList")]
    public async Task<ActionResult<List<UserDTO>>> UserListAsync() => (await _userService.ListAsync()).ToActionResult();

    /// <summary>Gets one account.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>The account.</returns>
    [HttpGet]
    [ProducesResponseType<UserDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "UserGet", OperationId = "UserGet")]
    public async Task<ActionResult<UserDTO>> UserGetAsync(string identifier) => (await _userService.GetAsync(identifier)).ToActionResult();

    /// <summary>Creates an account (the first one is an admin).</summary>
    /// <param name="item">User name, password, date of birth and sex.</param>
    /// <returns>The account.</returns>
    [HttpPost]
    [ProducesResponseType<UserDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "UserCreate", OperationId = "UserCreate")]
    public async Task<ActionResult<UserDTO>> UserCreateAsync(UserCreateRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("UserCreate [UserName={UserName}]", item.UserName);

        return (await _userService.CreateAsync(item)).ToActionResult();
    }

    /// <summary>Checks a user name and password.</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account.</returns>
    [HttpPost("signin")]
    [ProducesResponseType<UserDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "UserSignIn", OperationId = "UserSignIn")]
    public async Task<ActionResult<UserDTO>> UserSignInAsync(UserSignInRequest item) => (await _userService.SignInAsync(item)).ToActionResult();

    /// <summary>Finds the account a Discord account (already checked by the Website) is linked to.</summary>
    /// <param name="item">The Discord account's id.</param>
    /// <returns>The account.</returns>
    [HttpPost("discord/signin")]
    [ProducesResponseType<UserDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "UserDiscordSignIn", OperationId = "UserDiscordSignIn")]
    public async Task<ActionResult<UserDTO>> UserDiscordSignInAsync(UserDiscordSignInRequest item) => (await _userService.DiscordSignInAsync(item)).ToActionResult();

    /// <summary>Creates an account that signs in with a Discord account (already checked by the Website), without a password.</summary>
    /// <param name="item">The user name and the Discord account.</param>
    /// <returns>The account.</returns>
    [HttpPost("discord/create")]
    [ProducesResponseType<UserDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "UserDiscordCreate", OperationId = "UserDiscordCreate")]
    public async Task<ActionResult<UserDTO>> UserDiscordCreateAsync(UserDiscordCreateRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("UserDiscordCreate [UserName={UserName}]", item.UserName);

        return (await _userService.DiscordCreateAsync(item)).ToActionResult();
    }

    /// <summary>Links a Discord account to an account, or unlinks it.</summary>
    /// <param name="item">The account and the Discord account.</param>
    /// <returns>The account.</returns>
    [HttpPost("discord")]
    [ProducesResponseType<UserDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "UserDiscordLink", OperationId = "UserDiscordLink")]
    public async Task<ActionResult<UserDTO>> UserDiscordLinkAsync(UserDiscordLinkRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("UserDiscordLink [Id={Id}, Linked={Linked}]", item.Id, !string.IsNullOrWhiteSpace(item.DiscordId));

        return (await _userService.LinkDiscordAsync(item)).ToActionResult();
    }

    /// <summary>Changes a password.</summary>
    /// <param name="item">The account, current and new password.</param>
    /// <returns>The account.</returns>
    [HttpPost("password")]
    [ProducesResponseType<UserDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "UserPassword", OperationId = "UserPassword")]
    public async Task<ActionResult<UserDTO>> UserPasswordAsync(UserPasswordRequest item) => (await _userService.ChangePasswordAsync(item)).ToActionResult();

    /// <summary>Deletes an account and its data (never the last admin).</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>The deleted id.</returns>
    [HttpDelete]
    [ProducesResponseType<string>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "UserDelete", OperationId = "UserDelete")]
    public async Task<ActionResult<string>> UserDeleteAsync(string identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("UserDelete [Identifier={Identifier}]", identifier);

        return (await _userService.DeleteAsync(identifier)).ToActionResult();
    }
}
