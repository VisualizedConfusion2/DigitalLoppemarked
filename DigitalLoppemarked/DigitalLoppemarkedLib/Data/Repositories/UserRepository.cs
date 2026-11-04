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
        await using var cmd = _dataSource.CreateCommand("SELECT id, email, password_hash, full_name, is_active, created_at, gdpr_consent FROM users WHERE email = @email");
        cmd.Parameters.AddWithValue("email", email);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapUser(reader) : null;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand("SELECT id, email, password_hash, full_name, is_active, created_at, gdpr_consent FROM users WHERE id = @id");
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
            GdprConsent = reader.GetBoolean(6)
        };
    }

    public async Task<int> CreateUserAsync(User user, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO users (email, password_hash, full_name, gdpr_consent, is_active) VALUES (@email, @hash, @name, @gdpr, @active) RETURNING id");
        cmd.Parameters.AddWithValue("email", user.Email);
        cmd.Parameters.AddWithValue("hash", user.PasswordHash);
        cmd.Parameters.AddWithValue("name", user.FullName);
        cmd.Parameters.AddWithValue("gdpr", user.GdprConsent);
        cmd.Parameters.AddWithValue("active", user.IsActive);

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

}