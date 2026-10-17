namespace Kardia.Library.Enums;

/// <summary>
/// What an account may do.
/// </summary>
public enum UserRole
{
    /// <summary>Sees and syncs only its own data.</summary>
    User,

    /// <summary>The first account: can also add and remove accounts.</summary>
    Admin,
}
