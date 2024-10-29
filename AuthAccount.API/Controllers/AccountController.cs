using ApiUtilities.Constants;
using ApiUtilities.Models;
using Asp.Versioning;
using AuthAccount.API.Models;
using AuthAccount.API.Models.Account;
using AuthAccount.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Security.Claims;
using System.Text;

namespace AuthAccount.API.Controllers;

/// <summary>
/// Controller responsible for handling account-related operations such as user/admin registration, login, 
/// password reset, account updates, and deletion.
/// </summary>
/// <remarks>
/// This controller provides endpoints for user account management tasks, such as creating new accounts, 
/// updating account information, resetting passwords, and deleting user accounts.
/// It interacts with the UserManager for identity-related operations and leverages logging to track activities 
/// and errors. Configuration settings are used for token generation and other authentication-related functionality.
/// </remarks>
/// <param name="userManager">Handles user identity operations like user creation, deletion, and validation.</param>
/// <param name="configuration">Provides access to configuration settings (e.g., JWT token settings).</param>
/// <param name="logger">Logs activity and errors related to account operations.</param>
/// <param name="emailSender">Performs the email sending operations.</param>
[ApiController]
[ApiVersion("1.0")]
[Route("account/")]
public class AccountV1Controller(UserManager<ApiUser> userManager, IConfiguration configuration, ILogger<AccountV1Controller> logger, IEmailSender emailSender) : ControllerBase
{
    private readonly UserManager<ApiUser> _userManager = userManager ??
        throw new ArgumentNullException(nameof(userManager), "UserManager cannot be null.");
    private readonly IConfiguration _configuration = configuration ??
        throw new ArgumentNullException(nameof(configuration), "Configuration cannot be null.");
    private readonly ILogger<AccountV1Controller> _logger = logger ??
        throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");
    private readonly IEmailSender _emailSender = emailSender ??
        throw new ArgumentNullException(nameof(emailSender), "EmailSender cannot be null.");

    /// <summary>
    /// Registers a new administrator account asynchronously and sends an email confirmation link to the provided email address. 
    /// If the provided account email address is one of the default API emails <see cref="AuthConstants.AdminEmailKey"/> or 
    /// <see cref="AuthConstants.UserEmailKey"/>, then the confirmation link returns a message in <see cref="OkObjectResult"/>.
    /// </summary>
    /// <param name="model">The registration model <see cref="RegistrationModel"/> containing the administrator's details.</param>
    /// <returns>An <see cref="ActionResult"/> representing the outcome of the registration process.</returns>
    /// <remarks>
    /// This method validates the input model, checks for existing accounts with the same email 
    /// or username, creates a new user and assigns the admin role. It sends the email confirmation link to the provided email. 
    /// If  the provided  email one of API default emails <see cref="AuthConstants.AdminEmailKey"/> or 
    /// <see cref="AuthConstants.UserEmailKey"/> then the confirmation link returns message in <see cref="OkObjectResult"/>.
    /// In case of failures, it logs the errors and returns appropriate responses.
    /// </remarks>
    /// <response code="200">
    /// Returns a success message upon successful administrator account registration. It may contain the confirmation link for
    /// provided account email if it is one of the default API emails <see cref="AuthConstants.AdminEmailKey"/> or 
    /// <see cref="AuthConstants.UserEmailKey"/>. 
    /// </response>
    /// <response code="400">
    /// Invalid data provided for administrator account registration. Ensure all required fields are completed correctly.
    /// </response>
    /// <response code="500">
    /// An unexpected error occurred while processing the administrator account registration request.
    /// </response>
    [HttpPost]
    [Route("/admin/new")]
    public async Task<IActionResult> RegisterAdminAsync(RegistrationModel model)
    {
        try
        {
            //Checks the registration model required parameters and null checks
            var validationResult = ValidateRegistrationModelAsync(model);
            if (validationResult is not null)
            {
                return validationResult;
            }

            ApiUser user = new()
            {
                Id = Guid.NewGuid().ToString(),
                UserName = model.UsernameOrEmail,
                Email = model.EmailAddress,
                NormalizedUserName = model.UsernameOrEmail.ToUpperInvariant(),
                NormalizedEmail = model.EmailAddress.ToUpperInvariant(),
                PasswordHash = new PasswordHasher<ApiUser>().HashPassword(new ApiUser(), model.Password),
                SecurityStamp = Guid.NewGuid().ToString()
            };
            //Creates a new account
            var registrationResult = await _userManager.CreateAsync(user);

            if (!registrationResult.Succeeded)
            {
                _logger.LogError("The 'Admin' account registration failed: {Errors}",
                    string.Join(", ", registrationResult.Errors.Select(error => error.Description)));
                return BadRequest("The account registration failed.");
            }

            //Assigning registered user account to the 'Admin' role
            var assigningRoleResult = await _userManager.AddToRoleAsync(user, AuthConstants.AdminRole);
            if (!assigningRoleResult.Succeeded)
            {
                _logger.LogError("Failed to assign 'Admin' role to the registered user: {Errors}",
                    string.Join(", ", assigningRoleResult.Errors.Select(error => error.Description)));
                return BadRequest("Failed to assign 'Admin' role to the registered user.");
            }

            _logger.LogInformation("'Admin' account successfully registered.");

            //Sends the confirmation email link to the account 'Email'.
            //Exception for the default emails: email will not be sent, and the link will be returned to OkObjectResult as a text message.
            var emailSendResult = await SendConfirmationLinkAsync(user);

            return emailSendResult ??
                Ok("Account registration successful. Please check your email for confirmation link.");
        }
        catch (Exception ex)
        {
            return Exception(ex);
        }
    }

