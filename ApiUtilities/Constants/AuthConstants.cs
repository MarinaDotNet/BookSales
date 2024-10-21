using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace ApiUtilities.Constants;

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

    /// <summary>
    /// Regular expression pattern for validating email addresses.
    /// </summary>
    /// <remarks>
    /// This pattern requires that the email contains at least 6 characters before the "@" symbol,
    /// followed by a valid domain format. The domain must contain at least two characters after the last period.
    /// </remarks>
    public const string EmailPattern =
         @"^(?![_\.\%+\-])[A-Za-z0-9._%+-]+(?:'[A-Za-z0-9._%+-]+)*@[A-Za-z0-9]+([.-][A-Za-z0-9]+)*\.[A-Za-z]{2,6}$";

    /// <summary>
    /// Regular expression pattern for validating passwords.
    /// </summary>
    /// <remarks>
    /// This pattern enforces that the password must contain at least:
    /// - One lowercase letter (a-z)
    /// - One uppercase letter (A-Z)
    /// - One digit (0-9)
    /// - One special character from the set [@./\-_&!#$%*()+=]
    /// The total length of the password must be at least 8 characters.
    /// </remarks>
    public const string PasswordPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@./\-_&!#$%*()+=])[A-Za-z\d@./\-_&!#$%*()+=]{8,}$";

    /// <summary>
    /// Regular expression pattern for validating usernames.
    /// </summary>
    /// <remarks>
    /// This pattern requires that the username:
    /// - Be between 3 to 30 characters long
    /// - Can include letters, numbers, and the characters ._-@
    /// - Must not start or end with the characters ._-@
    /// </remarks>
    public const string UserNamePattern = @"^(?![_\.\-@])[a-zA-Z0-9._\-@]{3,30}(?<![_\.\-@])$";
}
