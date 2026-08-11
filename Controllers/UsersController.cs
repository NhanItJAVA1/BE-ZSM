using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtService _jwtService;
        private readonly DbSaveHelper _dbSaveHelper;
        public UsersController(
            AppDbContext context,
            JwtService jwtService,
            DbSaveHelper dbSaveHelper)
        {
            _context = context;
            _jwtService = jwtService;
            _dbSaveHelper = dbSaveHelper;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.DisplayName,
                    u.AvatarUrl,
                    u.CreatedAt,
                    u.UpdatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/Users/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.DisplayName,
                    u.AvatarUrl,
                    u.CreatedAt,
                    u.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found"
                });
            }

            return Ok(user);
        }

        // POST: api/Users/register
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterUserDto dto)
        {
            // Check username
            var usernameExists = await _context.Users
                .AnyAsync(u => u.Username == dto.Username);

            if (usernameExists)
            {
                return BadRequest(new
                {
                    message = "Username already exists"
                });
            }

            // Check email
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email == dto.Email);

            if (emailExists)
            {
                return BadRequest(new
                {
                    message = "Email already exists"
                });
            }

            // Hash password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = passwordHash,
                DisplayName = dto.DisplayName,
                AvatarUrl = dto.AvatarUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            return CreatedAtAction(
                nameof(GetUser),
                new { id = user.Id },
                new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.DisplayName,
                    user.AvatarUrl,
                    user.CreatedAt,
                    user.UpdatedAt
                }
            );
        }

        // POST: api/Users/login
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginUserDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == dto.Username);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid username or password"
                });
            }

            var passwordValid = BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash
            );

            if (!passwordValid)
            {
                return Unauthorized(new
                {
                    message = "Invalid username or password"
                });
            }

            var token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                message = "Login successful",

                accessToken = token,

                user = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.DisplayName,
                    user.AvatarUrl
                }
            });
        }

        // PUT: api/Users/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(
            int id,
            UpdateUserDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found"
                });
            }

            // Check email
            var emailExists = await _context.Users
                .AnyAsync(u =>
                    u.Email == dto.Email &&
                    u.Id != id);

            if (emailExists)
            {
                return BadRequest(new
                {
                    message = "Email already exists"
                });
            }

            user.Email = dto.Email;
            user.DisplayName = dto.DisplayName;
            user.AvatarUrl = dto.AvatarUrl;
            user.UpdatedAt = DateTime.UtcNow;

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.DisplayName,
                user.AvatarUrl,
                user.CreatedAt,
                user.UpdatedAt
            });
        }

        // DELETE: api/Users/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found"
                });
            }

            _context.Users.Remove(user);

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            return NoContent();
        }
    }
}