namespace McpApi.Core.Notifications;

/// <summary>
/// Interface for sending email notifications.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email to the specified recipient.
    /// </summary>
    Task SendEmailAsync(
        string to,
        string subject,
        string plainTextContent,
        string htmlContent,
        CancellationToken cancellationToken = default);
}