    /// <summary>
    /// Registers a new user account asynchronously and sends an email confirmation link to the provided email address. 
    /// If the provided account email address is one of the default API emails <see cref="AuthConstants.AdminEmailKey"/> or 
    /// <see cref="AuthConstants.UserEmailKey"/>, then the confirmation link returns a message in <see cref="OkObjectResult"/>.
    /// </summary>
    /// <param name="model">The registration model containing the user's details.</param>
    /// <returns>An <see cref="ActionResult"/> representing the outcome of the registration process.</returns>
    /// <remarks>
    /// This method validates the input model, checks for existing accounts with the same email 
    /// or username, creates a new user and assigns the user role. It sends the email confirmation link to the provided email. If 
    /// the provided
    /// email one of API default emails <see cref="AuthConstants.AdminEmailKey"/> or <see cref="AuthConstants.UserEmailKey"/> then
    /// the confirmation link returns message in <see cref="OkObjectResult"/>.
    /// In case of failures, it logs the errors and returns appropriate responses.
    /// </remarks>
    /// <response code="200">
    /// Returns a success message upon successful user account registration. It may contain the confirmation link for
    /// provided account email if it is one of the default API emails <see cref="AuthConstants.AdminEmailKey"/> or 
    /// <see cref="AuthConstants.UserEmailKey"/>. </response>
    /// <response code="400">
    /// Invalid data provided for user account registration. Ensure all required fields are filled correctly.
    /// </response>
    /// <response code="500">
    /// An unexpected error occurred while processing the registration request.
    /// </response>
    [HttpPost]
    [Route("/new")]
    public async Task<ActionResult> RegisterUserAsync(RegistrationModel model)
    {
        try
        {
            //Checks the registration model's required parameters and null checks.
            var validationResult = ValidateRegistrationModelAsync(model);
            if (validationResult is not null)
            {
                return validationResult;
            }

            ApiUser user = new()
            {
                Id = Guid.NewGuid().ToString(),
                UserName = model.UsernameOrEmail,
                Email = model.EmailAddress,
                NormalizedUserName = model.UsernameOrEmail.ToUpperInvariant(),
                NormalizedEmail = model.EmailAddress.ToUpperInvariant(),
                PasswordHash = new PasswordHasher<ApiUser>().HashPassword(new ApiUser(), model.Password),
                SecurityStamp = Guid.NewGuid().ToString()
            };

            //Creates a new account
            var registrationResult = await _userManager.CreateAsync(user);
            if (!registrationResult.Succeeded)
            {
                _logger.LogError("The 'User' account registration failed: {Errors}",
                    string.Join(", ", registrationResult.Errors.Select(error => error.Description)));
                return BadRequest("The account registration failed.");
            }

            //Assigning registered user account to the 'User' role
            var assigningRoleResult = await _userManager.AddToRoleAsync(user, AuthConstants.UserRole);
            if (!assigningRoleResult.Succeeded)
            {
                _logger.LogError("Failed to assign' User' role to the registered user: {Errors}",
                    string.Join(", ", assigningRoleResult.Errors.Select(error => error.Description)));
                return BadRequest("Failed to assign 'User' role to the registered user.");
            }

            _logger.LogInformation("The account successfully registered.");

            //Sends the confirmation email link to the account 'Email'.
            //Exception for the default emails: email will not be sent, and the link will be returned to OkObjectResult as a text message.
            var emailSendResult = await SendConfirmationLinkAsync(user);

            return emailSendResult ??
                Ok("Account registration successful. Please check your email for confirmation link.");
        }
        catch (Exception ex)
        {
            return Exception(ex);
        }
    }

