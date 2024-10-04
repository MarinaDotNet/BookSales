using AuthAccount.API.Constants;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AuthAccount.API.Models.Account;

/// <summary>
/// Represents a model for user login, inheriting from the <see cref="AccountModel"/> class.
/// </summary>
/// <remarks>
/// This model is designed to hold the necessary data for user authentication,
/// such as username or email and password. It can be extended with additional 
/// properties or validation as needed.
/// </remarks>
public partial class LoginModel : AccountModel
{

}
