namespace AuthAccount.API.Constants;

/// <summary>
/// Provides constant values related to authentication, including user roles and configuration keys.
/// </summary>
public static class AuthConstants
{
    public const string Admin = "admin";
    public const string User = "user";

    /// <summary>
    /// Defines the available user roles within the application.
    /// </summary>
    public enum Role
    {
        Admin,
        User
    }

    /// <summary>
    /// Gets the string representation of the Admin role.
    /// </summary>
    public static string AdminRole => Role.Admin.ToString();
    /// <summary>
    /// Gets the string representation of the User role.
    /// </summary>
    public static string UserRole => Role.User.ToString();

    public const string AdminEmailKey = "AdminEmail";
    public const string UserEmailKey = "UserEmail";
    public const string PasswordKey = "AccountsPassword";
}
