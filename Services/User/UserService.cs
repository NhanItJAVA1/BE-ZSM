using AutoMapper;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using BE_ZSM.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Services;
public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtService _jwtService;
    private readonly IMapper _mapper;
    private readonly IGenericRepository<User> _userRepo;

    public UserService(
        IUnitOfWork unitOfWork,
        JwtService jwtService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
        _mapper = mapper;
        _userRepo = _unitOfWork.GetRepository<User>();
    }
    public async Task<List<UserResponseDto>> GetUsersAsync()
    {
        var users = await _userRepo.All().Include(u => u.Role).AsNoTracking().ToListAsync();

        return _mapper.Map<List<UserResponseDto>>(users);

    }

    public async Task<UserResponseDto> GetUserAsync(int id)
    {
        var user = await _unitOfWork
                .GetRepository<User>()
                .Where(u => u.Id == id)
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            throw new NotFoundException(
                "User not found",
                "USER_NOT_FOUND");
        }

        return _mapper.Map<UserResponseDto>(user);
    }

    public async Task RegisterAsync(RegisterUserDto dto)
    {
        var userRepository = _userRepo;

        var roleRepository =
            _unitOfWork.GetRepository<Role>();

        var usernameExists = await userRepository
            .Where(u => u.Username == dto.Username)
            .AnyAsync();

        if (usernameExists)
        {
            throw new ConflictException(
                "Username already exists",
                "USERNAME_ALREADY_EXISTS");
        }

        var emailExists = await userRepository
            .Where(u => u.Email == dto.Email)
            .AnyAsync();

        if (emailExists)
        {
            throw new ConflictException(
                "Email already exists",
                "EMAIL_ALREADY_EXISTS");
        }

        var userRole = await roleRepository
            .Where(r => r.Name == UserRole.User)
            .FirstOrDefaultAsync();

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

        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await userRepository.CreateAsync(user);

        await _unitOfWork.SaveChangesAsync();
    }
    public async Task<LoginResponseDto> LoginAsync(LoginUserDto dto)
    {
        var user = await _userRepo.Where(u => u.Username == dto.Username).Include(u => u.Role).FirstOrDefaultAsync();

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

        await _unitOfWork.GetRepository<RefreshToken>().CreateAsync(refreshTokenEntity);
        await _unitOfWork.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = _mapper.Map<UserResponseDto>(user)
        };
    }

    public async Task UpdateAsync(int id, UpdateUserDto dto)
    {
        var repository = _userRepo;

        var user = await repository
            .Where(u => u.Id == id)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            throw new NotFoundException(
                "User not found",
                "USER_NOT_FOUND");
        }

        var emailExists = await repository
            .Where(u => u.Email == dto.Email && u.Id != id)
            .AnyAsync();

        if (emailExists)
        {
            throw new ConflictException(
                "Email already exists",
                "EMAIL_ALREADY_EXISTS");
        }

        _mapper.Map(dto, user);

        user.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(user);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var user =
            await _userRepo.FindAsync(u => u.Id == id);

        if (user == null)
        {
            throw new NotFoundException(
                "User not found",
                "USER_NOT_FOUND");
        }
        await _userRepo.DeleteAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(string token)
    {
        var repository = _unitOfWork.GetRepository<RefreshToken>();
        var refreshToken = await repository
                .Where(rt => rt.Token == token)
                .Include(rt => rt.User)
                .ThenInclude(u => u.Role)
                .FirstOrDefaultAsync();

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