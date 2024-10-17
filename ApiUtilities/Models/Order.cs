using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiUtilities.Models;

/// <summary>
/// Represents an order made by a user.
/// </summary>
[Table("Orders")]
[PrimaryKey("OrderId")]
public class Order
{
    /// <summary>
    /// Unique identifier for the order, generated as a GUID.
    /// </summary>
    public string OrderId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Foreign key that references the primary key (Id) of the ApiUser.
    /// Links the order to the user who created it.
    /// </summary>
    [ForeignKey(nameof(User))]
    public string UserId { get; set; } = string.Empty!;

    /// <summary>
    /// Navigation property to access the user who placed the order.
    /// Ensures the user who created the order is linked.
    /// </summary>
    public ApiUser User { get; set; } = null!;

    /// <summary>
    /// A list of products added to the order.
    /// </summary>
    public List<string>? ProductsAddedToOrder { get; set; } = [];

    /// <summary>
    /// The total price of the order, with a precision of 18 digits and 2 decimal places.
    /// </summary>
    [Precision(18, 2)]
    public decimal? TotalPrice { get; set; }

    /// <summary>
    /// Indicates whether the order has been submitted.
    /// </summary>
    public bool IsOrderSubmitted { get; set; }
}
