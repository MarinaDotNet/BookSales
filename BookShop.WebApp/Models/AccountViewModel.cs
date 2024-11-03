namespace BookShop.WebApp.Models;

/// <summary>
/// Represents the view model for an account, holding user information 
/// and registration-related details.
/// </summary>
public class AccountViewModel
{
    /// <summary>
    /// Gets or sets the name of the user.
    /// </summary>
    /// <value>
    /// The name of the user.
    /// </value>
    public string UserName { get; set; } = string.Empty!;

    /// <summary>
    /// Gets or sets the email.
    /// </summary>
    /// <value>
    /// The email.
    /// </value>
    public string Email { get; set; } = string.Empty!;

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    /// <value>
    /// The password.
    /// </value>
    public string Password { get; set; } = string.Empty!;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is email confirmed.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is email confirmed; otherwise, <c>false</c>.
    /// </value>
    public bool IsEmailConfirmed { get; set; }

    /// <summary>
    /// Gets or sets the registration message.
    /// </summary>
    /// <value>
    /// The registration message.
    /// </value>
    public string? RegistrationMessage { get; set; } 
}
