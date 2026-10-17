namespace Kardia.Library.Constants;

/// <summary>
/// HTTP headers shared by the WebApi and its clients.
/// </summary>
public static class ApiHeaders
{
    /// <summary>Authenticates the calling application (the Website).</summary>
    public const string ApiKey = "x-api-key";

    /// <summary>The personal sync key of an account, sent by Health Auto Export or the iOS Shortcut.</summary>
    public const string SyncKey = "X-Kardia-Key";

    /// <summary>The prefix of every personal sync key, so a leaked one is easy to recognise.</summary>
    public const string SyncKeyPrefix = "kardia_sk_";
}
