namespace BookShop.WebApp.Models;

/// <summary>
/// Represents an error view model containing information about a request 
/// and any additional error messages for displaying in views.
/// </summary>
public class ErrorViewModel
{
    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    /// <value>
    /// The request identifier.
    /// </value>
    public string? RequestId { get; set; }

    /// <summary>
    /// Gets a value indicating whether [show request identifier].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [show request identifier]; otherwise, <c>false</c>.
    /// </value>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>
    /// Gets or sets the additional message.
    /// </summary>
    /// <value>
    /// The additional message.
    /// </value>
    public string? AdditionalMessage { get; set; }

    /// <summary>
    /// Gets a value indicating whether this instance is error view model is empty.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is error view model is empty; otherwise, <c>false</c>.
    /// </value>
    public bool IsErrorViewModelIsEmpty => string.IsNullOrEmpty(AdditionalMessage);
}
