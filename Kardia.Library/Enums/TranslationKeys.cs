namespace Kardia.Library.Enums;

/// <summary>
/// Keys of the user-facing messages. Use with <c>nameof(TranslationKeys.X)</c>, never a magic string.
/// </summary>
public enum TranslationKeys
{
    /// <summary>Generic title of a validation failure.</summary>
    ValidationProblems,

    /// <summary>A required field is empty.</summary>
    RequiredField,

    /// <summary>Another record already uses the value.</summary>
    AlreadyExists,

    /// <summary>The record does not exist.</summary>
    NoRecordsFound,

    /// <summary>A user name with characters or a length that is not allowed.</summary>
    UserNameInvalid,

    /// <summary>A password shorter than the minimum.</summary>
    PasswordTooShort,

    /// <summary>A password longer than the maximum.</summary>
    PasswordTooLong,

    /// <summary>Wrong user name or password (never says which).</summary>
    WrongCredentials,

    /// <summary>The current password is wrong.</summary>
    WrongCurrentPassword,

    /// <summary>Creating accounts is turned off.</summary>
    RegistrationClosed,

    /// <summary>The last admin cannot be deleted.</summary>
    LastAdmin,

    /// <summary>A value outside its allowed range.</summary>
    OutOfRange,

    /// <summary>A date in the future.</summary>
    FutureDate,

    /// <summary>An unknown journal tag.</summary>
    UnknownTag,

    /// <summary>No sync key, or an unknown one.</summary>
    SyncKeyInvalid,

    /// <summary>The sync payload is not something Kardia can read.</summary>
    SyncPayloadInvalid,

    /// <summary>The sync payload had nothing Kardia keeps.</summary>
    SyncNothingToImport,
}
