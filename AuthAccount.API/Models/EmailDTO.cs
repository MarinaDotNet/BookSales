namespace AuthAccount.API.Models;

/// <summary>
/// A data transfer object (DTO) representing an email information.
/// </summary>
/// <remarks>
/// This DTO is used for passing email data between the API and the client.
/// </remarks>
public record EmailDTO
{
    /// <summary>
    /// Gets or sets the email address for recipient/client, to whom the email  should be send.
    /// </summary>
    /// <value>The recipient email address as a non-empty string.</value>
    public string Email { get; set; } = string.Empty!;

    /// <summary>
    /// Gets or sets the email's subject.
    /// </summary>
    /// <value>The email's subject as a non-empty string.</value>
    public string Subject { get; set; } = string.Empty!;

    /// <summary>
    /// Gets or sets the email's body.
    /// </summary>
    /// <value>The email's body as a non-empty string.</value>
    public string Body { get; set; } = string.Empty!;

    /// <summary>
    /// Gets or sets the email's text message.
    /// </summary>
    /// <value>The email's text message can be an empty string if the text message is not specified.</value>
    public string? TextMessage { get; set; }
}
