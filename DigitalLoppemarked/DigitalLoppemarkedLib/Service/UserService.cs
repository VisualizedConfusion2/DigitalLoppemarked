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
}