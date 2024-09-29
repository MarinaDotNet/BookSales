namespace AuthAccount.API.Constants;

/// <summary>
/// Provides constant values related to security configurations for authentication and token management.
/// </summary>
public static class SecurityConstants
{
    public const string AuthApiKey = "AuthApiKey";
    public const string TokenDataKey = "JWT";
    public const string TokenValidIssuerKey = "ValidIssuer";
    public const string TokenValidAudienceKey = "ValidAudience";
    public const string TokenAdminSecretKey = "SecretAdmin";
    public const string TokenUserSecretKey = "SecretUser";
}
