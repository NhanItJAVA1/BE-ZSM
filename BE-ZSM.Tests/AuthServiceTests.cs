using AutoMapper;
using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Auth;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.UnitOfWork;
using BE_ZSM.Services;
using BE_ZSM.Services.Auth;
using BE_ZSM.Services.Auth.Models;
using BE_ZSM.Services.Provider;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BE_ZSM.Tests;

public sealed class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AuthService _service;
    private readonly FakeGoogleAuthProvider _googleProvider = new();

    public AuthServiceTests()
    {
        Environment.SetEnvironmentVariable("JWT_ISSUER", "BE-ZSM-Test");
        Environment.SetEnvironmentVariable("JWT_AUDIENCE", "BE-ZSM-Test-Client");
        Environment.SetEnvironmentVariable("JWT_KEY", "BE-ZSM-Test-Key-For-Auth-Service-Tests-Only-123456789");
        Environment.SetEnvironmentVariable("JWT_EXPIRE_MINUTES", "60");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"auth-service-tests-{Guid.NewGuid():N}")
            .Options;

        _db = new AppDbContext(options);
        _db.Roles.Add(new Role { Id = 1, Name = UserRole.User, Description = "Regular User" });
        _db.SaveChanges();

        var mapper = new Mock<IMapper>();
        mapper.Setup(x => x.Map<UserResponseDto>(It.IsAny<User>()))
            .Returns((User user) => new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role.Name.ToString(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            });

        _service = new AuthService(
            [_googleProvider],
            new UnitOfWork(_db),
            new JwtService(),
            mapper.Object);
    }

    [Fact]
    public async Task LocalLogin_RefreshToken_ShouldSucceed()
    {
        await SeedLocalUserAsync();

        var login = await _service.LoginAsync(new LoginUserDto
        {
            Username = "local-user",
            Password = "secret"
        });

        var refresh = await _service.RefreshTokenAsync(login.RefreshToken);

        Assert.NotEmpty(login.AccessToken);
        Assert.NotEmpty(login.RefreshToken);
        Assert.Equal(login.RefreshToken, refresh.RefreshToken);
        Assert.Equal("local-user@example.test", refresh.User.Email);
        Assert.Single(_db.RefreshTokens);
    }

    [Fact]
    public async Task GoogleLogin_RefreshToken_ShouldSucceed()
    {
        var login = await _service.ExternalLoginAsync(new ExternalLoginDto
        {
            Provider = AuthProvider.Google,
            Token = "google-id-token"
        });

        var refresh = await _service.RefreshTokenAsync(login.RefreshToken);

        Assert.NotEmpty(login.AccessToken);
        Assert.NotEmpty(login.RefreshToken);
        Assert.Equal(login.RefreshToken, refresh.RefreshToken);
        Assert.Equal("google-user@example.test", refresh.User.Email);
        Assert.Single(_db.ExternalLogins);
        Assert.Single(_db.RefreshTokens);
    }

    [Fact]
    public async Task RefreshToken_Expired_ShouldThrowUnauthorized()
    {
        await SeedLocalUserAsync();
        var login = await LoginLocalUserAsync();
        var refreshToken = await _db.RefreshTokens.SingleAsync();
        refreshToken.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.RefreshTokenAsync(login.RefreshToken));

        Assert.Equal("INVALID_REFRESH_TOKEN", exception.ErrorCode);
    }

    [Fact]
    public async Task RefreshToken_Invalid_ShouldThrowUnauthorized()
    {
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.RefreshTokenAsync("invalid-refresh-token"));

        Assert.Equal("INVALID_REFRESH_TOKEN", exception.ErrorCode);
    }

    [Fact]
    public async Task RefreshToken_Revoked_ShouldThrowUnauthorized()
    {
        await SeedLocalUserAsync();
        var login = await LoginLocalUserAsync();
        var refreshToken = await _db.RefreshTokens.SingleAsync();
        refreshToken.RevokeAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.RefreshTokenAsync(login.RefreshToken));

        Assert.Equal("INVALID_REFRESH_TOKEN", exception.ErrorCode);
    }

    [Fact]
    public async Task Logout_LocalLogin_ShouldRevokeRefreshToken()
    {
        await SeedLocalUserAsync();
        var login = await LoginLocalUserAsync();

        await _service.LogoutAsync(login.RefreshToken);

        var refreshToken = await _db.RefreshTokens.SingleAsync();
        Assert.NotNull(refreshToken.RevokeAt);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.RefreshTokenAsync(login.RefreshToken));

        Assert.Equal("INVALID_REFRESH_TOKEN", exception.ErrorCode);
    }

    [Fact]
    public async Task Logout_GoogleLogin_ShouldRevokeRefreshToken()
    {
        var login = await _service.ExternalLoginAsync(new ExternalLoginDto
        {
            Provider = AuthProvider.Google,
            Token = "google-id-token"
        });

        await _service.LogoutAsync(login.RefreshToken);

        var refreshToken = await _db.RefreshTokens.SingleAsync();
        Assert.NotNull(refreshToken.RevokeAt);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.RefreshTokenAsync(login.RefreshToken));

        Assert.Equal("INVALID_REFRESH_TOKEN", exception.ErrorCode);
    }

    [Fact]
    public async Task GoogleLogin_MultipleTimes_ShouldNotCreateDuplicateUserOrExternalLogin()
    {
        await _service.ExternalLoginAsync(new ExternalLoginDto
        {
            Provider = AuthProvider.Google,
            Token = "google-id-token"
        });

        await _service.ExternalLoginAsync(new ExternalLoginDto
        {
            Provider = AuthProvider.Google,
            Token = "google-id-token"
        });

        Assert.Single(_db.Users);
        Assert.Single(_db.ExternalLogins);
        Assert.Equal(2, await _db.RefreshTokens.CountAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task SeedLocalUserAsync()
    {
        _db.Users.Add(new User
        {
            Username = "local-user",
            Email = "local-user@example.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
            DisplayName = "Local User",
            RoleId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    private async Task<LoginResponseDto> LoginLocalUserAsync()
    {
        return await _service.LoginAsync(new LoginUserDto
        {
            Username = "local-user",
            Password = "secret"
        });
    }

    private sealed class FakeGoogleAuthProvider : IExternalAuthProvider
    {
        public AuthProvider Provider => AuthProvider.Google;

        public Task<ExternalUserInfo> ValidateAsync(string token)
        {
            return Task.FromResult(new ExternalUserInfo
            {
                ProviderUserId = "google-subject",
                Email = "google-user@example.test",
                DisplayName = "Google User",
                AvatarUrl = "https://example.test/avatar.png",
                EmailVerified = true
            });
        }
    }
}
