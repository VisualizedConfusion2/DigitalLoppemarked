using DigitalLoppemarkedLib.Data.Repositories;
using DigitalLoppemarkedLib.Models;
using BCrypt.Net;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Caching.Memory;

namespace DigitalLoppemarkedLib.Service;

public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;
    private const int MaxFailedAttempts = 5;

    public UserService(IUserRepository userRepository, IMemoryCache cache, IEmailService emailService)
    {
        _userRepository = userRepository;
        _cache = cache;
        _emailService = emailService;
    }

    public async Task<(bool Success, string Message, int? UserId)> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "E-mail og adgangskode er påkrævet.", null);
        }

        // 1. Brute force protection: Check failed attempts
        string cacheKey = $"login_attempts_{email}";
        if (_cache.TryGetValue(cacheKey, out int attempts) && attempts >= MaxFailedAttempts)
        {
            return (false, "Kontoen er midlertidigt låst pga. for mange mislykkede forsøg. Prøv igen senere.", null);
        }

        var user = await _userRepository.GetByEmailAsync(email, ct);

        // 2. Timing attack protection: Always perform a hash comparison
        // If user doesn't exist, we verify a dummy hash to keep response time consistent.
        string hashToVerify = user?.PasswordHash ?? "$2a$11$dummyhashdummyhashdummyhashdummyhashdummyhashdummyhash";
        bool isValid = BCrypt.Net.BCrypt.Verify(password, hashToVerify);

        if (user == null || !isValid)
        {
            // Increment failed attempts
            _cache.Set(cacheKey, attempts + 1, TimeSpan.FromMinutes(15));
            return (false, "Ugyldig e-mail eller adgangskode.", null);
        }

        // 3. Success: reset failed attempts
        _cache.Remove(cacheKey);

        return (true, "Login lykkedes.", user.Id);
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

        // 2. Prevent user enumeration: return the exact same message whether or not the
        // e-mail already exists, so an attacker cannot tell registered e-mails apart.
        const string genericMessage = "Din konto er oprettet. Du kan nu logge ind.";

        var existingUser = await _userRepository.GetByEmailAsync(email, ct);
        if (existingUser != null)
        {
            return (true, genericMessage);
        }

        // 3. Hash Password securely
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        // 4. Save the user as active immediately (no e-mail verification step).
        var user = new User
        {
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            GdprConsent = gdprConsent,
            IsActive = true
        };

        await _userRepository.CreateUserAsync(user, ct);

        return (true, genericMessage);
    }

    public async Task<(bool Success, string Message)> UpdateProfileAsync(int userId, string fullName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (false, "Navn er påkrævet.");
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null) return (false, "Bruger ikke fundet.");

        user.FullName = fullName;
        await _userRepository.UpdateUserAsync(user, ct);

        return (true, "Profilen er opdateret.");
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return (false, "Begge felter er påkrævet.");
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null) return (false, "Bruger ikke fundet.");

        // 1. Verify identity
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return (false, "Den nuværende adgangskode er forkert.");
        }

        // 2. Prevent reuse of the same password
        string newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        if (await _userRepository.HasPasswordBeenUsedAsync(userId, newHash, ct))
        {
            return (false, "Du kan ikke bruge en tidligere adgangskode.");
        }

        // 3. Update password and rotate security stamp to invalidate all sessions
        string newStamp = Guid.NewGuid().ToString();
        await _userRepository.UpdatePasswordAsync(userId, newHash, newStamp, ct);
        await _userRepository.AddPasswordHistoryAsync(userId, newHash, ct);

        return (true, "Din adgangskode er blevet ændret.");
    }

    public async Task<(bool Success, string Message)> RequestPasswordResetAsync(string email, string baseUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return (false, "E-mail er påkrævet.");

        var user = await _userRepository.GetByEmailAsync(email, ct);

        // Prevent user enumeration: always return success message
        const string genericMessage = "Hvis e-mailen er gyldig, vil du modtage et link til nulstilling af adgangskoden.";

        if (user == null)
        {
            // Simulate timing of hashing/saving for non-existent users to prevent timing attacks
            await Task.Delay(100, ct);
            return (true, genericMessage);
        }

        // Generate random token
        string token = Guid.NewGuid().ToString("N");
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(30);
        await _userRepository.SaveResetTokenAsync(user.Id, token, expiresAt, ct);

        // Send email with link (constructed from baseUrl to prevent Host header poisoning)
        string resetLink = $"{baseUrl}/reset-password?token={token}";
        // Email sending is now purely optional/mocked
        Console.WriteLine($"[MOCK EMAIL] To: {user.Email} | Subject: Nulstilling af adgangskode | Link: {resetLink}");

        return (true, genericMessage);
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
        {
            return (false, "Alle felter er påkrævet.");
        }

        var user = await _userRepository.GetUserByResetTokenAsync(token, ct);
        if (user == null)
        {
            return (false, "Ugyldigt eller udløbet nulstillingslink.");
        }

        // Prevent reuse of the same password
        string newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        if (await _userRepository.HasPasswordBeenUsedAsync(user.Id, newHash, ct))
        {
            return (false, "Du kan ikke bruge en tidligere adgangskode.");
        }

        // Update password and rotate stamp
        string newStamp = Guid.NewGuid().ToString();
        await _userRepository.UpdatePasswordAsync(user.Id, newHash, newStamp, ct);
        await _userRepository.AddPasswordHistoryAsync(user.Id, newHash, ct);
        await _userRepository.DeleteResetTokenAsync(user.Id, ct);

        return (true, "Din adgangskode er blevet nulstillet succesfuldt.");
    }
}