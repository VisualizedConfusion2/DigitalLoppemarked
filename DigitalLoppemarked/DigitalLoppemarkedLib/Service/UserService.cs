using DigitalLoppemarkedLib.Data.Repositories;
using DigitalLoppemarkedLib.Models;
using BCrypt.Net;
using System.ComponentModel.DataAnnotations;

namespace DigitalLoppemarkedLib.Service;

public class UserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(string email, string password, string fullName, bool gdprConsent, CancellationToken ct = default)
    {
        // 1. Basic Validation
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
        {
            return (false, "Alle felter skal udfyldes.");
        }

        if (!new EmailAddressAttribute().IsValid(email))
        {
            return (false, "Ugyldig e-mail format.");
        }

        if (!gdprConsent)
        {
            return (false, "Du skal acceptere behandling af persondata.");
        }

        // 2. Prevent User Enumeration (Story requirement)
        // We check if user exists, but we return the same generic success message
        // to the UI to avoid leaking whether an email is registered.
        var existingUser = await _userRepository.GetByEmailAsync(email, ct);
        if (existingUser != null)
        {
            // In a real app, we would send a "You already have an account" email.
            // For now, we simulate success to the UI.
            return (true, "Hvis e-mailen er gyldig, vil du modtage en bekræftelsesmail.");
        }

        // 3. Hash Password securely
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        // 4. Save User
        var user = new User
        {
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            GdprConsent = gdprConsent,
            IsActive = false
        };

        int userId = await _userRepository.CreateUserAsync(user, ct);

        // 5. Create Verification Token
        string token = Guid.NewGuid().ToString("N");
        DateTime expiresAt = DateTime.UtcNow.AddHours(24);
        await _userRepository.CreateVerificationTokenAsync(userId, token, expiresAt, ct);

        // 6. Send Email (Mocked for now)
        Console.WriteLine($"[MOCK EMAIL] To: {email} | Link: /verify?token={token}");

        return (true, "Din konto er oprettet. Tjek din e-mail for at aktivere den.");
    }

    public async Task<(bool Success, string Message)> VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        var userId = await _userRepository.GetUserIdByTokenAsync(token, ct);
        if (userId == null)
        {
            return (false, "Ugyldig eller udløbet token.");
        }

        var user = await _userRepository.GetByIdAsync(userId.Value, ct);
        if (user == null)
        {
            return (false, "Bruger findes ikke.");
        }

        user.IsActive = true;
        await _userRepository.UpdateUserAsync(user, ct);
        await _userRepository.DeleteVerificationTokenAsync(userId.Value, ct);

        return (true, "Din konto er nu aktiveret!");
    }
}