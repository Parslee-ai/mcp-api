namespace McpApi.Core.Notifications;

/// <summary>
/// Interface for sending SMS notifications.
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Sends an SMS to the specified phone number.
    /// </summary>
    Task SendSmsAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default);
}
