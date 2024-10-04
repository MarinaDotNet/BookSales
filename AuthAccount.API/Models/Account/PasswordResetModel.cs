using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace AuthAccount.API.Models.Account;

/// <summary>
/// Represents a model for resetting a user's password, including validation for new password and confirmation.
/// </summary>
public class PasswordResetModel : AccountModel
{
    /// <summary>
    /// Gets or sets the new password for the user.
    /// </summary>
    /// <value>
    /// The new password must be provided and cannot be empty.
    /// </value>
    [Required(AllowEmptyStrings = false, ErrorMessage = "A new password is required.")]
    [PasswordPropertyText]
    [DataType(DataType.Password)]
    [DisplayName("New Password")]
    public string NewUserPassword { get; set; } = string.Empty!;

    /// <summary>
    /// Gets or sets the confirmation of the new password.
    /// </summary>
    /// <value>
    /// The confirmation password must match the new password and cannot be empty.
    /// </value>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Please confirm your new password.")]
    [PasswordPropertyText]
    [DataType(DataType.Password)]
    [DisplayName("Confirm new Password")]
    [Compare("NewUserPassword", ErrorMessage = "The new password and its confirmation do not match.")]
    public string ConfirmNewUserPassword { get; set; } = string.Empty!;

    /// <summary>
    /// Attempts to validate the reset password model, ensuring that the new password meets the required conditions.
    /// </summary>
    /// <param name="errorMessage">If validation fails, contains the error message.</param>
    /// <returns>
    /// True if the model is valid; otherwise, false.
    /// </returns>
    public override bool TryValidateModel(out string errorMessage)
    {
        if (base.TryValidateModel(out _))
        {
            bool isValid = IsValidPassword(NewUserPassword) &&
            IsValidPassword(ConfirmNewUserPassword) &&
            NewUserPassword.Equals(ConfirmNewUserPassword) &&
            !Password.Equals(NewUserPassword);

            errorMessage = isValid ?
                string.Empty :
                !NewUserPassword.Equals(ConfirmNewUserPassword) ?
                "New password and confirmation new password do not match." :
                Password.Equals(NewUserPassword) ?
                "The new password cannot be the same as the current password." :
                "Both the new password and its confirmation are required.";

            return isValid;
        }

        return base.TryValidateModel(out errorMessage);
    }
}
