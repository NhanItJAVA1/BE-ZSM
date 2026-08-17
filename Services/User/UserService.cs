using BE_ZSM.DTOs.RefreshToken;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Interfaces;
using BE_ZSM.Services.Interfaces;

namespace BE_ZSM.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly JwtService _jwtService;

    public UserService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        JwtService jwtService)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtService = jwtService;
    }
    public async Task<List<UserResponseDto>> GetUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();

        return users
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<UserResponseDto> GetUserAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            throw new NotFoundException(
                "User not found",
                "USER_NOT_FOUND");
        }

        return MapToResponse(user);
    }

    public async Task<UserResponseDto> RegisterAsync(
        RegisterUserDto dto)
    {
        var usernameExists =
            await _userRepository.ExistsByUsernameAsync(
                dto.Username);

        if (usernameExists)
        {
            throw new ConflictException(
                "Username already exists",
                "USERNAME_ALREADY_EXISTS");
        }


        var emailExists =
            await _userRepository.ExistsByEmailAsync(
                dto.Email);

        if (emailExists)
        {
            throw new ConflictException(
                "Email already exists",
                "EMAIL_ALREADY_EXISTS");
        }


        var userRole =
            await _roleRepository.GetByNameAsync(
                UserRole.User);

        if (userRole == null)
        {
            throw new AppException(
                "Default user role not found",
                500,
                "DEFAULT_ROLE_NOT_FOUND");
        }


        var passwordHash =
            BCrypt.Net.BCrypt.HashPassword(
                dto.Password);


        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = passwordHash,
            DisplayName = dto.DisplayName,
            AvatarUrl = dto.AvatarUrl,

            RoleId = userRole.Id,
            Role = userRole,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };


        await _userRepository.AddAsync(user);


        await _userRepository.SaveChangesAsync();


        return MapToResponse(user);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginUserDto dto)
    {
        var user =
            await _userRepository.GetByUsernameAsync(
                dto.Username);

        if (user == null)
        {
            throw new UnauthorizedException(
                "Invalid username or password",
                "INVALID_CREDENTIALS");
        }


        var passwordValid =
            BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash);

        if (!passwordValid)
        {
            throw new UnauthorizedException(
                "Invalid username or password",
                "INVALID_CREDENTIALS");
        }


        var accessToken =
            _jwtService.GenerateToken(user);


        var refreshToken =
            _jwtService.GenerateRefreshToken();


        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt =
                _jwtService.GetRefreshTokenExpiration()
        };


        await _refreshTokenRepository
            .AddAsync(refreshTokenEntity);


        await _userRepository.SaveChangesAsync();


        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = MapToResponse(user)
        };
    }

    public async Task<UserResponseDto> UpdateAsync(int id,UpdateUserDto dto)
    {
        var user =
            await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            throw new NotFoundException(
                "User not found",
                "USER_NOT_FOUND");
        }


        var emailExists =
            await _userRepository.ExistsByEmailAsync(
                dto.Email,
                id);

        if (emailExists)
        {
            throw new ConflictException(
                "Email already exists",
                "EMAIL_ALREADY_EXISTS");
        }


        user.Email = dto.Email;
        user.DisplayName = dto.DisplayName;
        user.AvatarUrl = dto.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.SaveChangesAsync();
        return MapToResponse(user);
    }

    public async Task DeleteAsync(int id)
    {
        var user =
            await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            throw new NotFoundException(
                "User not found",
                "USER_NOT_FOUND");
        }
        _userRepository.Delete(user);
        await _userRepository.SaveChangesAsync();
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(string token)
    {
        var refreshToken =
            await _refreshTokenRepository.GetByTokenAsync(token);

        if (refreshToken == null ||
            refreshToken.RevokeAt != null ||
            refreshToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedException(
                "Invalid or expired refresh token",
                "INVALID_REFRESH_TOKEN");
        }


        var user = refreshToken.User;


        var newAccessToken =
            _jwtService.GenerateToken(user);


        return new LoginResponseDto
        {
            AccessToken = newAccessToken,

            RefreshToken = refreshToken.Token,

            User = MapToResponse(user)
        };
    }

    private static UserResponseDto MapToResponse(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.Name.ToString(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}