    /// <summary>
    /// Asynchronously handles the user login process.
    /// </summary>
    /// <param name="model">The <see cref="LoginModel"/> containing user login credentials.</param>
    /// <returns>
    /// A task representing the asynchronous operation, containing an <see cref="ActionResult"/>.
    /// If the login is successful, a JWT token will be returned. 
    /// If the model validation fails, it returns a BadRequest result.
    /// If the user does not exist, it returns an Unauthorized result.
    /// If an exception occurs, it returns a Problem result.
    /// </returns>
    /// <remarks>
    /// This method validates the incoming request model, checks the user's credentials,
    /// generates a JWT token with claims and logs the user's sign-in event.
    /// </remarks>
    /// <response code="200">
    /// Returns the JWT token and its associated information upon successful authentication.
    /// </response>
    /// <response code="401">
    /// Invalid credentials. The requested account was not found, or the entered password 
    /// does not match the account, or failed to generate a sign-in token.
    /// </response>
    /// <response code="403">
    /// User exists, but email not confirmed.
    /// </response>
    /// <response code="409">
    /// The account email was not confirmed.
    /// </response>
    /// <response code="500">
    /// An unexpected error occurred while processing the request.
    /// </response>
    [HttpPost]
    [Route("login")]
    public async Task<ActionResult> LoginAsync(LoginModel model)
    {
        try
        {
            //Checks the login model's required parameters and null checks.
            var validationResult = ValidateAccountInput(model);
            if (validationResult is not null)
            {
                return validationResult;
            }

            //Gets user data if provided sign-in credentials are correct for the requested account
            var user = await ValidateUserAsync(model);
            if (user is null)
            {
                return Unauthorized("Invalid credentials. Please verify your username and password, and try again.");
            }

            //Checking if the email is confirmed before generating the token for login
            if (!user.EmailConfirmed)
            {
                return Conflict("Please confirm the email. Confirmation link should be sended to your email.");
            }

            //Getting the list of all roles that the user is assigned
            var userRoles = await _userManager.GetRolesAsync(user!);

            //Creates and initializes the list of Claim class for the requested user account
            List<Claim> authorizationClaims = [
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                ];

            //Adds to the Claim list a new authorization claim for each role of the user
            foreach (var role in userRoles)
            {
                authorizationClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            //Gets the sign-in token for the requested account
            var token = GenerateJwtToken(authorizationClaims);
            if(token is null)
            {
                _logger.LogWarning("Faled to generate sign-in token for requested account: '{UserEmail}'.", user.Email);
                return Unauthorized("Faled to sign-in user into requested account.");
            }

            _logger.LogInformation("Generating JWT token for user: {UserName}", user.UserName);
            _logger.LogInformation("User successfuly signed in.");

            return Ok(new {
                message = "Login successful. You are now authenticated.",
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token!.ValidTo,
                user = user.UserName,
                email = user.Email
            });
        }
        catch (Exception ex)
        {
            return Exception(ex);
        }
    }

    /// <summary>
    /// Deletes a user account asynchronously based on the provided deletion model.
    /// </summary>
    /// <param name="model">The deletion model containing information required to delete the user account.</param>
    /// <returns>An <see cref="ActionResult"/> representing the outcome of the deletion process.</returns>
    /// <remarks>
    /// This method validates the input model, checks for confirmation, validates the user, 
    /// and deletes the user account if all conditions are met. 
    /// In case of failures, it logs the errors and returns appropriate responses.
    /// </remarks>
    /// <response code="200">The account was deleted successfully or the deletion process was canceled by the user.</response>
    /// <response code="401">User lacks the necessary permissions to delete this account.</response>
    /// <response code="400">An issue occurred during the processing of the deletion request.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpDelete]
    [Route("delete")]
    public async Task<ActionResult> DeleteAsync(DeletionModel model)
    {
        try
        {
            //Checks the deletion model required parameters and null check
            var resultValidation = ValidateAccountInput(model);
            if (resultValidation != null)
            {
                return resultValidation;
            }
            //Checks if the user has confirmed the account deletion
            if (!model.IsConfirmed)
            {
                _logger.LogInformation("Account deletion process was canceled by the user");
                return Ok(new
                {
                    message = "Account deletion process was canceled by the user.",
                    details = "No changes have been made to your account.",
                    help = "If you wish to delete your account in the future, please try again."
                });
            }
            //Checks the user credentials, if the user has the authority to change this account data
            var user = await ValidateUserAsync(model);

            //If the Validation of the user done above is failed
            if (user is null || !user.EmailConfirmed)
            {
                _logger.LogWarning("Unauthorized deletion attempt for account with login: {UserAccountLogin}", model.UsernameOrEmail);
                return Unauthorized("Lack the necessary permissions to delete this account.");
            }

            //Deleting the account
            var result = await _userManager.DeleteAsync(user!);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to delete account: {Errors}",
                    string.Join(", ", result.Errors.Select(error => error.Description)));
                return BadRequest("Failed to delete account.");
            }

            _logger.LogInformation("Account successfully deleted.");

            //Sending email with account changed information
            EmailDTO email = new()
            {
                Email = user!.Email!,
                Subject = "Account Deleted",
                TextMessage = "We are sorry to see you go. Account deleted by the user request.",
                Body = "<span>We are sorry to see you go. Account deleted by the user request.</span>"
            };
            var emailSendResult = SendEmail(email, "Failed to send email is information to the user.");

            return emailSendResult ??
                Ok("Your account has been successfully removed from our system.");
        }
        catch (Exception ex)
        {
            return Exception(ex);
        }
    }

    /// <summary>
    /// Updates the password for a user account.
    /// </summary>
    /// <param name="model">The password reset model contains the user's current and new passwords.</param>
    /// <returns>An <see cref="ActionResult"/> indicating the outcome of the password update operation.</returns>
    /// <remarks>
    /// This method validates the incoming <paramref name="model"/> for null values and 
    /// checks whether the user is authorized to change the password. If the user is 
    /// authenticated, it attempts to change the password using the provided current 
    /// password and new password. If the update is successful, a success message is 
    /// returned; otherwise, an error message detailing the issue is provided.
    ///
    /// If an exception occurs during the execution, a 500 Internal Server Error response 
    /// is returned with a generic error message.
    /// </remarks>
    /// <response code="200">The password was changed successfully</response>
    /// <response code="401">User lacks the permission to change the account password.</response>
    /// <response code="400">An issue occurred during the processing of the updating account password.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPut]
    [Route("password/reset")]
    public async Task<ActionResult> UpdatePasswordAsync(PasswordResetModel model)
    {
        try
        {
            //Checks the password reset model required parameters and null check
            var validationResult = ValidateAccountInput(model);
            if (validationResult is not null)
            {
                return validationResult;
            }

            //Checks the user credentials, if the user has the authority to change this account data
            var user = await ValidateUserAsync(model);
            //If the Validation of the user done above is failed
            if (user is null || !user.EmailConfirmed)
            {
                return Unauthorized("Lack the necessary permissions to change password for this account.");
            }

            //Changes user password after confirming the current password as the model.Password
            var result = await _userManager.ChangePasswordAsync(user!, model.Password, model.NewUserPassword);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to change account password: {Errors}",
                    string.Join(", ", result.Errors.Select(error => error.Description)));
                return BadRequest("Failed to change account password.");
            }

            //Sending email with account changed information
            EmailDTO email = new()
            {
                Email = user!.Email!,
                Subject = "The account password has been changed.",
                TextMessage = "Your account password has been changed. If you did not change the password, please contact the support team immediately.",
                Body = "<span>Your account password has been changed. If you did not change the password, please contact the support team immediately.</span>"
            };
            var sendEmailResult = SendEmail(email, "Failed to send email is information to the user.");

            return sendEmailResult ??
                Ok("Password changed successfully. Failed to send email is information to the user.");
        }
        catch (Exception ex)
        {
            return Exception(ex);
        }
    }

    /// <summary>
    /// Updates the account data for a user.
    /// </summary>
    /// <param name="model">The model containing the updated account information.</param>
    /// <returns>An <see cref="ActionResult"/> indicating the outcome of the update operation.</returns>
    /// <remarks>
    /// This method validates the incoming <paramref name="model"/> for null values and 
    /// checks whether the user has the necessary permissions to update the account data.
    /// If the user is authenticated and the data is valid, the user's account information 
    /// is updated with the provided new login and email address. If the update is successful, 
    /// a success message is returned; otherwise, an error message detailing the issue is provided.
    ///
    /// If an exception occurs during the execution, a 500 Internal Server Error response 
    /// is returned with a generic error message.
    /// </remarks>
    /// <response code="200">The account data was updated successfully</response>
    /// <response code="401">User lacks the necessary permissions to update the account data.</response>
    /// <response code="400">An issue occurred during the processing of the updating account data.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPut]
    [Route("update")]
    public async Task<ActionResult> UpdateAccountDataAsync(UpdateModel model)
    {
        try
        {
            //Checks the password reset model required parameters and null check
            var validationResult = ValidateAccountInput(model);
            if (validationResult != null)
            {
                return validationResult;
            }

            //Checks the user credentials, if the user has the authority to change this account data
            var user = await ValidateUserAsync(model);
            //If the Validation of the user done above is failed
            if (user is null || !user.EmailConfirmed)
            {
                return Unauthorized("Lack the necessary permissions to update this account.");
            }

            string beforeUpdateEmail = user.Email!;

            //Check if the updated login value is provided and is different from the current username.
            if (!string.IsNullOrWhiteSpace(model.UpdatedLogin) &&
                !user.UserName!.Equals(model.UpdatedLogin))
            {
                //Update the username and normalize it.
                user.UserName = model.UpdatedLogin;
                user.NormalizedUserName = model.UpdatedLogin.ToUpperInvariant();
            }
            //Check if the updated email address is and confirm that the updated email is provided,
            //and they are different from the current email address, but they are same as each other
            if (!string.IsNullOrWhiteSpace(model.UpdatedEmailAddress) &&
                !string.IsNullOrWhiteSpace(model.ConfirmUpdatedEmailAddress) &&
                !user.Email!.Equals(model.UpdatedEmailAddress) &&
                model.UpdatedEmailAddress.Equals(model.ConfirmUpdatedEmailAddress))
            {
                //Update the email and normalize it
                user.Email = model.UpdatedEmailAddress;
                user.NormalizedEmail = model.UpdatedEmailAddress.ToUpperInvariant();
                //Mark the email as unconfirmed since it's been changed.
                user.EmailConfirmed = false;
            }

            //Update the account
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to change account password: {Errors}",
                    string.Join(", ", result.Errors.Select(error => error.Description)));
                return BadRequest("Failed to update account.");
            }

            //The information message to be sent about updating the user account
            string infoMessage = user.Email!.Equals(user.Email) ?
                "Some of your account data has been changed. Please contact the support team immediately if you did not request it." :
                "Your account email address has been changed. This email was removed from your account. If you did not request it, please contact the support team immediately.";

            EmailDTO email = new()
            {
                //Ensure that the email is sent to the previous email in case it was changed and the user did not request this action
                Email = beforeUpdateEmail,
                TextMessage = infoMessage,
                Body = "<span>" + infoMessage + "</span>",
                Subject = "Updated account data"
            };

            var infoEmailSendResult = SendEmail(email, "Failed to send email is information to the user.");

            //Check if the account's email address has been changed. If so, a confirmation email link must be sent to the new email address.
            if (!user.EmailConfirmed)
            {
                //Sends the confirmation link to the provided by user email, exception for the default emails
                var linkConfirmSendResult = await SendConfirmationLinkAsync(user);
                return linkConfirmSendResult ??
                    Ok("Account updated successfully. Please check your new email for confirmation link. If you did not updated your account please contact the support team.");
            }

            return infoEmailSendResult ??
                Ok("Account updated successfully.");
        }
        catch (Exception ex)
        {
            return Exception(ex);
        }
    }

    [ApiVersionNeutral]
    [HttpGet]
    [Route("/account/confirmemail")]
    public async Task<ActionResult> ConfirmEmailAsync(string userId, string token)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound("Account was not found.");
            }

            var isValid = await _userManager.ConfirmEmailAsync(user, token);

            return isValid.Succeeded ?
                Ok("The account successfully confirmed.") :
                BadRequest("Request failed. Please contact the support team.");
        }
        catch (Exception ex)
        {
            return Exception(ex);
        }
    }


    /// <summary>
    /// Resends the email confirmation link to a registered user if the email is not confirmed.
    /// </summary>
    /// <param name="email">The email address of the user to confirm.</param>
    /// <returns>A status message indicating whether the confirmation email was resent successfully.</returns>
    /// <response code="200">The confirmation email was successfully sent or returned in the response.</response>
    /// <response code="400">The email address is null, empty, or invalid.</response>
    /// <response code="404">The account associated with the email address was not found.</response>
    /// <response code="500">An internal server error occurred while processing the request.</response>
    [HttpPost]
    [Route("/account/confirmemail/resend")]
    public async Task<ActionResult> ResendEmailConfirmationAsync([Required][FromBody]string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("The provided email is null or empty");
                return BadRequest(new { message = "The email cannot be null or empty" });
            }
            if (!new RegistrationModel().IsValidEmail(email))
            {
                _logger.LogWarning("The email not valid: '{Email}'.", email);
                return BadRequest(new { message = "The email is invalid format." });
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user is null)
            {
                _logger.LogWarning("The user account was not found.");
                return NotFound(new { message = "Account was not found." });
            }

            // Sends the confirmation email link to the account 'email'.
            // For default accounts, skips email sending and returns the link directly in the response.
            var emailSendResult = await SendConfirmationLinkAsync(user);

            return emailSendResult ??
                Ok(new { message = "Confirmation email sent. Please check your inbox." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while resending the confirmation email for '{Email}'.", email);
            return Exception(ex);
        }
    }

    #region Helper Methods

    /// <summary>
    /// Checks if an email address already exists in the database system.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. 
    /// The task result contains a boolean value indicating whether the email does not exist (true) 
    /// or if it exists (false).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the provided email is null.</exception>
    private async Task<bool> DoesEmailExistsAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentNullException(nameof(email), "Email cannot be null.");
        }

        var result = await _userManager.FindByEmailAsync(email);

        return result is not null;
    }

    /// <summary>
    /// Checks if a username already exists in the system.
    /// </summary>
    /// <param name="name">The username to check.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. 
    /// The task result contains a boolean value indicating whether the username exists (true) 
    /// or if it does not exist (false).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the provided name is null.</exception>
    private async Task<bool> DoesNameExistsAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentNullException(nameof(name), "Name cannot be null.");
        }

        var result = await _userManager.FindByNameAsync(name);

        return result is not null;
    }

    /// <summary>
    /// Checks user credentials based on login type (email or username).
    /// </summary>
    /// <param name="model">The login model containing user credentials.</param>
    /// <returns>The authenticated user if successful; otherwise, null.</returns>
    private async Task<ApiUser?> ValidateUserAsync(AccountModel model)
    {
        ApiUser? user = model.IsValidEmail(model.UsernameOrEmail) ?
            await _userManager.FindByEmailAsync(model.UsernameOrEmail) :
            await _userManager.FindByNameAsync(model.UsernameOrEmail);

        return user is null ||
            !await _userManager.CheckPasswordAsync(user, model.Password) ?
            null :
            user;
    }

    /// <summary>
    /// Generates a JWT token based on the provided claims.
    /// </summary>
    /// <param name="claims">The claims to include in the token.</param>
    /// <returns>A new JWT token.</returns>
    private JwtSecurityToken GenerateJwtToken(IEnumerable<Claim> claims)
    {
        var signInKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration[SecurityConstants.TokenDataKey + ':' + SecurityConstants.TokenAdminSecretKey]!));

        return new JwtSecurityToken
        (
            issuer: _configuration[SecurityConstants.TokenDataKey + ':' + SecurityConstants.TokenValidIssuerKey],
            audience: _configuration[SecurityConstants.TokenDataKey + ':' + SecurityConstants.TokenValidAudienceKey],
            expires: DateTime.Now.AddHours(3),
            claims: claims,
            signingCredentials: new SigningCredentials(signInKey, SecurityAlgorithms.HmacSha256Signature)
        );
    }

    /// <summary>
    /// Validates the specified account model for required fields and null checks.
    /// </summary>
    /// <param name="model">The account model to validate.</param>
    /// <returns>
    /// A <see cref="BadRequestObjectResult"/> if validation fails or the model is null; otherwise, null.
    /// </returns>
    /// <remarks>
    /// This method checks if the account model is null and logs an error if so.
    /// It also validates the model's properties and logs any validation errors.
    /// </remarks>
    /// <response code="400">Account model is null or if validation failed.</response>
    private BadRequestObjectResult? ValidateAccountInput(AccountModel model)
    {
        if (model is null)
        {
            _logger.LogError("Account model is null.");
            return BadRequest(new
            {
                message = "Account model cannot be null.",
                help = "Please provide a valid account model."
            });
        }

        if (!model.TryValidateModel(out string errorMessage))
        {
            _logger.LogError("Validation failed: {ErrorMessage}. Model State: {ModelState}", errorMessage, model);
            return BadRequest(new
            {
                message = "Validation failed for the account model.",
                error = errorMessage,
                help = "Please ensure all required fields are filled out correctly."
            });
        }
        return null;
    }

    /// <summary>
    /// Sends an email confirmation link to the specified user.
    /// </summary>
    /// <param name="user">
    /// The <see cref="ApiUser"/> for whom the confirmation email is to be sent. The user must have a valid email address.
    /// </param>
    /// <returns>
    /// Returns an <see cref="ObjectResult"/> indicating the result of the operation:
    /// - Returns <see cref="BadRequest"/> if the user or email is null, or if the email format is invalid.
    /// - Returns <see cref="Ok"/> if the email with the confirmation link was successfully sent.
    /// - Returns <see cref="Problem"/> if an error occurs during the email sending process.
    /// </returns>
    private async Task<ObjectResult> SendConfirmationLinkAsync(ApiUser user)
    {
        //Null checks
        if(user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogError("Failed to send 'Email' with email confirmation link, the 'ApiUser' object or 'ApiUser.Email' parameter is empty or null.");
            return new ObjectResult(BadRequest("The account data and 'Email' is required, the 'Email' and account data cannot be null or empty."));
        }
        //Email format check
        if (!new RegistrationModel().IsValidEmail(user.Email))
        {
            _logger.LogWarning("Failed to send 'Email' with email confirmation link, provided 'Email' is not in valid format: {EmailAddress}.", user.Email);
            return new ObjectResult(BadRequest("The provided 'Email' not in valid format. Please check the 'Email'."));
        }

        //Generates a confirmation link
        string confirmLink = await GenerateConfirmationEmailLink(HttpContext, user);
        
        if(string.IsNullOrWhiteSpace(confirmLink))
        {
            _logger.LogError("Failed to generate the email confirmation link for the provided 'Email': {EmailAddress}.", user.Email);
            return new ObjectResult(BadRequest("Failed to generate the email confirmation link. " +
                "Please try again later or contact the support team."));
        }

        //Prepare the email content with the confirmation link
        EmailDTO email = new()
        {
            Email = user.Email!,
            Subject = "Confirmation email",
            Body = $"<h1>Welcome, {user.UserName}!<h1/>" +
                $"Please, confirm your email address by <a href='{confirmLink}'>clicking this link </a><br>" +
                $", or copy and paste into the browser the link  below:<br>" +
                $"{confirmLink}",
            TextMessage = $"Welcome, {user.UserName}! Please confirm your email address by copying and pasting into the browser the link  below: {confirmLink}"
        };

        //Sends the confirmation link to the provided by user email. Exception for the default emails: the email will not be sent.
        var emailSendResult = SendEmail(email, "An unexpected error occurred while sending email confirmation link. Please try to resend confirmation link later or contact support if the issue persists.");

        return emailSendResult ??
            new ObjectResult(Ok("Email with confirmation link sended successfully."));
    }

    /// <summary>
    /// Generates an email confirmation link for the specified user.
    /// </summary>
    /// <param name="httpContext">The current <see cref="HttpContext"/> to retrieve the request scheme and host for constructing the URL.</param>
    /// <param name="user">The <see cref="ApiUser"/> for whom the confirmation link is to be generated. The user must have a valid email address.</param>
    /// <returns>
    /// Returns the generated email confirmation link as a string. If the user or email is null, an empty string is returned.
    /// </returns>
    private async Task<string> GenerateConfirmationEmailLink(HttpContext httpContext, ApiUser user)
    {
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogError("Failed to generate the confirmation link for email, the 'ApiUser' object or 'ApiUser.Email' parameter is empty or null.");
            return string.Empty;
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        //Encodes the generated token for safe URL transmission
        string encodedToken = WebUtility.UrlEncode(token);

        //Constructs and returns the confirmation link URL
        return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/account/confirmemail?userId={user.Id}&token={encodedToken}";
    }

    /// <summary>
    /// Validates the <see cref="RegistrationModel"/> provided by the user.
    /// Checks if the account input is valid, whether the email or username already exists.
    /// </summary>
    /// <param name="model">The <see cref="RegistrationModel"/> containing user registration details such as email and username.</param>
    /// <returns>
    /// Returns a <see cref="BadRequestObjectResult"/> if any validation fails, such as:
    /// - Invalid account input
    /// - Email address already exists
    /// - Username is already taken
    /// Returns null if all validations pass.
    /// </returns>
    private BadRequestObjectResult? ValidateRegistrationModelAsync(RegistrationModel model)
    {
        var validationResult = ValidateAccountInput(model);

        if (validationResult != null)
        {
            return validationResult;
        }

        //Checks if email address already in the system
        if (DoesEmailExistsAsync(model.EmailAddress).Result)
        {
            return BadRequest(new
            {
                message = "An account with the provided email address already exists.",
                reason = "The email address is already associated with another account.",
                help = "Try using a different email address, or if you already have an account, use the forgot password option."
            });
        }

        //Checks if user name already in the system
        if (DoesNameExistsAsync(model.UsernameOrEmail).Result)
        {
            return BadRequest(new
            {
                message = "An account with the provided username already exists.",
                reason = "The chosen username is already taken by another user.",
                help = "Please try registering with a different username or consider using your email address."
            });
        }

        //Return null if no validation issues are found
        return null;
    }

    /// <summary>
    /// Sends an email to the specified recipient using the provided <see cref="EmailDTO"/> object.
    /// Handles exceptions and prevents sending emails to default API accounts (admin and user).
    /// </summary>
    /// <param name="email">The <see cref="EmailDTO"/> object containing the email details such as recipient, subject, and body.</param>
    /// <param name="errorMessage">A custom error message to display in case of a failure to send the email.</param>
    /// <returns>
    /// If the email is successfully sent, returns <c>null</c>.
    /// If the email fails to send, returns an <see cref="ObjectResult"/> containing the error message.
    /// If the email belongs to a default API account (admin or user), returns an <see cref="OkObjectResult"/> with the email details in the response.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="email"/> parameter is null.</exception>
    private ObjectResult? SendEmail(EmailDTO email, string errorMessage) 
    {
        try
        {
            if(string.IsNullOrWhiteSpace(email.Email))
            {
                throw new ArgumentNullException(nameof(email), "The email address cannot be null or empty.");
            }

            //Prevent sending emails to default API accounts (admin or user) and return the email details in the response
            if (email.Email.Equals(_configuration[AuthConstants.AdminEmailKey]) ||
                email.Email.Equals(_configuration[AuthConstants.UserEmailKey]))
            {
                _logger.LogWarning("Unable to send email to the default API account: {EmailAddress}.", email.Email);
                return Ok(new
                {
                    message = "Unable to send email to the default API accounts",
                    body = email.TextMessage ?? email.Body,
                    subject = email.Subject
                });
            }

            var emailSendResult = _emailSender.SendEmailAsync(email.Email, email.Subject, email.Body);

            //If there is no exception in the result, return null indicating success
            return emailSendResult.Exception is null ?
                null :
                throw emailSendResult.Exception;
        }
        catch (Exception ex)
        {
            _logger.LogError("Message: {ErrorMessage}. Error: {Exception}.", errorMessage, ex);
            return Problem(detail: errorMessage, statusCode: 500);
        }
    }

    /// <summary>
    /// Handles exceptions by logging the error and returning a standardized problem response.
    /// This method is used to capture and respond to unexpected errors in the user registration process.
    /// </summary>
    /// <param name="ex">The <see cref="Exception"/> object representing the error that occurred.</param>
    /// <returns>
    /// An <see cref="ObjectResult"/> containing the error details and a status code of 500.
    /// The response includes a user-friendly error message instructing the user to try again later or contact support.
    /// </returns>
    private ObjectResult Exception(Exception ex)
    {
        _logger.LogError(ex, "An error occured while registering user.");
        return Problem(
            detail: "An unexpected error occurred while processing your request. Please try again later or contact " +
            "support if the issue persists.",
            statusCode: 500);
    }
    #endregion Helper Methods
}

