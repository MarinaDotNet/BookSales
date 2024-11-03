using ApiUtilities.Constants;
using BookShop.WebApp.Models;
using BookShop.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BookShop.WebApp.Controllers;

/// <summary>
/// The <see cref="AccountController"/> handles user account-related operations, 
/// such as login, registration, password changes, and account updates.
/// </summary>
/// <remarks>
/// This controller provides actions to manage user authentication and account settings. 
/// It leverages services for user validation and token generation.
/// </remarks>
public class AccountController : Controller
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountController"/> class with
    /// specified caching, configuration, and logging services.
    /// </summary>
    /// <param name="memoryCache">An in-memory cache for storing and retrieving temporary data.</param>
    /// <param name="configuration">The configuration settings for the application.</param>
    /// <param name="logger">The logger instance for logging activities and errors within the controller.</param>
    public AccountController(IMemoryCache memoryCache, IConfiguration configiration, ILogger<AccountController> logger)
    {
        _memoryCache = memoryCache ?? 
            throw new ArgumentNullException(nameof(memoryCache), "The IMemoryCache should not be null.");
        _logger = logger ?? 
            throw new ArgumentNullException(nameof(logger), "The ILogger should not be null.");
        _configuration = configiration ??
            throw new ArgumentNullException(nameof(configiration), "The IConfiguration should not be null.");

        _httpClient.DefaultRequestHeaders.Add(SecurityConstants.AuthApiKey, _configuration[SecurityConstants.AuthApiKey + "Auth"]);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Api-Version", "1");
    }

    /// <summary>
    /// Logins the page.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns></returns>
    public IActionResult LoginPage(ViewModel? model) => View("Login", model ?? new ViewModel() { });

    /// <summary>
    /// Logins the asynchronous.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <param name="password">The password.</param>
    /// <returns></returns>
    public async Task<IActionResult> LoginAsync(string email, string password)
    {
        var loginData = new LoginDto
        {
            UserNameOrEmail = email,
            Password = password
        };

        if(!loginData.IsValid)
        {
            _logger.LogWarning("Validation error during login attempt: {ValidationError}", loginData.ValidationError);
            return View(new ViewModel { ErrorView = new() { AdditionalMessage = loginData.ValidationError } });
        }

        var content = new StringContent(JsonSerializer.Serialize(loginData), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(ApiEndpoints.Account.GetLoginToken, content);

        if(!response.IsSuccessStatusCode)
        {
            string message = await GetMessageFromResponseAsync(response);

            _logger.LogError("Login API request failed with status code { StatusCode}. Response message: {ResponseMessage}", response.StatusCode, message);

            return View(response.StatusCode == HttpStatusCode.Conflict ?
                "ResendEmailConfirmation" : "Login",
                new ViewModel { ErrorView = new() { AdditionalMessage = string.IsNullOrWhiteSpace(message) ?
                "Register API request failed. Please try again latter or contact the support team." : message } });
        }

        return await StoreTokenFromResponseAsync(response, "userToken") ?
       RedirectToAction("Index", "Home") :
       View(new ViewModel { ErrorView = new() { AdditionalMessage = "Store token failed" } });
    }

    /// <summary>
    /// Logouts this instance.
    /// </summary>
    /// <returns></returns>
    public IActionResult Logout()
    {
        _memoryCache.Remove("userToken");
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Manages the index.
    /// </summary>
    /// <returns></returns>
    public IActionResult ManageIndex()
    {
        if(IsTokenFromCasheValid(out (string token, string expiry, string userName, string email) userCache))
        {
            ViewModel accountData = new()
            {
                AccountView = new()
                {
                    UserName = userCache.userName,
                    Email = userCache.email
                }
            };
            return View(accountData);
        }

        return RedirectToAction("Logout", "Account");
    }

    /// <summary>
    /// Registers the page.
    /// </summary>
    /// <returns></returns>
    public IActionResult RegisterPage() => View("Register", new ViewModel() { });

    /// <summary>
    /// Registers the asynchronous.
    /// </summary>
    /// <param name="userName">Name of the user.</param>
    /// <param name="email">The email.</param>
    /// <param name="confirmationEmail">The confirmation email.</param>
    /// <param name="password">The password.</param>
    /// <param name="confirmationPassword">The confirmation password.</param>
    /// <returns></returns>
    public async Task<IActionResult> RegisterAsync(string userName, string email, string confirmationEmail, string password, string confirmationPassword)
    {
        RegisterDto registerData = new()
        {
            UserNameOrEmail = userName,
            EmailAddress = email,
            ConfirmEmailAddress = confirmationEmail,
            Password = password,
            ConfirmPassword = confirmationPassword
        };

        if (!registerData.IsValid)
        {
            _logger.LogWarning("Validation error during registration attempt: {ValidationError}", registerData.ValidationError);
            return View(new ViewModel { ErrorView = new() { AdditionalMessage = registerData.ValidationError } });
        }
        var content = new StringContent(JsonSerializer.Serialize(registerData), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(ApiEndpoints.Account.SendRegisterRequest, content);

        if (!response.IsSuccessStatusCode)
        {
            string message = await response.Content.ReadAsStringAsync();

            _logger.LogError("Register API request failed with status code { StatusCode}.", response.StatusCode);
            return View(new ViewModel { ErrorView = new() { AdditionalMessage = message ?? 
                "Register API request failed. Please try again latter or contact the support team." } });
        }
        string information = email.Equals("user@msichova.com", StringComparison.OrdinalIgnoreCase) ||
            email.Equals("admin@msichova.com", StringComparison.OrdinalIgnoreCase) ?
            await GetMessageFromResponseAsync(response) :
            "The confimation link sended to the email you provided. Please confirm your email address.";
        return View(new ViewModel { AccountView = new() { RegistrationMessage = information} });
    }

    /// <summary>
    /// Resends the email confirmation asynchronous.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <returns></returns>
    public async Task<IActionResult> ResendEmailConfirmationAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return View(new ViewModel() { ErrorView = new() { AdditionalMessage = "Email is required." } });
        }

        var content = new StringContent(JsonSerializer.Serialize(email), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(ApiEndpoints.Account.ReSendConfirmationRequest, content);

        if (!response.IsSuccessStatusCode)
        {
            string message = await GetMessageFromResponseAsync(response);
            _logger.LogError("ReSend confirm link API request failed with status code { StatusCode}. Response message: {ResponseMessage}", response.StatusCode, message);

            return View(new ViewModel()
            {
                ErrorView = new()
                {
                    AdditionalMessage = string.IsNullOrWhiteSpace(message) ?
                "ReSend confirm link API request failed. Please try again latter or contact the support team." : message
                }
            });
        }

        string information = email.Equals("user@msichova.com", StringComparison.OrdinalIgnoreCase) ||
            email.Equals("admin@msichova.com", StringComparison.OrdinalIgnoreCase) ?
            await GetMessageFromResponseAsync(response) :
            "The confirmation link resended successfully. Please check your email. If did not recieve the email please contact the support team.";

        return View(new ViewModel() { AccountView = new() { RegistrationMessage = information } });
    }

    /// <summary>
    /// Deletes the account page.
    /// </summary>
    /// <returns></returns>
    public IActionResult DeleteAccountPage() => View("DeleteAccount");

    /// <summary>
    /// Deletes the account asynchronous.
    /// </summary>
    /// <param name="accountPassword">The account password.</param>
    /// <param name="isConfirmed">if set to <c>true</c> [is confirmed].</param>
    /// <returns></returns>
    public async Task<IActionResult> DeleteAccountAsync(string accountPassword, bool isConfirmed)
    {
        if(!isConfirmed)
        {
            return View(new ViewModel() { ErrorView = new() { AdditionalMessage = "To delete account require to confirm the deletion process" } });
        }
        if (IsTokenFromCasheValid(out (string token, string expiry, string userName, string email) userCache))
        {
            DeleteAccountDto data = new()
            {
                UserNameOrEmail = userCache.userName,
                Password = accountPassword,
                IsConfirmed = isConfirmed
            };

            if (!data.IsValid)
            {
                return View(new ViewModel() { ErrorView = new() { AdditionalMessage = data.ValidationError } });
            }

            var request = new HttpRequestMessage(HttpMethod.Delete, ApiEndpoints.Account.SendDeleteAccountRequest)
            {
                Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);

            if(!response.IsSuccessStatusCode)
            {
                string message = await GetMessageFromResponseAsync(response);
                _logger.LogError("Delete account API request failed with status code {StatusCode}. Response message: {ResponseMessage}", response.StatusCode, message);

                return View(new ViewModel()
                {
                    ErrorView = new()
                    {
                        AdditionalMessage = string.IsNullOrWhiteSpace(message) ?
                        "Delete account API request failed. Please try again latter or contact the support team." : message
                    }
                });
            }

            string information = userCache.email.Equals("user@msichova.com", StringComparison.OrdinalIgnoreCase) ||
            userCache.email.Equals("admin@msichova.com", StringComparison.OrdinalIgnoreCase) ?
            await GetMessageFromResponseAsync(response) :
            "The account deleted successfully";

            _memoryCache.Remove("userToken");

            return LoginPage(new ViewModel() { AccountView = new() { RegistrationMessage =  information ?? "Account deleted"} });
        }

        return LoginPage(new ViewModel() { AccountView = new() { RegistrationMessage = "Your session cookie has expired. Please sign in." } });
    }

    /// <summary>
    /// Changes the password page.
    /// </summary>
    /// <returns></returns>
    public IActionResult ChangePasswordPage() => View("ChangePassword");

    /// <summary>
    /// Changes the password asynchronous.
    /// </summary>
    /// <param name="currentPassword">The current password.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="confirmPassword">The confirm password.</param>
    /// <returns></returns>
    public async Task<IActionResult> ChangePasswordAsync(string currentPassword, string newPassword,  string confirmPassword)
    {
        if(IsTokenFromCasheValid(out(string token, string expiry, string userName, string email) userCache))
        {
            PasswordChangeDto data = new()
            {
                UserNameOrEmail = userCache.userName,
                Password = currentPassword,
                NewUserPassword = newPassword,
                ConfirmNewUserPassword = confirmPassword
            };

            if (!data.IsPasswordChangeValid)
            {
                return View(new ViewModel() { ErrorView = new() { AdditionalMessage = data.PasswordChangeError} });
            }

            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(ApiEndpoints.Account.SendPasswordChangeRequest, content);

            if(!response.IsSuccessStatusCode)
            {
                string message = await GetMessageFromResponseAsync(response);
                _logger.LogError("Change password account API request failed with status code {StatusCode}. Response message: {ResponseMessage}", response.StatusCode, message);

                return View(new ViewModel()
                {
                    ErrorView = new()
                    {
                        AdditionalMessage = string.IsNullOrWhiteSpace(message) ?
                        "Change password account API request failed. Please try again latter or contact the support team." : message
                    }
                });
            }

            _memoryCache.Remove("userToken");
            return LoginPage(new ViewModel() { AccountView = new() { RegistrationMessage = "Password changed. Please sign in with new password." } });
        }
        _memoryCache.Remove("userToken");
        return LoginPage(new ViewModel() { AccountView = new() { RegistrationMessage = "Your session cookie has expired. Please sign in." } });
    }

    /// <summary>
    /// Changes the login page.
    /// </summary>
    /// <returns></returns>
    public IActionResult ChangeLoginPage() => View("ChangeLogin");

    /// <summary>
    /// Changes the login asynchronous.
    /// </summary>
    /// <param name="newLogin">The new login.</param>
    /// <param name="accountPassword">The account password.</param>
    /// <returns></returns>
    public async Task<IActionResult> ChangeLoginAsync(string newLogin, string accountPassword)
    {
        if (IsTokenFromCasheValid(out (string token, string expiry, string userName, string email) userCache))
        {
            AccountUpdateDto data = new()
            {
                UserNameOrEmail = userCache.email,
                Password = accountPassword,
                UpdatedLogin = newLogin,
                UpdatedEmailAddress = string.Empty,
                ConfirmUpdatedEmailAddress = string.Empty
            };

            if(!data.IsLoginChangeValid || !data.IsValid)
            {
                return View(new ViewModel() { ErrorView = new() { AdditionalMessage = data.LoginChangeValidationError } });
            }

            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(ApiEndpoints.Account.SendUpdateAccountRequest, content);

            if (!response.IsSuccessStatusCode)
            {
                string message = await GetMessageFromResponseAsync(response);
                _logger.LogError("Change login account API request failed with status code {StatusCode}. Response message: {ResponseMessage}", response.StatusCode, message);

                return View(new ViewModel()
                {
                    ErrorView = new()
                    {
                        AdditionalMessage = string.IsNullOrWhiteSpace(message) ?
                        "Change login account API request failed. Please try again latter or contact the support team." : message
                    }
                });
            }
            _memoryCache.Remove("userToken");
            return LoginPage(new ViewModel() { AccountView = new() { RegistrationMessage = "Login changed. Please sign in with new login." } });
        }
        _memoryCache.Remove("userToken");
        return LoginPage(new ViewModel() { AccountView = new() { RegistrationMessage = "Your session cookie has expired. Please sign in." } });
    }

    /// <summary>
    /// Changes the email page.
    /// </summary>
    /// <returns></returns>
    public IActionResult ChangeEmailPage() => View("ChangeEmail");

    /// <summary>
    /// Changes the email asynchronous.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <param name="email">The email.</param>
    /// <param name="confirmEmail">The confirm email.</param>
    /// <returns></returns>
    public async Task<IActionResult> ChangeEmailAsync(string password, string email, string confirmEmail)
    {
        if (IsTokenFromCasheValid(out (string token, string expiry, string userName, string email) userCache))
        {
            AccountUpdateDto data = new()
            {
                UserNameOrEmail = userCache.email,
                Password = password,
                UpdatedLogin = string.Empty,
                UpdatedEmailAddress = email,
                ConfirmUpdatedEmailAddress = confirmEmail
            };

            if (!data.IsLoginChangeValid || !data.IsValid)
            {
                return View(new ViewModel() { ErrorView = new() { AdditionalMessage = data.LoginChangeValidationError } });
            }

            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(ApiEndpoints.Account.SendUpdateAccountRequest, content);

            if (!response.IsSuccessStatusCode)
            {
                string message = await GetMessageFromResponseAsync(response);
                _logger.LogError("Change email account API request failed with status code {StatusCode}. Response message: {ResponseMessage}", response.StatusCode, message);

                return View(new ViewModel()
                {
                    ErrorView = new()
                    {
                        AdditionalMessage = string.IsNullOrWhiteSpace(message) ?
                        "Change email account API request failed. Please try again latter or contact the support team." : message
                    }
                });
            }
            string information = email.Equals("user@msichova.com", StringComparison.OrdinalIgnoreCase) ||
            email.Equals("admin@msichova.com", StringComparison.OrdinalIgnoreCase) ?
            await GetMessageFromResponseAsync(response) :
            "Email changed. Please confirm new email and sign in with new email.";

            _memoryCache.Remove("userToken");
            return LoginPage(new ViewModel() { AccountView = new() { RegistrationMessage = information } });
        }
        _memoryCache.Remove("userToken");
        return LoginPage(new ViewModel() { AccountView = new() { RegistrationMessage = "Your session cookie has expired. Please sign in." } });
    }
    #region Helper Methods

    /// <summary>
    /// Deserializes the response asynchronous.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns></returns>
    /// <exception cref="System.Text.Json.JsonException">Deserialized response is null.</exception>
    private async Task<Dictionary<string, string>> DeserializeResponseAsync(HttpResponseMessage response)
    {
        var responseString = await response.Content.ReadAsStringAsync();

        // Deserialize response and handle possible exceptions
        Dictionary<string, string> responseData;
        try
        {
            responseData = JsonSerializer.Deserialize<Dictionary<string, string>>(responseString) ??
                throw new JsonException("Deserialized response is null.");
        }
        catch (JsonException ex)
        {
            _logger.LogError("JSON deserialization error: {Message}", ex.Message);
            return [];
        }
        return responseData;
    }

    /// <summary>
    /// Asynchronously stores the token from the response in IMemoryCache.
    /// </summary>
    /// <param name="response">The response containing the token and its data.</param>
    /// <param name="cacheKey">The cache key under which the token and its data will be stored.</param>
    /// <returns>
    /// <see langword="true"/> if the token and expiration date are present, not expired, 
    /// and there are no JSON deserialization errors; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the deserialized response is null.</exception>
    private async Task<bool> StoreTokenFromResponseAsync(HttpResponseMessage response, string cacheKey)
    {
        // Deserialize response and handle possible exceptions
        Dictionary<string, string> responseData = await DeserializeResponseAsync(response);

        // Ensure that token and expiration date and time are present
        if (responseData is not null &&
            responseData.TryGetValue("token", out string? token) &&
            responseData.TryGetValue("expiration", out string? expiration) &&
            responseData.TryGetValue("user", out string? user) && 
            responseData.TryGetValue("email", out string? email))
        {
            _memoryCache.Set(cacheKey, (token, expiration, user, email));
            return true;
        }

        // Fallback for any unexpected response structure
        _logger.LogError("Failed to retrieve required data from the login response.");
        return false;
    }

    /// <summary>
    /// Determines whether [is token from cashe valid] [the specified cache].
    /// </summary>
    /// <param name="cache">The cache.</param>
    /// <returns>
    ///   <c>true</c> if [is token from cashe valid] [the specified cache]; otherwise, <c>false</c>.
    /// </returns>
    private bool IsTokenFromCasheValid(out (string token, string expiry, string userName, string email) cache)
    {
        if(_memoryCache.TryGetValue("userToken", out (string token, string expiry, string userName, string email) cacheData))
        {
            if(!string.IsNullOrWhiteSpace(cacheData.token) && 
                !string.IsNullOrWhiteSpace(cacheData.expiry) && 
                DateTime.TryParse(cacheData.expiry, out DateTime expiryTokenTime))
            {
                cache = cacheData;
                return expiryTokenTime >= DateTime.Now;
            }
            
        }
        cache = cacheData;
        return false;
    }

    /// <summary>
    /// Gets the message from response asynchronous.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns></returns>
    private async Task<string> GetMessageFromResponseAsync(HttpResponseMessage response)
    {
        Dictionary<string, string> responseData = await DeserializeResponseAsync(response);
        string toReturn = "";
        if(responseData is not null)
        {
            if (responseData.ContainsKey("message") &&
                responseData.TryGetValue("message", out string? message))
            {
                toReturn += message;
            }
            if(responseData.ContainsKey("body") &&
                responseData.TryGetValue("body", out string? body))
            {
                toReturn += "\n" + body;
            }
            if(!responseData.ContainsKey("body") && !responseData.ContainsKey("message"))
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        
        return toReturn;
    }

    /// <summary>
    /// Base data transfer object for authentication requests, 
    /// containing essential properties and validation for user identification and credentials.
    /// </summary>
    private abstract record AuthRequestBaseDto
    {
        /// <summary>
        /// Gets or sets the user name or email.
        /// </summary>
        /// <value>
        /// The user name or email.
        /// </value>
        public string? UserNameOrEmail { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        /// <value>
        /// The password.
        /// </value>
        public string? Password { get; set; }

        /// <summary>
        /// Indicates whether the request is valid by ensuring 
        /// both <see cref="UserNameOrEmail"/> and <see cref="Password"/> are not empty.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
        /// </value>
        public virtual bool IsValid => 
            !string.IsNullOrWhiteSpace(UserNameOrEmail) && 
            !string.IsNullOrWhiteSpace(Password);

        /// <summary>
        /// Gets the validation error if <see cref="IsValid"/> is false.
        /// </summary>
        /// <value>
        /// The validation error.
        /// </value>
        public virtual string ValidationError =>
            string.IsNullOrWhiteSpace(UserNameOrEmail) ? "User name or email is required." :
            string.IsNullOrWhiteSpace(Password) ? "Password is required." :
            string.Empty;

    }

    /// <summary>
    /// Data transfer object for login requests, inheriting validation from <see cref="AuthRequestBaseDto"/>.
    /// </summary>
    private record LoginDto : AuthRequestBaseDto
    {
        public override bool IsValid => base.IsValid;

        public override string ValidationError => base.ValidationError;
    }

    /// <summary>
    /// Data transfer object for user registration requests, with additional properties for email and password confirmation.
    /// </summary>
    private record RegisterDto : AuthRequestBaseDto
    {
        /// <summary>
        /// Gets or sets the confirm password.
        /// </summary>
        /// <value>
        /// The confirm password.
        /// </value>
        public string? ConfirmPassword { get; set; }

        /// <summary>
        /// Gets or sets the email address.
        /// </summary>
        /// <value>
        /// The email address.
        /// </value>
        public string? EmailAddress { get; set; }
        public string? ConfirmEmailAddress { get; set; }

        /// <summary>
        /// Gets a value indicating whether [are passwords valid].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [are passwords valid]; otherwise, <c>false</c>.
        /// </value>
        private bool ArePasswordsValid => 
            !string.IsNullOrWhiteSpace(Password) &&
            !string.IsNullOrWhiteSpace(ConfirmPassword) &&
            Password.Equals(ConfirmPassword);

        /// <summary>
        /// Gets a value indicating whether [are emails valid].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [are emails valid]; otherwise, <c>false</c>.
        /// </value>
        private bool AreEmailsValid => 
            !string.IsNullOrWhiteSpace(EmailAddress) &&
            !string.IsNullOrWhiteSpace(ConfirmEmailAddress) &&
            EmailAddress.Equals(ConfirmEmailAddress);

        public override bool IsValid => base.IsValid && ArePasswordsValid && AreEmailsValid;

        public override string ValidationError => 
            !base.IsValid ? base.ValidationError :
            !ArePasswordsValid ? "Passwords must match and cannot be empty." :
            !AreEmailsValid ? "Emails must match and cannot be empty." :
            string.Empty;
    }

    /// <summary>
    /// Data transfer object for account deletion requests, extending login properties with an additional confirmation flag.
    /// </summary>
    private record DeleteAccountDto : LoginDto
    {
        /// <summary>
        /// Gets or sets a value indicating whether this instance is confirmed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is confirmed; otherwise, <c>false</c>.
        /// </value>
        public bool IsConfirmed { get; set; }
    }

    /// <summary>
    /// Data transfer object for password change requests, including validation for new password requirements.
    /// </summary>
    private record PasswordChangeDto : LoginDto
    {
        /// <summary>
        /// Creates new userpassword.
        /// </summary>
        /// <value>
        /// The new user password.
        /// </value>
        public string? NewUserPassword { get; set; }

        /// <summary>
        /// Gets or sets the confirm new user password.
        /// </summary>
        /// <value>
        /// The confirm new user password.
        /// </value>
        public string? ConfirmNewUserPassword { get; set; }

        /// <summary>
        /// Indicates whether the password change request is valid, ensuring new passwords match 
        /// and differ from the existing password.
        /// </summary>
        public bool IsPasswordChangeValid => 
            base.IsValid &&
            !string.IsNullOrWhiteSpace(NewUserPassword) &&
            !string.IsNullOrWhiteSpace(ConfirmNewUserPassword) &&
            NewUserPassword.Equals(ConfirmNewUserPassword) &&
            !Password!.Equals(NewUserPassword);

        /// <summary>
        /// Provides a validation error message specific to the password change request if <see cref="IsPasswordChangeValid"/> is false.
        /// </summary>
        public string PasswordChangeError =>
            base.IsValid ? base.ValidationError :
            !IsPasswordChangeValid ?
            (string.IsNullOrWhiteSpace(NewUserPassword) ? "New password is required." :
            Password!.Equals(NewUserPassword) ? "New password must be different from the current password." :
            "New password and confirmation password must match.") :
            string.Empty;
    }

    /// <summary>
    /// Data transfer object for updating account information, 
    /// allowing login or email to be changed with necessary validation.
    /// </summary>
    private record AccountUpdateDto : LoginDto
    {
        /// <summary>
        /// Gets or sets the updated login.
        /// </summary>
        /// <value>
        /// The updated login.
        /// </value>
        public string? UpdatedLogin {  get; set; }

        /// <summary>
        /// Gets or sets the updated email address.
        /// </summary>
        /// <value>
        /// The updated email address.
        /// </value>
        public string? UpdatedEmailAddress { get; set; }

        /// <summary>
        /// Gets or sets the confirm updated email address.
        /// </summary>
        /// <value>
        /// The confirm updated email address.
        /// </value>
        public string? ConfirmUpdatedEmailAddress { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is login change valid.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is login change valid; otherwise, <c>false</c>.
        /// </value>
        public bool IsLoginChangeValid =>
            !string.IsNullOrWhiteSpace(UpdatedLogin) ||
            (!string.IsNullOrWhiteSpace(UpdatedEmailAddress) &&
            !string.IsNullOrWhiteSpace(ConfirmUpdatedEmailAddress) &&
            UpdatedEmailAddress.Equals(ConfirmUpdatedEmailAddress));

        /// <summary>
        /// Gets the login change validation error.
        /// </summary>
        /// <value>
        /// The login change validation error.
        /// </value>
        public string LoginChangeValidationError =>
            !IsValid ? base.ValidationError :
            !IsLoginChangeValid ?
            (string.IsNullOrWhiteSpace(UpdatedLogin) && string.IsNullOrWhiteSpace(UpdatedEmailAddress) ? "New login or email is required." :
            "New email and confirmation email must match.") :
            string.Empty;
    }
    #endregion Helper Methods
}
