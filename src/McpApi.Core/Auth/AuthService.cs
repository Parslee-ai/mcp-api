using System.Security.Cryptography;
using McpApi.Core.Models;
using McpApi.Core.Notifications;
using McpApi.Core.Storage;

namespace McpApi.Core.Auth;

/// <summary>
/// Implementation of authentication operations.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserStore _userStore;
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly string _baseUrl;

    private const int EmailTokenExpiryHours = 24;
    private const int PhoneCodeExpiryMinutes = 10;

    public AuthService(
        IUserStore userStore,
        IEmailService emailService,
        ISmsService smsService,
        string baseUrl)
    {
        _userStore = userStore;
        _emailService = emailService;
        _smsService = smsService;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        // Validate email format
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return new AuthResult(false, "Invalid email address.");
        }

        // Validate password strength
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return new AuthResult(false, "Password must be at least 8 characters.");
        }

        // Check if email already exists
        if (await _userStore.EmailExistsAsync(email, cancellationToken))
        {
            return new AuthResult(false, "An account with this email already exists.");
        }

        // Create user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = email.ToLowerInvariant(),
            PasswordHash = PasswordHasher.HashPassword(password),
            EmailVerified = false,
            PhoneVerified = false,
            EmailVerificationToken = GenerateSecureToken(),
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(EmailTokenExpiryHours),
            EncryptionKeySalt = GenerateSecureToken(),
            CreatedAt = DateTime.UtcNow
        };

        await _userStore.UpsertAsync(user, cancellationToken);

        // Send verification email
        var verifyUrl = $"{_baseUrl}/auth/verify-email?token={user.EmailVerificationToken}";
        await _emailService.SendEmailAsync(
            user.Email,
            "Verify your MCP-API account",
            $"Click the link to verify your email: {verifyUrl}",
            $"<p>Click the link below to verify your email:</p><p><a href=\"{verifyUrl}\">Verify Email</a></p>",
            cancellationToken);

        return new AuthResult(true, User: user);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userStore.GetByEmailAsync(email, cancellationToken);
        if (user == null)
        {
            return new AuthResult(false, "Invalid email or password.");
        }

        if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
        {
            return new AuthResult(false, "Invalid email or password.");
        }

        if (!user.EmailVerified)
        {
            return new AuthResult(false, "Please verify your email before logging in.");
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userStore.UpsertAsync(user, cancellationToken);

        return new AuthResult(true, User: user);
    }

    public async Task<AuthResult> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var user = await _userStore.GetByEmailVerificationTokenAsync(token, cancellationToken);
        if (user == null)
        {
            return new AuthResult(false, "Invalid or expired verification token.");
        }

        if (user.EmailVerificationTokenExpiry < DateTime.UtcNow)
        {
            return new AuthResult(false, "Verification token has expired. Please request a new one.");
        }

        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiry = null;

        await _userStore.UpsertAsync(user, cancellationToken);

        return new AuthResult(true, User: user);
    }

    public async Task<AuthResult> ResendEmailVerificationAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userStore.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return new AuthResult(false, "User not found.");
        }

        if (user.EmailVerified)
        {
            return new AuthResult(false, "Email is already verified.");
        }

        user.EmailVerificationToken = GenerateSecureToken();
        user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(EmailTokenExpiryHours);

        await _userStore.UpsertAsync(user, cancellationToken);

        var verifyUrl = $"{_baseUrl}/auth/verify-email?token={user.EmailVerificationToken}";
        await _emailService.SendEmailAsync(
            user.Email,
            "Verify your MCP-API account",
            $"Click the link to verify your email: {verifyUrl}",
            $"<p>Click the link below to verify your email:</p><p><a href=\"{verifyUrl}\">Verify Email</a></p>",
            cancellationToken);

        return new AuthResult(true, User: user);
    }

    public async Task<AuthResult> SetPhoneNumberAsync(string userId, string phoneNumber, CancellationToken cancellationToken = default)
    {
        var user = await _userStore.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return new AuthResult(false, "User not found.");
        }

        // Generate 6-digit code
        var code = GeneratePhoneCode();

        user.PhoneNumber = phoneNumber;
        user.PhoneVerified = false;
        user.PhoneVerificationCode = code;
        user.PhoneVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(PhoneCodeExpiryMinutes);

        await _userStore.UpsertAsync(user, cancellationToken);

        // Send SMS
        await _smsService.SendSmsAsync(
            phoneNumber,
            $"Your MCP-API verification code is: {code}",
            cancellationToken);

        return new AuthResult(true, User: user);
    }

    public async Task<AuthResult> VerifyPhoneAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _userStore.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return new AuthResult(false, "User not found.");
        }

        if (user.PhoneVerificationCode != code)
        {
            return new AuthResult(false, "Invalid verification code.");
        }

        if (user.PhoneVerificationCodeExpiry < DateTime.UtcNow)
        {
            return new AuthResult(false, "Verification code has expired. Please request a new one.");
        }

        user.PhoneVerified = true;
        user.PhoneVerificationCode = null;
        user.PhoneVerificationCodeExpiry = null;

        await _userStore.UpsertAsync(user, cancellationToken);

        return new AuthResult(true, User: user);
    }

    public async Task<User?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _userStore.GetByIdAsync(userId, cancellationToken);
    }

    public async Task<AuthResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userStore.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return new AuthResult(false, "User not found.");
        }

        if (!PasswordHasher.VerifyPassword(currentPassword, user.PasswordHash))
        {
            return new AuthResult(false, "Current password is incorrect.");
        }

        if (newPassword.Length < 8)
        {
            return new AuthResult(false, "New password must be at least 8 characters.");
        }

        user.PasswordHash = PasswordHasher.HashPassword(newPassword);
        await _userStore.UpsertAsync(user, cancellationToken);

        return new AuthResult(true, User: user);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string GeneratePhoneCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var code = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
        return code.ToString("D6");
    }
}
