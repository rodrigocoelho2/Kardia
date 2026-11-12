using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.Library.Validation;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Demo.Services;
using Kardia.WebApi.Features.Profiles.Models.Entities;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Users.Models.DTOs;
using Kardia.WebApi.Features.Users.Models.Entities;
using Kardia.WebApi.Features.Users.Repositories;
using Microsoft.Extensions.Options;

namespace Kardia.WebApi.Features.Users.Services;

/// <summary>
/// The accounts: create, sign in, change password, delete. Passwords are PBKDF2 hashes.
/// </summary>
/// <param name="userRepository">Accounts.</param>
/// <param name="profileRepository">Profiles (date of birth and sex are saved with the account).</param>
/// <param name="demoSeeder">Fills new accounts with sample data in demo mode.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="clock">The app's clock.</param>
/// <param name="options">Module options.</param>
internal sealed partial class UserService(
    IUserRepository userRepository,
    IProfileRepository profileRepository,
    IDemoSeeder demoSeeder,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock,
    IOptions<KardiaOptions> options) : IUserService
{
    /// <summary>Shortest password accepted.</summary>
    internal const int MinPasswordLength = 8;

    /// <summary>The demo account's user name.</summary>
    internal const string DemoUserName = "demo";

    private const int MaxPasswordLength = 200;
    private const string FieldUserName = "UserName";
    private const string FieldPassword = "Password";
    private const string FieldCurrentPassword = "CurrentPassword";
    private const string FieldNewPassword = "NewPassword";
    private const string FieldIdentifier = "Identifier";
    private const string FieldBirthDate = "BirthDate";
    private const string FieldDiscordId = "DiscordId";

    // Spends the same time on an unknown user name as on a wrong password.
    private static readonly string DummyHash = PasswordHasher.Hash("not-a-real-password");

    private readonly IUserRepository _userRepository = userRepository;
    private readonly IProfileRepository _profileRepository = profileRepository;
    private readonly IDemoSeeder _demoSeeder = demoSeeder;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly IAppClock _clock = clock;
    private readonly KardiaOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserStatusDTO>> StatusAsync() => ServiceApiResponse<UserStatusDTO>.Success(new UserStatusDTO
    {
        UserCount = await _userRepository.CountAsync(),
        DemoMode = _options.DemoMode,
        DemoUserName = _options.DemoMode ? DemoUserName : null,
        DemoPassword = _options.DemoMode ? _options.DemoPassword : null,
    });

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<UserDTO>>> ListAsync() =>
        ServiceApiResponse<List<UserDTO>>.Success((await _userRepository.ListAsync())
            .OrderBy(static user => user.Username, StringComparer.OrdinalIgnoreCase).Select(ToDTO).ToList());

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> GetAsync(string identifier)
    {
        var user = Guid.TryParse(identifier, out var id) ? await _userRepository.GetAsync(id) : null;
        return user is null
            ? ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier ?? string.Empty))
            : ServiceApiResponse<UserDTO>.Success(ToDTO(user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> CreateAsync(UserCreateRequest item)
    {
        var userName = (item.UserName ?? string.Empty).Trim();
        var password = item.Password ?? string.Empty;

        var validationErrors = new List<ApiValidationError>();
        if (!UserNamePattern().IsMatch(userName))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldUserName, nameof(TranslationKeys.UserNameInvalid)));
        }

        validationErrors.AddRange(await ValidatePasswordAsync(FieldPassword, password));
        validationErrors.AddRange(await ValidateBirthDateAsync(item.BirthDate));
        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<UserDTO>()).AddErrorRange(validationErrors);
        }

        var user = await _userRepository.CreateAsync(userName, PasswordHasher.Hash(password), NewStamp());
        if (user is null)
        {
            return (await ValidationProblemsAsync<UserDTO>())
                .AddError(FieldUserName, (await _globalizationHelper.ValidationErrorAsync(FieldUserName, nameof(TranslationKeys.AlreadyExists), userName)).Message);
        }

        await _profileRepository.SaveAsync(user.UserUniqueID, new UserProfile
        {
            BirthDate = item.BirthDate?.ToDateTime(TimeOnly.MinValue),
            Sex = (byte)item.Sex,
        });
        await _demoSeeder.SeedAsync(user.UserUniqueID);
        return ServiceApiResponse<UserDTO>.Success(ToDTO(user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> SignInAsync(UserSignInRequest item)
    {
        var userName = (item.UserName ?? string.Empty).Trim();
        var user = userName.Length is > 0 and <= 32 ? await _userRepository.GetByNameAsync(userName) : null;

        var valid = PasswordHasher.Verify(item.Password ?? string.Empty, user?.PasswordHash ?? DummyHash) && user is not null;
        if (!valid)
        {
            var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.WrongCredentials));
            return ServiceApiResponse<UserDTO>.ValidationProblems(message).AddError(FieldPassword, message);
        }

        await _userRepository.TouchLoginAsync(user!.UserUniqueID);
        return ServiceApiResponse<UserDTO>.Success(ToDTO((await _userRepository.GetAsync(user.UserUniqueID)) ?? user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> DiscordSignInAsync(UserDiscordSignInRequest item)
    {
        var discordId = (item.DiscordId ?? string.Empty).Trim();
        var user = UserRules.IsDiscordId(discordId) ? await _userRepository.GetByDiscordAsync(discordId) : null;
        if (user is null)
        {
            return ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordNotLinked)));
        }

        await _userRepository.TouchLoginAsync(user.UserUniqueID);
        return ServiceApiResponse<UserDTO>.Success(ToDTO((await _userRepository.GetAsync(user.UserUniqueID)) ?? user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> DiscordCreateAsync(UserDiscordCreateRequest item)
    {
        var userName = (item.UserName ?? string.Empty).Trim();
        var discordId = (item.DiscordId ?? string.Empty).Trim();

        var validationErrors = new List<ApiValidationError>();
        if (!UserNamePattern().IsMatch(userName))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldUserName, nameof(TranslationKeys.UserNameInvalid)));
        }

        if (!UserRules.IsDiscordId(discordId))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldDiscordId, nameof(TranslationKeys.DiscordInvalid)));
        }

        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<UserDTO>()).AddErrorRange(validationErrors);
        }

        if (await _userRepository.GetByDiscordAsync(discordId) is not null)
        {
            return await DiscordTakenAsync();
        }

        // No password: the empty hash never verifies, so only Discord opens the account until one is set.
        var user = await _userRepository.CreateAsync(userName, string.Empty, NewStamp());
        if (user is null)
        {
            return (await ValidationProblemsAsync<UserDTO>())
                .AddError(FieldUserName, (await _globalizationHelper.ValidationErrorAsync(FieldUserName, nameof(TranslationKeys.AlreadyExists), userName)).Message);
        }

        if (!await _userRepository.SetDiscordAsync(user.UserUniqueID, discordId, UserRules.DiscordName(item.DiscordName)))
        {
            // Linked somewhere else a moment ago: don't leave an account nobody can open.
            await _userRepository.DeleteAsync(user.UserUniqueID);
            return await DiscordTakenAsync();
        }

        // Date of birth and sex are filled in later, in Settings › Profile.
        await _profileRepository.SaveAsync(user.UserUniqueID, new UserProfile());
        await _demoSeeder.SeedAsync(user.UserUniqueID);
        await _userRepository.TouchLoginAsync(user.UserUniqueID);
        return ServiceApiResponse<UserDTO>.Success(ToDTO((await _userRepository.GetAsync(user.UserUniqueID))!));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> LinkDiscordAsync(UserDiscordLinkRequest item)
    {
        var user = Guid.TryParse(item.Id, out var id) ? await _userRepository.GetAsync(id) : null;
        if (user is null)
        {
            return ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.Id ?? string.Empty));
        }

        var discordId = string.IsNullOrWhiteSpace(item.DiscordId) ? null : item.DiscordId.Trim();
        if (discordId is not null && !UserRules.IsDiscordId(discordId))
        {
            var error = await _globalizationHelper.ValidationErrorAsync(FieldDiscordId, nameof(TranslationKeys.DiscordInvalid));
            return ServiceApiResponse<UserDTO>.ValidationProblems(error.Message).AddError(FieldDiscordId, error.Message);
        }

        if (discordId is null && string.IsNullOrEmpty(user.PasswordHash))
        {
            var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordNeedsPassword));
            return ServiceApiResponse<UserDTO>.ValidationProblems(message).AddError(FieldDiscordId, message);
        }

        var name = UserRules.DiscordName(item.DiscordName);
        if (!await _userRepository.SetDiscordAsync(user.UserUniqueID, discordId, discordId is null ? null : name))
        {
            return await DiscordTakenAsync();
        }

        return ServiceApiResponse<UserDTO>.Success(ToDTO((await _userRepository.GetAsync(user.UserUniqueID))!));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> ChangePasswordAsync(UserPasswordRequest item)
    {
        var user = Guid.TryParse(item.Id, out var id) ? await _userRepository.GetAsync(id) : null;
        if (user is null)
        {
            return ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.Id ?? string.Empty));
        }

        var validationErrors = new List<ApiValidationError>();
        // An account made with Discord has no password yet: its first one needs no current password.
        if (!string.IsNullOrEmpty(user.PasswordHash) && !PasswordHasher.Verify(item.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldCurrentPassword, nameof(TranslationKeys.WrongCurrentPassword)));
        }

        validationErrors.AddRange(await ValidatePasswordAsync(FieldNewPassword, item.NewPassword ?? string.Empty));
        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<UserDTO>()).AddErrorRange(validationErrors);
        }

        await _userRepository.SetPasswordAsync(user.UserUniqueID, PasswordHasher.Hash(item.NewPassword!), NewStamp());
        return ServiceApiResponse<UserDTO>.Success(ToDTO((await _userRepository.GetAsync(user.UserUniqueID))!));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(string identifier)
    {
        var users = await _userRepository.ListAsync();
        var user = Guid.TryParse(identifier, out var id) ? users.FirstOrDefault(user => user.UserUniqueID == id) : null;
        if (user is null)
        {
            return ServiceApiResponse<string>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier ?? string.Empty));
        }

        if (user.IsAdmin && users.Count(static other => other.IsAdmin) == 1)
        {
            var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.LastAdmin));
            return ServiceApiResponse<string>.ValidationProblems(message).AddError(FieldIdentifier, message);
        }

        await _userRepository.DeleteAsync(user.UserUniqueID);
        return ServiceApiResponse<string>.Success(user.UserUniqueID.ToString("N"));
    }

    /// <summary>
    /// Maps an account to its DTO.
    /// </summary>
    /// <param name="user">The account.</param>
    /// <returns>The DTO.</returns>
    internal static UserDTO ToDTO(UserAccount user) => new()
    {
        Id = user.UserUniqueID.ToString("N"),
        UserName = user.Username,
        Role = user.IsAdmin ? UserRole.Admin : UserRole.User,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc)),
        LastSignInAt = user.LastLoginAt is { } at ? new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)) : null,
        SecurityStamp = user.SecurityStamp,
        DiscordLinked = !string.IsNullOrEmpty(user.DiscordID),
        DiscordName = string.IsNullOrEmpty(user.DiscordID) ? null : user.DiscordName,
        HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
    };

    private static string NewStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private async Task<List<ApiValidationError>> ValidatePasswordAsync(string field, string password)
    {
        var errors = new List<ApiValidationError>();
        if (password.Length < MinPasswordLength)
        {
            errors.Add(await _globalizationHelper.ValidationErrorAsync(field, nameof(TranslationKeys.PasswordTooShort), MinPasswordLength));
        }
        else if (password.Length > MaxPasswordLength)
        {
            errors.Add(await _globalizationHelper.ValidationErrorAsync(field, nameof(TranslationKeys.PasswordTooLong)));
        }

        return errors;
    }

    private async Task<List<ApiValidationError>> ValidateBirthDateAsync(DateOnly? birthDate)
    {
        var errors = new List<ApiValidationError>();
        if (birthDate is not { } date)
        {
            return errors;
        }

        var today = _clock.Today;
        if (date > today)
        {
            errors.Add(await _globalizationHelper.ValidationErrorAsync(FieldBirthDate, nameof(TranslationKeys.FutureDate)));
        }
        else if (date < today.AddYears(-110) || date > today.AddYears(-13))
        {
            errors.Add(await _globalizationHelper.ValidationErrorAsync(FieldBirthDate, nameof(TranslationKeys.OutOfRange), today.AddYears(-110).Year, today.AddYears(-13).Year));
        }

        return errors;
    }

    private async Task<ServiceApiResponse<UserDTO>> DiscordTakenAsync()
    {
        var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordTaken));
        return ServiceApiResponse<UserDTO>.ValidationProblems(message).AddError(FieldDiscordId, message);
    }

    private async Task<ServiceApiResponse<T>> ValidationProblemsAsync<T>() =>
        ServiceApiResponse<T>.ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)));

    [GeneratedRegex("^[A-Za-z0-9._-]{3,32}$")]
    private static partial Regex UserNamePattern();
}
