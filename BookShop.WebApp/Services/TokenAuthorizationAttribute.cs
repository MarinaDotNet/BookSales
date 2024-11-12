
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace BookShop.WebApp.Services;

/// <summary>
/// A custom authorization attribute for methods that checks if the current user is authorized to access the data.
/// </summary>
/// <remarks>
/// This attribute performs authorization by verifying the availability and validity of the token data stored in the cache using the specified
/// <paramref name="cacheKey"/>. 
/// It checks whether the token exists, and if so, validates its expiration date and time to ensure the token is still valid.
/// If the current UTC time is earlier than or equal to the token's expiration time, the method proceeds normally and authorization is 
/// granted (i.e., the method returns without further action). 
/// Otherwise, if the token has expired, an unauthorized result is returned, preventing access to the requested resource.
/// </remarks>
/// <param name="cacheKey">
/// The specified cache key used to look up token data in the cache.
/// </param>
[AttributeUsage(AttributeTargets.Method)]
public class TokenAuthorizationAttribute(string cacheKey) : Attribute, IAuthorizationFilter

{
    private readonly string _cacheKey = cacheKey ??
        throw new ArgumentNullException(nameof(cacheKey), "The 'cacheKey' cannot be null.");

    /// <summary>
    /// Called early in the filter pipeline to confirm request is authorized.
    /// </summary>
    /// <param name="context">
    /// The <see cref="T:Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext">AuthorizationFilterContext</see>.
    /// </param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var memoryCache = (IMemoryCache)context.HttpContext.RequestServices.GetService(typeof(IMemoryCache))!;

        if (memoryCache.TryGetValue(_cacheKey, out (string token, string expiry, string userName, string email) cachedValue))
        {
            DateTime expires = DateTime.Parse(cachedValue.expiry).ToUniversalTime();

            if (DateTime.UtcNow <= expires)
            {
                return;
            }
        }

        context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
    }
}
