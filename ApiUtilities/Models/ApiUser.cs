using Microsoft.AspNetCore.Identity;

namespace ApiUtilities.Models;

/// <summary>
/// Represents an API user that inherits from the IdentityUser class.
/// Contains a collection of associated orders.
/// </summary>
public class ApiUser : IdentityUser
{
    /// <summary>
    /// Navigation property for the collection of orders associated with the user.
    /// This is a one-to-many relationship where a user can have multiple orders.
    /// </summary>
    public virtual ICollection<Order>? Orders { get; set; } = [];
}
