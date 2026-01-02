using Azure.Communication.Sms;

namespace McpApi.Core.Notifications;

/// <summary>
/// Azure Communication Services implementation of SMS sending.
/// </summary>
public class AcsSmsService : ISmsService
{
    private readonly SmsClient _smsClient;
    private readonly string _senderPhoneNumber;

    public AcsSmsService(string connectionString, string senderPhoneNumber)
    {
        _smsClient = new SmsClient(connectionString);
        _senderPhoneNumber = senderPhoneNumber;
    }

    public async Task SendSmsAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        await _smsClient.SendAsync(
            from: _senderPhoneNumber,
            to: phoneNumber,
            message: message,
            cancellationToken: cancellationToken);
    }
}
