using AuthAccount.API.Constants;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AuthAccount.API.Models.Account;

/// <summary>
/// Represents a model used for confirming the deletion of an account.
/// </summary>
/// <remarks>
/// This class inherits from <see cref="AccountModel"/> and includes a property 
/// to confirm whether the user is sure about the deletion process.
/// </remarks>
public partial class DeletionModel : AccountModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the user has confirmed 
    /// the deletion of the account.
    /// </summary>
    /// <value>
    /// A boolean value indicating the confirmation status. 
    /// The default is <c>false</c>.
    /// </value>
    /// <exception cref="ValidationException">
    /// Thrown when the confirmation of deletion process is required but not provided.
    /// </exception>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Confiramtion of deletion process is required.")]
    [DisplayName("Are you sure?")]
    public bool IsConfirmed { get; set; }
}
