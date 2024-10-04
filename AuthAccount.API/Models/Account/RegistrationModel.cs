using Microsoft.OpenApi.MicrosoftExtensions;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AuthAccount.API.Models.Account;

/// <summary>
/// Represents a model for user account registration, including validation for email and password confirmation.
/// </summary>
public class RegistrationModel : AccountModel
{
    /// <summary>
    /// Gets or sets the email address for registration.
    /// </summary>
    /// <value>
    /// The email address must be unique and is required for registration.
    /// </value>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Unique Email is required.")]
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [DisplayName("Email Address")]
    public string EmailAddress { get; set; } = string.Empty!;

    /// <summary>
    /// Gets or sets the confirmation email address for registration.
    /// </summary>
    /// <value>
    /// The confirmation email address must match the email address and is required.
    /// </value>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Confirm Email is required.")]
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [DisplayName("Confirm Email Address")]
    [Compare("EmailAddress", ErrorMessage = "Email and Confirmation Email do not match.")]
    public string ConfirmEmailAddress { get; set; } = string.Empty!;

    /// <summary>
    /// Gets or sets the confirmation password for registration.
    /// </summary>
    /// <value>
    /// The confirmation password must match the original password and is required.
    /// </value>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Confirm Password is required.")]
    [PasswordPropertyText]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    [Compare("Password", ErrorMessage = "Password and Confirmation Password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty!;

    /// <summary>
    /// Attempts to validate the registration model, ensuring that email and password confirmations match the original values.
    /// </summary>
    /// <param name="errorMessage">If validation fails, contains the error message.</param>
    /// <returns>
    /// True if the model is valid; otherwise, false.
    /// </returns>
    public override bool TryValidateModel(out string errorMessage)
    {
        if(base.TryValidateModel(out _))
        {
            bool isValid = IsValidEmail(EmailAddress) &&
                EmailAddress.Equals(ConfirmEmailAddress) &&
                Password.Equals(ConfirmPassword);

            errorMessage = isValid ?
                string.Empty :
                string.IsNullOrWhiteSpace(EmailAddress) || !IsValidEmail(EmailAddress) ?
                "Email Address is required" :
                !EmailAddress.Equals(ConfirmEmailAddress) ?
                "The confirmation email doesnot match." :
                "The confirmation password doesnot match.";

            return isValid;
        }

        return base.TryValidateModel(out errorMessage);
    }
}
