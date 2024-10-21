using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.RegularExpressions;
using ApiUtilities.Constants;

namespace AuthAccount.API.Models;

/// <summary>
/// Represents a base model for account-related functionality, including 
/// login with either a username or an email and password.
/// This class is intended to be inherited by specific account models.
/// </summary>
public abstract partial class AccountModel
{
    /// <summary>
    /// Gets or sets the username or email for login purposes.
    /// </summary>
    /// <remarks>
    /// This property is required and cannot be an empty string.
    /// </remarks>
    [Required(AllowEmptyStrings = false, ErrorMessage = "The Username or email is required.")]
    [DisplayName("Username or email")]
    public string UsernameOrEmail { get; set; } = string.Empty!;

    /// <summary>
    /// Gets or sets the password for the account.
    /// </summary>
    /// <remarks>
    /// This property is required and cannot be an empty string.
    /// It is displayed as a password field in the user interface.
    /// </remarks>
    [Required(AllowEmptyStrings = false, ErrorMessage = "The password is required.")]
    [PasswordPropertyText]
    [DataType(DataType.Password)]
    [DisplayName("Password")]
    public string Password { get; set; } = string.Empty!;

    /// /// <summary>
    /// Generates a regular expression to validate an email addresses.
    /// The pattern ensures that the email:
    /// - Does not start or end with special characters like '_', '.', '-', or '@'.
    /// - Consists of alphanumeric characters (a-z, A-Z, 0-9), periods (.), underscores (_), hyphens (-), 
    /// and at symbols (@).
    /// - Local part is at least 6 characters long.
    /// - Includes a mandatory <c>@</c> symbol.
    /// - Domain name can have letters, digits, dots, and hyphens.
    /// - Top-level domain must have at least 2 characters.
    /// </summary>
    /// <returns>A <see cref="Regex"/> object based on the pattern: 
    /// "^[A-Za-z0-9._%+-]{6,}@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$".</returns>
    [GeneratedRegex(AuthConstants.EmailPattern)]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Validates whether the provided string is a valid email address.
    /// </summary>
    /// <param name="value">The email address to validate.</param>
    /// <returns>True if the email address is valid; otherwise, false.</returns>
    /// <remarks>
    /// This method checks if the input <paramref name="value"/> is not null or whitespace 
    /// and then uses a regular expression to verify its format. 
    /// </remarks>
    public bool IsValidEmail(string value) 
    { 
        if(string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        return  EmailRegex().IsMatch(value); 
    }

    /// <summary>
    /// Generates a regular expression to validate a username or login string.
    /// The pattern ensures that the username:
    /// - Does not start or end with special characters like '_', '.', '-', or '@'.
    /// - Consists of alphanumeric characters (a-z, A-Z, 0-9), periods (.), underscores (_), hyphens (-), 
    /// and at symbols (@).
    /// - Has a length between 3 and 30 characters.
    /// </summary>
    /// <returns>A Regex object based on the pattern: "^(?![_\.\-@])[a-zA-Z0-9._\-@]{3,30}(?<![_\.\-@])$".</returns>
    [GeneratedRegex(AuthConstants.UserNamePattern)]
    private static partial Regex UserNamePattern();

    /// <summary>
    /// Validates whether the provided string is a valid username or login.
    /// </summary>
    /// <param name="value">The username or login string to validate.</param>
    /// <returns>
    /// True if the username matches the specified pattern; otherwise, false.
    /// </returns>
    public bool IsValidUserName(string value) 
    { 
        if(string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        return UserNamePattern().IsMatch(value);
    }

    /// <summary>
    /// Generates a regular expression to validate password strength based on the defined pattern.
    /// The pattern requires the password to:
    /// - Contain at least one lowercase letter (a-z)
    /// - Contain at least one uppercase letter (A-Z)
    /// - Contain at least one digit (0-9)
    /// - Contain at least one special symbol from [@ . / - _ & ! # $ % * ( ) + =]
    /// - Have a minimum length of 8 characters.
    /// </summary>
    /// <returns>A compiled regular expression for password validation.</returns>
    [GeneratedRegex(AuthConstants.PasswordPattern)]
    private static partial Regex PasswordRegex();

    /// <summary>
    /// Validates whether the provided string is a valid password.
    /// The password must match specific security criteria defined by the regular expression,
    /// or it can be a default password (e.g., "P@ssw0rd") for special cases.
    /// </summary>
    /// <param name="value">The password string to validate.</param>
    /// <returns>
    /// True if the password is either the default "P@ssw0rd" or if it matches the required password criteria;
    /// otherwise, false.
    /// </returns>
    public bool IsValidPassword(string value)
    {
        if(string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        return PasswordRegex().IsMatch(value);
    }

    /// <summary>
    /// Attempts to validate the account model by ensuring both the username (or email) 
    /// and password are in valid formats.
    /// </summary>
    /// <param name="errorMessage">
    /// If validation fails, contains the error message describing why the model is invalid.
    /// </param>
    /// <returns>
    /// True if the model is valid; otherwise, false.
    /// </returns>
    public virtual bool TryValidateModel(out string errorMessage)
    {
        if (!IsValidEmail(UsernameOrEmail) && !IsValidUserName(UsernameOrEmail))
        {
            errorMessage = "Login must be a valid email or username.";
            return false;
        }

        if(!IsValidPassword(Password))
        {
            errorMessage = "Entered password is not valid.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
