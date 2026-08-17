using BE_ZSM.DTOs.RefreshToken;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BE_ZSM.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userService.GetUsersAsync();

        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _userService.GetUserAsync(id);

        return Ok(user);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterUserDto dto)
    {
        var user = await _userService.RegisterAsync(dto);

        return CreatedAtAction(
            nameof(GetUser),
            new { id = user.Id },
            user);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginUserDto dto)
    {
        var result = await _userService.LoginAsync(dto);

        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(
        int id,
        UpdateUserDto dto)
    {
        var user = await _userService.UpdateAsync(id, dto);

        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        await _userService.DeleteAsync(id);

        return NoContent();
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(
        RefreshTokenDto dto)
    {
        var result =
            await _userService.RefreshTokenAsync(
                dto.RefreshToken);

        return Ok(result);
    }
}