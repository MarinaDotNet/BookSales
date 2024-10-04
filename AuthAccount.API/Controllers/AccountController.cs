using Asp.Versioning;
using AuthAccount.API.Constants;
using AuthAccount.API.Models;
using AuthAccount.API.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;

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
[ApiController]
[ApiVersion("1.0")]
public class AccountV1Controller(UserManager<ApiUser> userManager, IConfiguration configuration, ILogger<AccountV1Controller> logger) : ControllerBase
{
    private readonly UserManager<ApiUser> _userManager = userManager ??
        throw new ArgumentNullException(nameof(userManager), "UserManager cannot be null.");
    private readonly IConfiguration _configuration = configuration ??
        throw new ArgumentNullException(nameof(configuration), "Configuration cannot be null.");
    private readonly ILogger<AccountV1Controller> _logger = logger ?? 
        throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");

    /// <summary>
    /// Registers a new administrator account asynchronously.
    /// </summary>
    /// <param name="model">The registration model <see cref="RegistrationModel"/> containing the administrator's details.</param>
    /// <returns>An <see cref="ActionResult"/> representing the outcome of the registration process.</returns>
    /// <remarks>
    /// This method validates the input model, checks for existing accounts with the same email 
    /// or username, creates a new user, and assigns the admin role. 
    /// In case of failures, it logs the errors and returns appropriate responses.
    /// </remarks>
    /// <response code="200">Returns a success message upon successful administrator account registration.</response>
    /// <response code="400">Invalid data provided for administrator account registration. Ensure all required fields 
    /// are completed correctly.</response>
    /// <response code="500">An unexpected error occurred while processing the administrator account registration 
    /// request.</response>
    [HttpPost]
    [Route("register")]
    public async Task<IActionResult> RegisterAdminAsync(RegistrationModel model)
    {
        try
        {
            var validationResult = ValidateAccountInput(model);

            if (validationResult != null)
            {
                return validationResult;
            }

            if (await DoesEmailExistsAsync(model.EmailAddress))
            {
                return BadRequest(new
                {
                    message = "An account with the provided email address already exists.",
                    reason = "The email address is already associated with another account.",
                    help = "Try using a different email address, or if you already have an account, use the forgot password option."
                });
            }

            if (await DoesNameExistsAsync(model.UsernameOrEmail))
            {
                return BadRequest(new
                {
                    message = "An account with the provided username already exists.",
                    reason = "The chosen username is already taken by another user.",
                    help = "Please try registering with a different username or consider using your email address."
                });
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

           var registrationResult = await _userManager.CreateAsync(user);

            if (!registrationResult.Succeeded)
            {
                _logger.LogError("Admin registration failed: {Errors}", string.Join(", ", registrationResult.Errors.Select(error => error.Description)));
                return BadRequest(new
                {
                    message = "Admin registration failed."
                });
            }
            var assigningRoleResult = await _userManager.AddToRoleAsync(user, AuthConstants.AdminRole);

            if (!assigningRoleResult.Succeeded)
            {
                _logger.LogError("Failed to assign admin role to the registered user: {Errors}", string.Join(", ", assigningRoleResult.Errors.Select(error => error.Description)));
                return BadRequest(new
                {
                    message = "Failed to assign admin role to the registered user."
                });
            }

            _logger.LogInformation("Admin account successfully registered.");
            return Ok(new
            {
                message = "Account registration successful. The account is now active and ready for use.",
                note = "Please keep your credentials secure, and contact support if you encounter any issues."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while registering admin.");
            return Problem(
                detail: "An unexpected error occurred while processing your request. Please try again later or contact " +
                "support if the issue persists.",
                statusCode: 500);
        }
    }

    /// <summary>
    /// Registers a new user account asynchronously.
    /// </summary>
    /// <param name="model">The registration model containing the user's details.</param>
    /// <returns>An <see cref="ActionResult"/> representing the outcome of the registration process.</returns>
    /// <remarks>
    /// This method validates the input model, checks for existing accounts with the same email 
    /// or username, creates a new user, and assigns the user role. 
    /// In case of failures, it logs the errors and returns appropriate responses.
    /// </remarks>
    /// <response code="200">Returns a success message upon successful account registration.</response>
    /// <response code="400">Invalid data provided for user account registration. Ensure all required fields are filled correctly.</response>
    /// <response code="500">An unexpected error occurred while processing the registration request.</response>
    [HttpPost]
    [Route("user/register")]
    public async Task<ActionResult> RegisterUserAsync(RegistrationModel model)
    {
        try
        {
            var validationResult = ValidateAccountInput(model);

            if (validationResult != null)
            {
                return validationResult;
            }

            if (await DoesEmailExistsAsync(model.EmailAddress))
            {
               return BadRequest(new 
               {
                    message = "An account with the provided email address already exists.",
                    reason = "The email address is already associated with another account.",
                    help = "Try using a different email address, or if you already have an account, use the forgot password option."
               });
            }

            if (await DoesNameExistsAsync(model.UsernameOrEmail))
            {
                return BadRequest(new
                {
                    message = "An account with the provided username already exists.",
                    reason = "The chosen username is already taken by another user.",
                    help = "Please try registering with a different username or consider using your email address."
                });
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

            var registrationResult = await _userManager.CreateAsync(user);

            if (!registrationResult.Succeeded)
            {
                _logger.LogError("User registration failed: {Errors}", string.Join(", ", registrationResult.Errors.Select(error => error.Description)));
                return BadRequest(new 
                { 
                    message = "User registration failed." 
                });
            }
            var assigningRoleResult = await _userManager.AddToRoleAsync(user, AuthConstants.UserRole);

            if (!assigningRoleResult.Succeeded)
            {
                _logger.LogError("Failed to assign user role to the registered user: {Errors}", string.Join(", ", assigningRoleResult.Errors.Select(error => error.Description)));
                return BadRequest(new 
                { 
                    message = "Failed to assign user role to the registered user."
                });
            }

            _logger.LogInformation("User account successfully registered.");
            return Ok(new 
            {
                message = "Account registration successful. The account is now active and ready for use.",
                note = "Please keep your credentials secure, and contact support if you encounter any issues."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while registering user.");
            return Problem(
                detail: "An unexpected error occurred while processing your request. Please try again later or contact " +
                "support if the issue persists.",
                statusCode: 500);
        }
    }

    /// <summary>
    /// Asynchronously handles the user login process.
    /// </summary>
    /// <param name="model">The <see cref="LoginModel"/> containing user login credentials.</param>
    /// <returns>
    /// A task that represents the asynchronous operation, containing an <see cref="ActionResult"/>.
    /// If the login is successful, it returns a JWT token. 
    /// If the model validation fails, it returns a BadRequest result.
    /// If the user does not exist, it returns an Unauthorized result.
    /// If an exception occurs, it returns a Problem result.
    /// </returns>
    /// <remarks>
    /// This method validates the incoming request model, checks the user's credentials,
    /// generates a JWT token with claims, and logs the user's sign-in event.
    /// </remarks>
    /// <response code="200">Returns the JWT token and its associated information upon successful authentication.</response>
    /// <response code="401">Invalid credentials. The requested account was not found, or the entered password 
    /// does not match the account.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPost]
    [Route("login")]
    public async Task<ActionResult> LoginAsync(LoginModel model)
    {
        try
        {
            var validationResult = ValidateAccountInput(model);

            if (validationResult != null)
            {
                return validationResult;
            }

            var user = await ValidateUserAsync(model);
            if(user is null)
            {
                return Unauthorized(new { 
                    message = "Invalid credentials. Please verify your username and password, and try again.",
                    code = "UNAUTHORIZED_ACCESS",
                    help = "If you forgot your password, please reset it."
                });
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            List<Claim> authorizationClaims = [
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                ];

            foreach(var role in userRoles)
            {
                authorizationClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = GenerateJwtToken(authorizationClaims);

            _logger.LogInformation("Generating JWT token for user: {UserName}", user.UserName);
            _logger.LogInformation("User successfuly signed in.");
            return Ok(new {
                message = "Login successful. You are now authenticated.",
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo,
                user = user.UserName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while login the user.");
            return Problem(
                detail: "An unexpected error occurred while processing your request. Please try again later or contact " +
                "support if the issue persists.", 
                statusCode: 500);
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
            var resultValidation = ValidateAccountInput(model);
            if (resultValidation != null)
            {
                return resultValidation;
            }

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

            var user = await ValidateUserAsync(model);

            if(user is null)
            {
                _logger.LogWarning("Unauthorized deletion attempt for account with login: {UserAccountLogin}", model.UsernameOrEmail);
                return Unauthorized(new
                {
                    message = "Lack the necessary permissions to delete this account.",
                    details = "Your current role does not have the required permissions to perform this action.",
                    help = "Please contact an administrator if you believe this is an error or if you need further assistance."
                });
            }

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                _logger.LogError("Failed to delete account: {Errors}", 
                    string.Join(", ", result.Errors.Select(error => error.Description)));
                return BadRequest(new
                {
                    message = "Failed to delete account.",
                    details = "There was an issue processing your request to delete the account.",
                    help = "Please ensure you have provided the correct account information and try again. If the problem persists, contact support for assistance."
                });
            }

            _logger.LogInformation("Account successfully deleted.");
            return Ok(new
            {
                message = "Account deleted.",
                details = "Your account has been successfully removed from our system.",
                help = "If you have any further questions or need assistance, please contact our support team."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while deleting the account.");
            return Problem(
                detail: "An unexpected error occurred while processing your request. Please try again later or contact " +
                "support if the issue persists.",
                statusCode: 500);
        }
    }

    /// <summary>
    /// Updates the password for a user account.
    /// </summary>
    /// <param name="model">The password reset model containing the user's current and new passwords.</param>
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
    /// <response code="401">User lacks the necessary permissions to change the account password.</response>
    /// <response code="400">An issue occurred during the processing of the updating account password.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPut]
    [Route("password/reset")]
    public async Task<ActionResult> UpdatePasswordAsync(PasswordResetModel model)
    {
        try
        {
            var validationResult = ValidateAccountInput(model);
            if(validationResult != null)
            {
                return validationResult;
            }

            var user = await ValidateUserAsync(model);
            if (user is null)
            { 
                return Unauthorized(new
                {
                    message = "Lack the necessary permissions to change password for this account.",
                    details = "Your current role does not have the required permissions to perform this action.",
                    help = "Please contact an administrator if you believe this is an error or if you need further assistance."
                });
            }

            var result = await _userManager.ChangePasswordAsync(user, model.Password, model.NewUserPassword);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to change account password: {Errors}",
                    string.Join(", ", result.Errors.Select(error => error.Description)));
                return BadRequest("Failed to change account password.");
            }

            return Ok(new
            {
                message = "Password changed successfully.",
                help = "You can now log in with your new password."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while deleting the account.");
            return Problem(
                detail: "An unexpected error occurred while processing your request. Please try again later or contact " +
                "support if the issue persists.",
                statusCode: 500);
        }
    }

    /// <summary>
    /// Updates the password for a user account by an admin.
    /// </summary>
    /// <param name="model">The model containing the user account identifier and new password information.</param>
    /// <returns>An <see cref="ActionResult"/> indicating the result of the password update operation.</returns>
    /// <remarks>
    /// Returns BadRequest if the model is invalid or the user is not found.
    /// Returns Ok if the password update is successful.
    /// </remarks>
    /// <response code="200">The password was changed successfully</response>
    /// <response code="401">User lacks the necessary permissions to change the account password.</response>
    /// <response code="400">An issue occurred during the processing of the updating account password.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPut]
    [Route("admin/update/password")]
    public async Task<ActionResult> UpdateUserPasswordAsync(AdminPasswordResetModel model)
    {
        try
        {
            var validationResult = ValidateAccountInput(model);
            if (validationResult != null)
            {
                return validationResult;
            }

            var admin = ValidateUserAsync(model).Result;
            if (admin is null || 
                !await _userManager.IsInRoleAsync(admin, AuthConstants.AdminRole))
            {
                return Unauthorized(new
                {
                    message = "Access denied.",
                    details = "You must be an authorized administrator to perform this action.",
                    help = "Please contact your system administrator if you believe you have received this message in error."
                });
            }

            var currentAccount = model.IsValidEmail(model.UserIdentifier) ?
                await _userManager.FindByEmailAsync(model.UserIdentifier) :
                await _userManager.FindByNameAsync(model.UserIdentifier);
            if (currentAccount is null)
            {
                return NotFound(new
                {
                    message = "Customer account not found.",
                    details = "No account exists with the provided identifier. Please check the email or username and try again.",
                    help = "If you believe this account should exist, please contact support for further assistance."
                });
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(currentAccount);

            var result = await _userManager.ResetPasswordAsync(currentAccount, resetToken, model.NewUserPassword);

            if (!result.Succeeded)
            {
                _logger.LogError("Failed to change account password: {Errors}",
                    string.Join(", ", result.Errors.Select(error => error.Description)));
                return BadRequest(new
                {
                    message = "Failed to update password for user account.",
                    details = "There was an issue processing your request to update the password for the account. Please check the provided information and ensure it meets the required criteria.",
                    help = "If the issue persists, please contact support for assistance."
                });
            }
            return Ok(new
            {
                message = "Account password updated successfully.",
                details = "User account password has been successfully updated.",
                help = "User can continue to use account as usual."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while updating data of the account.");
            return Problem(
                detail: "An unexpected error occurred while processing your request. Please try again later or contact " +
                "support if the issue persists.",
                statusCode: 500);
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
            var validationResult = ValidateAccountInput(model);
            if(validationResult != null) 
            {
                return validationResult; 
            }

            var userCurrent = await ValidateUserAsync(model);

            if(userCurrent is null)
            {
                return Unauthorized(new
                {
                    message = "Lack the necessary permissions to update this account.",
                    details = "Your current role does not have the required permissions to perform this action.",
                    help = "Please contact an administrator if you believe this is an error or if you need further assistance."
                });
            }

            ApiUser updatedUser = userCurrent;
                
            if(!string.IsNullOrWhiteSpace(model.UpdatedLogin) &&
                !userCurrent.UserName!.Equals(model.UpdatedLogin))
            {
                updatedUser.UserName = model.UpdatedLogin;
                updatedUser.NormalizedUserName = model.UpdatedLogin.ToUpperInvariant();
            }
            if(!string.IsNullOrWhiteSpace(model.UpdatedEmailAddress) &&
                !userCurrent.Email!.Equals(model.UpdatedEmailAddress))
            {
                updatedUser.Email = model.UpdatedEmailAddress; 
                updatedUser.NormalizedEmail = model.UpdatedEmailAddress.ToUpperInvariant();
            }

            var result = await _userManager.UpdateAsync(updatedUser);

            if (!result.Succeeded)
            {
                _logger.LogError("Failed to change account password: {Errors}",
                    string.Join(", ", result.Errors.Select(error => error.Description)));
                return BadRequest(new
                {
                    message = "Failed to update account.",
                    details = "There was an issue processing your request to update the account.",
                    help = "Please ensure you have provided the correct account information and try again. If the problem persists, contact support for assistance."
                });
            }

            return Ok(new
            {
                message = "Account updated successfully."
            });
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "An error occured while updating data of the account.");
            return Problem(
                detail: "An unexpected error occurred while processing your request. Please try again later or contact " +
                "support if the issue persists.",
                statusCode: 500);
        }
    }

    /// <summary>
    /// Updates the user account information for an admin user.
    /// </summary>
    /// <param name="model">The model containing updated user account data.</param>
    /// <returns>An <see cref="ActionResult"/> indicating the result of the operation.</returns>
    /// <remarks>
    /// Returns BadRequest if the model is invalid or the user is not found.
    /// Returns Ok if the update is successful.
    /// </remarks>
    /// <response code="200">The account data was updated successfully</response>
    /// <response code="401">User lacks the necessary permissions to update the account data.</response>
    /// <response code="400">An issue occurred during the processing of the updating account data.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPut]
    [Route("admin/update")]
    public async Task<ActionResult> UpdateUserAccountAsync(AdminUpdateModel model)
    {
        try
        {
            var validationResult = ValidateAccountInput(model);
            if(validationResult != null)
            {
                return validationResult;
            }

            var admin = await ValidateUserAsync(model);
            if(admin is null ||
                !await _userManager.IsInRoleAsync(admin, AuthConstants.AdminRole))
            {
                return Unauthorized(new
                {
                    message = "Access denied.",
                    details = "You must be an authorized administrator to perform this action.",
                    help = "Please contact your system administrator if you believe you have received this message in error."
                });
            }

            var userCurrent = model.IsValidEmail(model.UserIdentifier) ?
                await _userManager.FindByEmailAsync(model.UserIdentifier) :
                await _userManager.FindByNameAsync(model.UserIdentifier);

            if (userCurrent is null)
            {
                return NotFound(new
                {
                    message = "Customer account not found.",
                    details = "No account exists with the provided identifier. Please check the email or username and try again.",
                    help = "If you believe this account should exist, please contact support for further assistance."
                });
            }

            ApiUser updatedUser = userCurrent;

            if (!string.IsNullOrWhiteSpace(model.UpdatedLogin) &&
                !userCurrent.UserName!.Equals(model.UpdatedLogin))
            {
                updatedUser.UserName = model.UpdatedLogin;
                updatedUser.NormalizedUserName = model.UpdatedLogin.ToUpperInvariant();
            }
            if (!string.IsNullOrWhiteSpace(model.UpdatedEmailAddress) &&
                !userCurrent.Email!.Equals(model.UpdatedEmailAddress))
            {
                updatedUser.Email = model.UpdatedEmailAddress;
                updatedUser.NormalizedEmail = model.UpdatedEmailAddress.ToUpperInvariant();
            }

            var result = await _userManager.UpdateAsync(updatedUser);

            if (!result.Succeeded)
            {
                _logger.LogError("Failed to update account data: {Errors}",
                    string.Join(", ", result.Errors.Select(error => error.Description)));
                return BadRequest(new
                {
                    message = "Failed to update account data.",
                    details = "There was an issue processing your request to update the account. Please check the provided information and ensure it meets the required criteria.",
                    help = "If the issue persists, please contact support for assistance."
                });
            }
            return Ok(new
            {
                message = "Account updated successfully.",
                details = "User account information has been successfully updated.",
                help = "User can continue to use account as usual."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while updating data of the account.");
            return Problem(
                detail: "An unexpected error occurred while processing your request. Please try again later or contact " +
                "support if the issue persists.",
                statusCode: 500);
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
            issuer: _configuration[SecurityConstants.TokenValidIssuerKey],
            audience: _configuration[SecurityConstants.TokenValidAudienceKey],
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
    #endregion Helper Methods
}

