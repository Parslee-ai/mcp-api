using Azure;
using Azure.Communication.Email;

namespace McpApi.Core.Notifications;

/// <summary>
/// Azure Communication Services implementation of email sending.
/// </summary>
public class AcsEmailService : IEmailService
{
    private readonly EmailClient _emailClient;
    private readonly string _senderAddress;

    public AcsEmailService(string connectionString, string senderAddress)
    {
        _emailClient = new EmailClient(connectionString);
        _senderAddress = senderAddress;
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string plainTextContent,
        string htmlContent,
        CancellationToken cancellationToken = default)
    {
        var emailMessage = new EmailMessage(
            senderAddress: _senderAddress,
            content: new EmailContent(subject)
            {
                PlainText = plainTextContent,
                Html = htmlContent
            },
            recipients: new EmailRecipients(new List<EmailAddress>
            {
                new EmailAddress(to)
            }));

        await _emailClient.SendAsync(WaitUntil.Started, emailMessage, cancellationToken);
    }
}
