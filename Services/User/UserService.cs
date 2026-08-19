using AutoMapper;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.UnitOfWork;
using BE_ZSM.Services.Interfaces;

namespace BE_ZSM.Services;
public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtService _jwtService;
    private readonly IMapper _mapper;

    public UserService(
        IUnitOfWork unitOfWork,
        JwtService jwtService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
        _mapper = mapper;
    }
    public async Task<List<UserResponseDto>> GetUsersAsync()
    {
        var users = await _unitOfWork.Users.GetAllWithRoleAsync();

        return _mapper.Map<List<UserResponseDto>>(users);

    }

    public async Task<UserResponseDto> GetUserAsync(int id)
    {
        var user = await _unitOfWork.Users.GetByIdWithRoleAsync(id);

        if (user == null)
        {
            throw new NotFoundException(
                "User not found",
                "USER_NOT_FOUND");
        }

        return _mapper.Map<UserResponseDto>(user);
    }

    public async Task<UserResponseDto> RegisterAsync(
        RegisterUserDto dto)
    {
        var usernameExists =
            await _unitOfWork.Users.ExistsByUsernameAsync(
                dto.Username);

        if (usernameExists)
        {
            throw new ConflictException(
                "Username already exists",
                "USERNAME_ALREADY_EXISTS");
        }


        var emailExists =
            await _unitOfWork.Users.ExistsByEmailAsync(
                dto.Email);

        if (emailExists)
        {
            throw new ConflictException(
                "Email already exists",
                "EMAIL_ALREADY_EXISTS");
        }

        var userRole =
            await _unitOfWork.Roles.GetByNameAsync(
                UserRole.User);

        if (userRole == null)
        {
            throw new AppException(
                "Default user role not found",
                500,
                "DEFAULT_ROLE_NOT_FOUND");
        }

        var user = _mapper.Map<User>(dto);

        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(dto.Password);

        user.RoleId = userRole.Id;
        user.Role = userRole;

        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<UserResponseDto>(user);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginUserDto dto)
    {
        var user =
            await _unitOfWork.Users.GetByUsernameAsync(
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

        var accessToken = _jwtService.GenerateToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = _jwtService.GetRefreshTokenExpiration()
        };

        await _unitOfWork.RefreshTokens.AddAsync(refreshTokenEntity);
        await _unitOfWork.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = _mapper.Map<UserResponseDto>(user)
        };
    }

    public async Task<UserResponseDto> UpdateAsync(int id,UpdateUserDto dto)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null)
        {
            throw new NotFoundException(
                "User not found",
                "USER_NOT_FOUND");
        }

        var emailExists =
            await _unitOfWork.Users.ExistsByEmailAsync(
                dto.Email,
                id);

        if (emailExists)
        {
            throw new ConflictException(
                "Email already exists",
                "EMAIL_ALREADY_EXISTS");
        }

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();
        return _mapper.Map<UserResponseDto>(user);
    }

    public async Task DeleteAsync(int id)
    {
        var user =
            await _unitOfWork.Users.GetByIdAsync(id);

        if (user == null)
        {
            throw new NotFoundException(
                "User not found",
                "USER_NOT_FOUND");
        }
        _unitOfWork.Users.Delete(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(string token)
    {
        var refreshToken =
            await _unitOfWork.RefreshTokens.GetByTokenAsync(token);

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
            User = _mapper.Map<UserResponseDto>(user)
        };
    }    
}