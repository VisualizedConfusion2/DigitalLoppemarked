using DigitalLoppemarkedLib.Data.Repositories;
using DigitalLoppemarkedLib.Models;
using DigitalLoppemarkedLib.Service;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace DigitalLoppemarkedLib.Tests;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly IMemoryCache _cache;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _userService = new UserService(_userRepoMock.Object, _cache);
    }

    // ---- 1. Registration Tests --------------------------------------------------

    [Theory]
    [InlineData("", "password123", "Full Name", true)]
    [InlineData("test@test.com", "", "Full Name", true)]
    [InlineData("test@test.com", "password123", "", true)]
    [InlineData("invalid-email", "password123", "Full Name", true)]
    [InlineData("test@test.com", "password123", "Full Name", false)]
    public async Task RegisterAsync_WithInvalidInput_ReturnsFailure(string email, string password, string name, bool gdpr)
    {
        var result = await _userService.RegisterAsync(email, password, name, gdpr);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RegisterAsync_WithValidInput_HashesPasswordAndSaves()
    {
        var email = "new@test.com";
        var password = "SecurePassword123!";

        _userRepoMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
                     .ReturnsAsync((User)null);

        var result = await _userService.RegisterAsync(email, password, "Test User", true);

        Assert.True(result.Success);
        _userRepoMock.Verify(r => r.CreateUserAsync(It.Is<User>(u =>
            u.Email == email &&
            u.PasswordHash != password &&
            u.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify password hash separately as BCrypt.Verify cannot be used inside Moq expression trees
        var createdUser = _userRepoMock.Invocations
            .FirstOrDefault(i => i.Method.Name == "CreateUserAsync")?
            .Arguments[0] as User;

        Assert.NotNull(createdUser);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, createdUser.PasswordHash));
    }

    // ---- 2. Login Tests --------------------------------------------------------

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsSuccess()
    {
        var email = "user@test.com";
        var password = "password123";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        _userRepoMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new User { Email = email, PasswordHash = hash, IsActive = true });

        var result = await _userService.LoginAsync(email, password);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsTheUsersId()
    {
        var email = "user@test.com";
        var password = "password123";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        _userRepoMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new User { Id = 42, Email = email, PasswordHash = hash, IsActive = true });

        var result = await _userService.LoginAsync(email, password);

        Assert.True(result.Success);
        Assert.Equal(42, result.UserId);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNullUserId()
    {
        var email = "user@test.com";
        var hash = BCrypt.Net.BCrypt.HashPassword("correct_password");

        _userRepoMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new User { Id = 42, Email = email, PasswordHash = hash });

        var result = await _userService.LoginAsync(email, "wrong_password");

        Assert.False(result.Success);
        Assert.Null(result.UserId);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsGenericFailure()
    {
        var email = "user@test.com";
        var password = "password123";
        var hash = BCrypt.Net.BCrypt.HashPassword("correct_password");

        _userRepoMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new User { Email = email, PasswordHash = hash });

        var result = await _userService.LoginAsync(email, "wrong_password");

        Assert.False(result.Success);
        Assert.Equal("Ugyldig e-mail eller adgangskode.", result.Message);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownUser_ReturnsSameGenericFailure()
    {
        var email = "unknown@test.com";
        _userRepoMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
                     .ReturnsAsync((User)null);

        var result = await _userService.LoginAsync(email, "any_password");

        Assert.False(result.Success);
        Assert.Equal("Ugyldig e-mail eller adgangskode.", result.Message);
    }

    [Fact]
    public async Task LoginAsync_RepeatedFailures_TriggersLockout()
    {
        var email = "brute@test.com";
        var password = "wrong_password";
        _userRepoMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
                     .ReturnsAsync((User)null);

        // Fail 5 times
        for (int i = 0; i < 5; i++)
        {
            await _userService.LoginAsync(email, password);
        }

        // 6th attempt should trigger lockout
        var result = await _userService.LoginAsync(email, password);

        Assert.False(result.Success);
        Assert.Contains("låst", result.Message);
    }
}
