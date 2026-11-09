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
using Microsoft.Extensions.Logging.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        _db.Roles.Add(new Role { Id = 1, Name = "User", Description = "Regular User" });
        _db.SaveChanges();

        var mapper = new Mock<IMapper>();
        mapper.Setup(x => x.Map<User>(It.IsAny<RegisterUserDto>()))
            .Returns((RegisterUserDto dto) => new User
            {
                Username = dto.Username,
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                AvatarUrl = dto.AvatarUrl
            });
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
    public async Task LoginAndRefresh_ReturnEffectivePermissionsAcrossRoles()
    {
        await SeedLocalUserAsync();
        _db.Roles.Add(new Role { Id = 2, Name = "Extra", Description = "Extra role" });
        _db.UserRoles.Add(new BE_ZSM.Entities.Rbac.UserRole { UserId = 1, RoleId = 2 });
        _db.RolePermissions.Add(new RolePermission
        {
            RoleId = 1,
            Permission = new Permission { Code = "roles.manage-permissions" }
        });
        _db.RolePermissions.Add(new RolePermission
        {
            RoleId = 2,
            Permission = new Permission { Code = "users.manage-roles" }
        });
        await _db.SaveChangesAsync();

        var login = await LoginLocalUserAsync();
        var refresh = await _service.RefreshTokenAsync(login.RefreshToken);

        Assert.Contains("roles.manage-permissions", login.User.Permissions);
        Assert.Contains("users.manage-roles", login.User.Permissions);
        Assert.Equal(login.User.Permissions, refresh.User.Permissions);
    }

    [Fact]
    public async Task Register_CreatesDefaultUserRoleWithoutClientRoleInput()
    {
        await _service.RegisterAsync(new RegisterUserDto
        {
            Username = "new-user",
            Email = "new-user@example.test",
            Password = "secret",
            DisplayName = "New User"
        });

        var user = await _db.Users.Include(x => x.UserRoles).SingleAsync();
        Assert.Equal(1, user.RoleId);
        Assert.Collection(user.UserRoles, membership => Assert.Equal(1, membership.RoleId));
    }

    [Fact]
    public async Task Login_EmitsAllDistinctUserRolesAndRefreshReadsCurrentRoles()
    {
        await SeedLocalUserAsync();
        _db.Roles.Add(new Role { Id = 2, Name = "Admin", Description = "Administrator" });
        _db.UserRoles.Add(new BE_ZSM.Entities.Rbac.UserRole { UserId = 1, RoleId = 2 });
        await _db.SaveChangesAsync();

        var login = await LoginLocalUserAsync();
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken).Claims;
        Assert.Contains(claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "User");
        Assert.Contains(claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Admin");
        Assert.True(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")).IsInRole("Admin"));

        _db.UserRoles.RemoveRange(_db.UserRoles.Where(x => x.RoleId == 2));
        await _db.SaveChangesAsync();
        var refreshed = await _service.RefreshTokenAsync(login.RefreshToken);
        var refreshedClaims = new JwtSecurityTokenHandler().ReadJwtToken(refreshed.AccessToken).Claims;
        Assert.DoesNotContain(refreshedClaims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Admin");
    }

    [Fact]
    public void UserResponseMapping_KeepsLegacyRoleAndReturnsAllMemberships()
    {
        var mapper = new MapperConfiguration(
            config => config.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var user = new User
        {
            Role = new Role { Id = 1, Name = "User" },
            UserRoles =
            [
                new BE_ZSM.Entities.Rbac.UserRole
                {
                    Role = new Role
                    {
                        Id = 2,
                        Name = "Admin",
                        RolePermissions =
                        [
                            new RolePermission { Permission = new Permission { Code = "roles.create" } }
                        ]
                    }
                },
                new BE_ZSM.Entities.Rbac.UserRole
                {
                    Role = new Role
                    {
                        Id = 1,
                        Name = "User",
                        RolePermissions =
                        [
                            new RolePermission { Permission = new Permission { Code = "todo.read" } }
                        ]
                    }
                }
            ]
        };

        var response = mapper.Map<UserResponseDto>(user);

        Assert.Equal("User", response.Role);
        Assert.Equal(new[] { "Admin", "User" }, response.Roles);
        Assert.Equal(new[] { "roles.create", "todo.read" }, response.Permissions);
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
        Assert.Single(_db.UserRoles);
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
            UserRoles = { new BE_ZSM.Entities.Rbac.UserRole { RoleId = 1 } },
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

