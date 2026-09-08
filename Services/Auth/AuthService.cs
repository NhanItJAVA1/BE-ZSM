using AutoMapper;
using BE_ZSM.DTOs.Auth;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using BE_ZSM.Services.Auth.Models;
using BE_ZSM.Services.Provider;
using Microsoft.EntityFrameworkCore;
using ExternalLoginEntity = BE_ZSM.Entities.ExternalLogin;

namespace BE_ZSM.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IEnumerable<IExternalAuthProvider> _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtService _jwtService;
    private readonly IMapper _mapper;
    private readonly IGenericRepository<User> _userRepo;
    private readonly IGenericRepository<Role> _roleRepo;
    private readonly IGenericRepository<RefreshToken> _refreshTokenRepo;
    private readonly IGenericRepository<ExternalLoginEntity> _externalLoginRepo;

    public AuthService(
        IEnumerable<IExternalAuthProvider> providers,
        IUnitOfWork unitOfWork,
        JwtService jwtService,
        IMapper mapper)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
        _mapper = mapper;

        _userRepo = unitOfWork.GetRepository<User>();
        _roleRepo = unitOfWork.GetRepository<Role>();
        _refreshTokenRepo = unitOfWork.GetRepository<RefreshToken>();
        _externalLoginRepo = unitOfWork.GetRepository<ExternalLoginEntity>();
    }

    public async Task RegisterAsync(RegisterUserDto dto)
    {
        await EnsureUsernameAvailableAsync(dto.Username);
        await EnsureEmailAvailableAsync(dto.Email);

        var userRole = await GetDefaultUserRoleAsync();
        var user = _mapper.Map<User>(dto);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        user.RoleId = userRole.Id;
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepo.CreateAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<LoginResponseDto> LoginAsync(LoginUserDto dto)
    {
        var user = await _userRepo.Where(u => u.Username == dto.Username)
            .Include(u => u.Role)
            .FirstOrDefaultAsync();

        if (user == null || !IsPasswordValid(dto.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid username or password", "INVALID_CREDENTIALS");

        return await CreateLoginSessionAsync(user);
    }

    public async Task<LoginResponseDto> ExternalLoginAsync(ExternalLoginDto dto)
    {
        var provider = GetProvider(dto);
        var externalUser = await provider.ValidateAsync(dto.Token);
        var user = await FindOrCreateExternalUserAsync(dto, externalUser);

        return await CreateLoginSessionAsync(user);
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(string token)
    {
        var refreshToken = await _refreshTokenRepo
            .Where(rt => rt.Token == token)
            .Include(rt => rt.User)
            .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync();

        if (!IsRefreshTokenActive(refreshToken))
            throw new UnauthorizedException("Invalid or expired refresh token", "INVALID_REFRESH_TOKEN");

        return new LoginResponseDto
        {
            AccessToken = CreateAccessToken(refreshToken!.User),
            RefreshToken = refreshToken.Token,
            User = _mapper.Map<UserResponseDto>(refreshToken.User)
        };
    }

    public async Task LogoutAsync(string token)
    {
        var refreshToken = await _refreshTokenRepo
            .Where(rt => rt.Token == token)
            .FirstOrDefaultAsync();

        if (!IsRefreshTokenActive(refreshToken))
            throw new UnauthorizedException("Invalid or expired refresh token", "INVALID_REFRESH_TOKEN");

        refreshToken!.RevokeAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    private IExternalAuthProvider GetProvider(ExternalLoginDto dto)
    {
        var provider = _providers.FirstOrDefault(x => x.Provider == dto.Provider);

        if (provider == null)
            throw new BadRequestException("Unsupported authentication provider", "UNSUPPORTED_AUTH_PROVIDER");

        return provider;
    }

    private async Task<User> FindOrCreateExternalUserAsync(
        ExternalLoginDto dto,
        ExternalUserInfo externalUser)
    {
        var externalLogin = await FindExternalLoginAsync(dto, externalUser.ProviderUserId);

        if (externalLogin != null)
            return externalLogin.User;

        var user = await FindUserByEmailAsync(externalUser.Email)
            ?? await CreateExternalUserAsync(externalUser);

        return await LinkExternalLoginAsync(user, dto, externalUser);
    }

    private async Task<ExternalLoginEntity?> FindExternalLoginAsync(
        ExternalLoginDto dto,
        string providerUserId)
    {
        return await _externalLoginRepo.All()
            .Include(x => x.User)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x =>
                x.Provider == dto.Provider &&
                x.ProviderUserId == providerUserId);
    }

    private async Task<User?> FindUserByEmailAsync(string email)
    {
        return await _userRepo.Where(x => x.Email == email)
            .Include(x => x.Role)
            .FirstOrDefaultAsync();
    }

    private async Task<User> CreateExternalUserAsync(ExternalUserInfo externalUser)
    {
        var userRole = await GetDefaultUserRoleAsync();

        return new User
        {
            Email = externalUser.Email,
            Username = externalUser.Email,
            DisplayName = externalUser.DisplayName ?? externalUser.Email,
            AvatarUrl = externalUser.AvatarUrl,
            RoleId = userRole.Id,
            Role = userRole,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private async Task<User> LinkExternalLoginAsync(
        User user,
        ExternalLoginDto dto,
        ExternalUserInfo externalUser)
    {
        var externalLogin = new ExternalLoginEntity
        {
            User = user,
            Provider = dto.Provider,
            ProviderUserId = externalUser.ProviderUserId,
            Email = externalUser.Email,
            CreatedAt = DateTime.UtcNow
        };

        await _externalLoginRepo.CreateAsync(externalLogin);

        try
        {
            await _unitOfWork.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException ex)
        {
            DetachEntries(ex);

            var existingLogin = await FindExternalLoginAsync(dto, externalUser.ProviderUserId);
            if (existingLogin != null)
                return existingLogin.User;

            throw new ConflictException("External login could not be linked", "EXTERNAL_LOGIN_CONFLICT");
        }
    }

    private async Task<LoginResponseDto> CreateLoginSessionAsync(User user)
    {
        var refreshToken = _jwtService.GenerateRefreshToken();
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = _jwtService.GetRefreshTokenExpiration()
        };

        await _refreshTokenRepo.CreateAsync(refreshTokenEntity);
        await _unitOfWork.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = CreateAccessToken(user),
            RefreshToken = refreshToken,
            User = _mapper.Map<UserResponseDto>(user)
        };
    }

    private string CreateAccessToken(User user)
    {
        return _jwtService.GenerateToken(user);
    }

    private async Task EnsureUsernameAvailableAsync(string username)
    {
        var exists = await _userRepo.Where(u => u.Username == username)
            .AsNoTracking()
            .AnyAsync();

        if (exists)
            throw new ConflictException("Username already exists", "USERNAME_ALREADY_EXISTS");
    }

    private async Task EnsureEmailAvailableAsync(string email)
    {
        var exists = await _userRepo.Where(u => u.Email == email)
            .AsNoTracking()
            .AnyAsync();

        if (exists)
            throw new ConflictException("Email already exists", "EMAIL_ALREADY_EXISTS");
    }

    private async Task<Role> GetDefaultUserRoleAsync()
    {
        var userRole = await _roleRepo.Where(r => r.Name == UserRole.User)
            .FirstOrDefaultAsync();

        if (userRole == null)
            throw new AppException("Default user role not found", 500, "DEFAULT_ROLE_NOT_FOUND");

        return userRole;
    }

    private static bool IsPasswordValid(string password, string? passwordHash)
    {
        return !string.IsNullOrWhiteSpace(passwordHash)
            && BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    private static bool IsRefreshTokenActive(RefreshToken? refreshToken)
    {
        return refreshToken != null
            && refreshToken.RevokeAt == null
            && refreshToken.ExpiresAt >= DateTime.UtcNow;
    }

    private static void DetachEntries(DbUpdateException exception)
    {
        foreach (var entry in exception.Entries)
            entry.State = EntityState.Detached;
    }
}
