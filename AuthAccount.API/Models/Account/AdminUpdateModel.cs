using System.ComponentModel.DataAnnotations;

namespace AuthAccount.API.Models.Account;

/// <summary>
/// Represents the model for an admin to update a user's account information,
/// specifically for the user's account login or email address.
/// </summary>
/// <remarks>
/// This model validates that the provided user identifier is either a valid username 
/// or a valid email address. It is intended for use in administrative contexts.
/// </remarks>
public class AdminUpdateModel : UpdateModel
{
    /// <summary>
    /// Gets or sets the identifier for the user, which can be a login name or an email address.
    /// </summary>
    /// <remarks>
    /// This field is required and cannot be empty or null.
    /// </remarks>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Updating account login or email required.")]
    public string UserIdentifier { get; set; } = string.Empty!;

    /// <summary>
    /// Validates the model to ensure that the provided user identifier is valid.
    /// </summary>
    /// <param name="errorMessage">The error message if the model is invalid.</param>
    /// <returns>True if the model is valid; otherwise, false.</returns>
    public override bool TryValidateModel(out string errorMessage)
    {
        if (base.TryValidateModel(out _))
        {
            bool isValid = IsValidUserName(UserIdentifier) || IsValidEmail(UserIdentifier);

            errorMessage = isValid ?
                string.Empty :
                "User account login or email must be valid.";
            return isValid;
        }

        return base.TryValidateModel(out errorMessage);
    }
}
