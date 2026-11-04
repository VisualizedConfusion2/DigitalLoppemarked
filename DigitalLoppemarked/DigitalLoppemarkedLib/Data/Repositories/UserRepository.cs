using DigitalLoppemarkedLib.Data.Repositories;
using DigitalLoppemarkedLib.Models;
using Npgsql;
using System.Data;

namespace DigitalLoppemarkedLib.Data.Repositories;

/// <summary>
/// Repository for managing User data in the database.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public UserRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand("SELECT id, email, password_hash, full_name, is_active, created_at, gdpr_consent, security_stamp FROM users WHERE email = @email");
        cmd.Parameters.AddWithValue("email", email);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapUser(reader) : null;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand("SELECT id, email, password_hash, full_name, is_active, created_at, gdpr_consent, security_stamp FROM users WHERE id = @id");
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapUser(reader) : null;
    }

    /// <summary>
    /// Maps a database reader row to a User model.
    /// </summary>
    private User MapUser(NpgsqlDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt32(0),
            Email = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            FullName = reader.GetString(3),
            IsActive = reader.GetBoolean(4),
            CreatedAt = reader.GetDateTime(5),
            GdprConsent = reader.GetBoolean(6),
            SecurityStamp = reader.GetGuid(7).ToString()
        };
    }

    public async Task<int> CreateUserAsync(User user, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO users (email, password_hash, full_name, gdpr_consent, is_active, security_stamp) VALUES (@email, @hash, @name, @gdpr, @active, @stamp) RETURNING id");
        cmd.Parameters.AddWithValue("email", user.Email);
        cmd.Parameters.AddWithValue("hash", user.PasswordHash);
        cmd.Parameters.AddWithValue("name", user.FullName);
        cmd.Parameters.AddWithValue("gdpr", user.GdprConsent);
        cmd.Parameters.AddWithValue("active", user.IsActive);
        cmd.Parameters.AddWithValue("stamp", Guid.Parse(user.SecurityStamp == string.Empty ? Guid.NewGuid().ToString() : user.SecurityStamp));

        return (int)await cmd.ExecuteScalarAsync(ct);
    }

    public async Task UpdateUserAsync(User user, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            "UPDATE users SET is_active = @active, full_name = @name WHERE id = @id");
        cmd.Parameters.AddWithValue("active", user.IsActive);
        cmd.Parameters.AddWithValue("name", user.FullName);
        cmd.Parameters.AddWithValue("id", user.Id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdatePasswordAsync(int userId, string newHash, string newStamp, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            "UPDATE users SET password_hash = @hash, security_stamp = @stamp WHERE id = @id");
        cmd.Parameters.AddWithValue("hash", newHash);
        cmd.Parameters.AddWithValue("stamp", Guid.Parse(newStamp));
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> HasPasswordBeenUsedAsync(int userId, string passwordHash, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT EXISTS(SELECT 1 FROM password_history WHERE user_id = @id AND password_hash = @hash)");
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("hash", passwordHash);
        return (bool)await cmd.ExecuteScalarAsync(ct);
    }

    public async Task AddPasswordHistoryAsync(int userId, string passwordHash, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO password_history (user_id, password_hash) VALUES (@id, @hash)");
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("hash", passwordHash);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveResetTokenAsync(int userId, string token, DateTime expiresAt, CancellationToken ct)
    {
        await DeleteResetTokenAsync(userId, ct);

        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO user_verification_tokens (user_id, token, expires_at) VALUES (@id, @token, @expiry)");
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("token", token);
        cmd.Parameters.AddWithValue("expiry", expiresAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<User?> GetUserByResetTokenAsync(string token, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT u.id, u.email, u.password_hash, u.full_name, u.is_active, u.created_at, u.gdpr_consent, u.security_stamp " +
            "FROM users u JOIN user_verification_tokens t ON u.id = t.user_id WHERE t.token = @token AND t.expires_at > now()");
        cmd.Parameters.AddWithValue("token", token);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapUser(reader) : null;
    }

    public async Task DeleteResetTokenAsync(int userId, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand("DELETE FROM user_verification_tokens WHERE user_id = @id");
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

}