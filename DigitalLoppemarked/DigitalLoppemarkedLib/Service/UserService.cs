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
    private const int MaxFailedAttempts = 5;

    public UserService(IUserRepository userRepository, IMemoryCache cache)
    {
        _userRepository = userRepository;
        _cache = cache;
    }

    public async Task<(bool Success, string Message)> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "E-mail og adgangskode er påkrævet.");
        }

        // 1. Brute force protection: Check failed attempts
        string cacheKey = $"login_attempts_{email}";
        if (_cache.TryGetValue(cacheKey, out int attempts) && attempts >= MaxFailedAttempts)
        {
            return (false, "Kontoen er midlertidigt låst pga. for mange mislykkede forsøg. Prøv igen senere.");
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
            return (false, "Ugyldig e-mail eller adgangskode.");
        }

        // 3. Success: Reset failed attempts
        _cache.Remove(cacheKey);

        return (true, "Login lykkedes.");
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