using System.Globalization;
using Kardia.Library.Enums;
using Kardia.Library.Validation;

namespace Kardia.WebApi.Extensions.Globalization;

/// <summary>
/// Translates message keys and field identifiers into user-facing text.
/// </summary>
internal interface IGlobalizationHelper
{
    /// <summary>
    /// Translates a key.
    /// </summary>
    /// <param name="key">A <see cref="TranslationKeys"/> name or a field identifier.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The translated text, or the key itself when there is no translation.</returns>
    Task<string> TranslateAsync(string key, params object[] arguments);

    /// <summary>
    /// Builds a validation error "{field label} {message}".
    /// </summary>
    /// <param name="field">The field identifier.</param>
    /// <param name="messageKey">The message key.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The validation error.</returns>
    Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments);
}

/// <summary>
/// The user-facing messages (English). The app has one language, so the texts live in code rather than in a
/// translation table.
/// </summary>
internal sealed class GlobalizationHelper : IGlobalizationHelper
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");

    private static readonly Dictionary<string, string> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(TranslationKeys.ValidationProblems)] = "Some of the data sent is not valid.",
        [nameof(TranslationKeys.RequiredField)] = "is required.",
        [nameof(TranslationKeys.AlreadyExists)] = "'{0}' is already taken.",
        [nameof(TranslationKeys.NoRecordsFound)] = "'{0}' does not exist.",
        [nameof(TranslationKeys.UserNameInvalid)] = "must be 3–32 letters, numbers, dots, dashes or underscores.",
        [nameof(TranslationKeys.PasswordTooShort)] = "must have at least {0} characters.",
        [nameof(TranslationKeys.PasswordTooLong)] = "is too long.",
        [nameof(TranslationKeys.WrongCredentials)] = "Wrong user name or password.",
        [nameof(TranslationKeys.WrongCurrentPassword)] = "is wrong.",
        [nameof(TranslationKeys.RegistrationClosed)] = "Creating accounts is turned off. Ask whoever runs Kardia to add you.",
        [nameof(TranslationKeys.LastAdmin)] = "The last admin can't be deleted.",
        [nameof(TranslationKeys.OutOfRange)] = "must be between {0} and {1}.",
        [nameof(TranslationKeys.FutureDate)] = "can't be in the future.",
        [nameof(TranslationKeys.UnknownTag)] = "'{0}' isn't a journal entry Kardia knows.",
        [nameof(TranslationKeys.SyncKeyInvalid)] = "Missing or unknown Kardia key. Copy it again from Settings › Apple Watch.",
        [nameof(TranslationKeys.SyncPayloadInvalid)] = "Kardia couldn't read this. Send Health Auto Export JSON (REST API, version 2) or the Kardia Shortcut's dictionary.",
        [nameof(TranslationKeys.PushDisabled)] = "Notifications are turned off on this server (Kardia__PushEnabled).",
        [nameof(TranslationKeys.PushEndpointNotAllowed)] = "isn't a browser push service.",
        [nameof(TranslationKeys.PushKeysInvalid)] = "sent keys Kardia can't use. Turn notifications off and on again.",
        [nameof(TranslationKeys.PushNoDevices)] = "None of your devices has notifications on yet.",
        [nameof(TranslationKeys.SyncNothingToImport)] = "Nothing new in the last sync (if this keeps happening with data on the phone, check the metrics picked in Health Auto Export).",
        [nameof(TranslationKeys.DiscordNotLinked)] = "No account here is linked to that Discord account. Sign in with your password and link it in Settings.",
        [nameof(TranslationKeys.DiscordTaken)] = "That Discord account is already linked to another account.",
        [nameof(TranslationKeys.DiscordInvalid)] = "isn't a Discord account.",
        [nameof(TranslationKeys.DiscordNeedsPassword)] = "Discord is the only way into this account. Set a password before unlinking it.",

        ["UserName"] = "User name",
        ["Password"] = "Password",
        ["CurrentPassword"] = "Current password",
        ["NewPassword"] = "New password",
        ["Identifier"] = "Account",
        ["DiscordId"] = "Discord account",
        ["BirthDate"] = "Date of birth",
        ["HeightCm"] = "Height",
        ["WeightKg"] = "Weight",
        ["MaxHeartRate"] = "Maximum heart rate",
        ["SleepNeedMinutes"] = "Sleep need",
        ["Date"] = "Date",
        ["Tag"] = "Journal entry",
        ["Key"] = "Kardia key",
        ["Body"] = "Export",
        ["SleepGoalPercent"] = "Sleep goal",
        ["ActiveMinutesWeek"] = "Minutes in zone 2+ a week",
        ["StepsDay"] = "Daily steps",
        ["SleepNightsWeek"] = "Nights on the sleep goal",
        ["BedtimeNightsWeek"] = "Nights on time",
        ["BedtimeLeadMinutes"] = "Reminder before bedtime",
        ["Endpoint"] = "This browser",
        ["Devices"] = "Notifications",
        ["Period"] = "Period",
    };

    /// <inheritdoc />
    public Task<string> TranslateAsync(string key, params object[] arguments)
    {
        var text = Translations.TryGetValue(key, out var value) ? value : key;
        return Task.FromResult(arguments.Length == 0 ? text : string.Format(English, text, arguments));
    }

    /// <inheritdoc />
    public async Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments)
    {
        var label = await TranslateAsync(field);
        var message = await TranslateAsync(messageKey, arguments);
        return new ApiValidationError(field, $"{label} {message}");
    }
}
