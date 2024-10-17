using AuthAccount.API.Constants;
using DnsClient;
using Microsoft.AspNetCore.Identity.UI.Services;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net.Sockets;

namespace AuthAccount.API.Services;

/// <summary>
/// Sends the email to the recipient with email confirmation and account updation information.
/// Utilises the Twilio SendGrid services to send the email.
/// </summary>
/// <param name="configuration">
/// An instance of <see cref="IConfiguration"/> used to retrieve the SendGrid API key from the configuration settings.
/// </param>
/// <param name="logger">Logs activity and errors related to send email operations.</param>
public class EmailSender(IConfiguration configuration, ILogger<EmailSender> logger) : IEmailSender
{
    private readonly IConfiguration _configuration = configuration ?? 
        throw new ArgumentNullException(nameof(configuration), "The configuration settings cannot be null.");

    private readonly ILogger<EmailSender> _logger = logger ??
        throw new ArgumentNullException(nameof(logger), "The logger cannot be null.");

    /// <summary>
    /// Asynchronously sends the email to the recipient.
    /// </summary>
    /// <param name="email">The email address of the recipient.</param>
    /// <param name="subject">The email subject.</param>
    /// <param name="htmlMessage">The body/content of the email in HTML format.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("The receipent email address should not be null or empty");
            throw new ArgumentNullException(nameof(email), "The email cannot be null or empty.");
        }

        //Checking email address if it is default email, then it is impossible to send to it email
        if (email.Equals(_configuration[AuthConstants.UserEmailKey], StringComparison.OrdinalIgnoreCase) ||
            email.Equals(_configuration[AuthConstants.AdminEmailKey], StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Sending emails to API default system accounts is not allowed.");
            return Task.FromException(new InvalidOperationException("Sending emails to API default system accounts is not allowed."));
        } 

        try
        {
            //Check if email address has a valid host name by checking MX records.
            string hostName = email.Split('@')[1];
            var lookUp = new LookupClient();
            var mxRecords = lookUp.Query(hostName, QueryType.MX);
            if (!mxRecords.Answers.MxRecords().Any())
            {
                _logger.LogWarning("The requested recipient email has incorrect host name: {hostName}.", hostName);
                return Task.FromException(new SocketException((int)SocketError.HostNotFound));
            }

            //Setting and send email by Twilio Send Grid
            var client = new SendGridClient(_configuration[SendGridConstants.SendGridKey]);
            var message = new SendGridMessage()
            {
                From = new EmailAddress(_configuration[SendGridConstants.SendGridEmailKey], "Books Stock team"),
                Subject = subject,
                PlainTextContent = htmlMessage,
                HtmlContent = $"<span>{htmlMessage}</span>"
            };

            message.AddTo(email);

            return client.SendEmailAsync(message).ContinueWith(task =>
            {
                if(task.IsCompletedSuccessfully)
                {
                    _logger.LogInformation("Email sent to {email} successfully.", email);
                }
                else
                {
                    _logger.LogError("Failed to send email: {task.Exception}", task.Exception?.GetBaseException().Message);
                }
            });
            
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {Error}", ex.Message);
            return Task.FromException(ex);
        }
    }
}
