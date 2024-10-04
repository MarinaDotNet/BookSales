using System.ComponentModel.DataAnnotations;
using System.Runtime;

namespace AuthAccount.API.Models.Account;

/// <summary>
/// Represents the model for updating an account's login and email information.
/// </summary>
/// <remarks>
/// This model allows the user to specify a new login and/or a new email address.
/// It validates that at least one of the new fields is provided and that the new email
/// matches its confirmation.
/// </remarks>
public class UpdateModel : AccountModel
{
    /// <summary>
    /// Gets or sets the new login for the account. This field is optional.
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    public string? UpdatedLogin { get; set; }

    /// <summary>
    /// Gets or sets the new email address for the account. This field is optional.
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    public string? UpdatedEmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the confirmation for the new email address. This field is optional.
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [Compare("UpdatedEmailAddress", ErrorMessage = "The updated email and its confirmation do not match.")]
    public string? ConfirmUpdatedEmailAddress { get; set; }

    /// <summary>
    /// Validates the model to ensure that at least one of the updated login or updated email fields is provided,
    /// and that the updated email matches its confirmation. Additionally, it checks that the new values
    /// do not match the existing login or email.
    /// </summary>
    /// <param name="errorMessage">The error message if the model is invalid.</param>
    /// <returns>True if the model is valid; otherwise, false.</returns>
    public override bool TryValidateModel(out string errorMessage)
    {
        if(base.TryValidateModel(out _))
        {
            if (!IsValidUserName(UpdatedLogin!) &&
                !IsValidEmail(UpdatedEmailAddress!))
            {
                errorMessage = "At least one of the updated login or email is required.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(UpdatedEmailAddress) && 
                !UpdatedEmailAddress!.Equals(ConfirmUpdatedEmailAddress))
            {
                errorMessage = string.IsNullOrWhiteSpace(ConfirmUpdatedEmailAddress) ?
                    "The confirmation email is required." :
                    "The updated email and confirmation email do not match.";
                return false;
            }

            if (UpdatedEmailAddress!.Equals(UsernameOrEmail))
            {
                errorMessage = "The updated email should not match the current email.";
                return false;
            }

            if (UsernameOrEmail.Equals(UpdatedLogin))
            {
                errorMessage = "The updated login should not match the current login.";
                return false;
            }
        }

        return base.TryValidateModel(out errorMessage);
    }
}
