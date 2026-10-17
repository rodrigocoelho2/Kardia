namespace Kardia.Website.Model.Dashboard;

/// <summary>
/// Model of the public page that explains Kardia before signing in.
/// </summary>
public sealed class LandingModel
{
    /// <summary>Gets a value indicating whether the visitor is already signed in (the button opens the app instead of the sign-in).</summary>
    public bool SignedIn { get; init; }

    /// <summary>Gets a value indicating whether anyone may create an account: shows Create account.</summary>
    public bool CanCreateAccount { get; init; }

    /// <summary>Gets the version shown in the footer.</summary>
    public string Version { get; init; } = AppPageModel.CurrentVersion;
}
