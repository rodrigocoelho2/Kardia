namespace Kardia.WebApi.Features.Push.Services;

/// <summary>One notification: what the service worker shows, and where a tap goes.</summary>
/// <param name="Title">The title.</param>
/// <param name="Body">The text.</param>
/// <param name="Url">Where a tap opens (a path of the app).</param>
/// <param name="Tag">Same tag = the newer replaces the older on the device.</param>
internal sealed record PushMessage(string Title, string Body, string Url, string Tag);
