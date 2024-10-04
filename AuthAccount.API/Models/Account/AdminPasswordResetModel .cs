using System.ComponentModel.DataAnnotations;

namespace AuthAccount.API.Models.Account;

/// <summary>
/// Represents a model for resetting a user's password with an associated user account login or email.
/// </summary>
public class AdminPasswordResetModel : PasswordResetModel
{
    /// <summary>
    /// Gets or sets the user account login or email that is being updated.
    /// </summary>
    /// <value>
    /// The user account login or email must be provided and cannot be empty.
    /// </value>
    [Required(AllowEmptyStrings = false, ErrorMessage = "An account login or email is required")]
    public string UserIdentifier { get; set; } = string.Empty!;

    /// <summary>
    /// Attempts to validate the reset password model for a specific user account.
    /// </summary>
    /// <param name="errorMessage">If validation fails, contains the error message.</param>
    /// <returns>
    /// True if the model is valid; otherwise, false.
    /// </returns>
    public override bool TryValidateModel(out string errorMessage)
    {
        if (base.TryValidateModel(out _))
        {
            bool isValid = IsValidUserName(UserIdentifier) || IsValidEmail(UserIdentifier);

            errorMessage = isValid ?
                string.Empty :
                "Updating account login or email is required.";

            return isValid;
        }

        return base.TryValidateModel(out errorMessage);
    }
}
