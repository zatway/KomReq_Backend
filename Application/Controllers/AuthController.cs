using Platform.Models.Response.Identity;
using Platform.Models.Roles;
using Platform.Models.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Application.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IConfiguration _configuration;

    public AuthController(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        var user = new ApplicationUser
        {
            UserName = model.UserName,
            Email = model.Email,
            FullName = model.FullName,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            await _roleManager.CreateAsync(new ApplicationRole { Name = model.Role, Description = $"Роль {model.Role}" });
        }

        await _userManager.AddToRoleAsync(user, model.Role);
        return Ok(new { Message = "Пользователь успешно зарегистрирован." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var user = await _userManager.FindByNameAsync(model.UserName);
        if (user == null || !user.IsActive)
            return Unauthorized(new { Message = "Неверное имя пользователя или пользователь не активен." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
        if (!result.Succeeded)
            return Unauthorized(new { Message = "Неверный пароль." });

        var roles = await _userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles);

        return Ok(new
        {
            Token = token,
            User = new { user.Id, user.UserName, user.Email, user.FullName, Roles = roles }
        });
    }

    private string GenerateJwtToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? string.Empty));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ---------------------- Админ: список всех пользователей ----------------------
    [Authorize(Roles = "Admin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = _userManager.Users.ToList();
        var result = new List<object>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new
            {
                user.Id,
                user.UserName,
                user.FullName,
                user.Email,
                user.IsActive,
                Roles = roles
            });
        }

        return Ok(result);
    }

    // ---------------------- Создание пользователя (только админ) ----------------------
    [Authorize(Roles = "Admin")]
    [HttpPost("create-user")]
    public async Task<IActionResult> CreateUser([FromBody] RegisterModel model)
    {
        return await Register(model);
    }

    // ---------------------- Удаление пользователя ----------------------
    [Authorize]
    [HttpDelete("delete-user/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        if (currentUserId != id && !isAdmin)
            return Forbid("Вы можете удалить только свой аккаунт.");

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { Message = "Пользователь не найден." });

        await _userManager.DeleteAsync(user);
        return Ok(new { Message = "Пользователь удалён." });
    }

    // ---------------------- Смена пароля ----------------------
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        if (currentUserId != model.UserId && !isAdmin)
            return Forbid("Вы можете менять пароль только для себя.");

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null) return NotFound(new { Message = "Пользователь не найден." });

        IdentityResult result;
        if (isAdmin && currentUserId != model.UserId)
        {
            // Админ может менять пароль без старого
            result = await _userManager.RemovePasswordAsync(user);
            if (!result.Succeeded) return BadRequest(result.Errors);
            result = await _userManager.AddPasswordAsync(user, model.NewPassword);
        }
        else
        {
            result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        }

        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(new { Message = "Пароль успешно изменён." });
    }

    // ---------------------- Изменение роли (только админ) ----------------------
    [Authorize(Roles = "Admin")]
    [HttpPost("change-role")]
    public async Task<IActionResult> ChangeRole([FromBody] ChangeRoleModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null) return NotFound(new { Message = "Пользователь не найден." });

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);

        if (!await _roleManager.RoleExistsAsync(model.NewRole))
        {
            await _roleManager.CreateAsync(new ApplicationRole
            {
                Name = model.NewRole,
                Description = $"Роль {model.NewRole}"
            });
        }

        await _userManager.AddToRoleAsync(user, model.NewRole);
        return Ok(new { Message = $"Роль пользователя изменена на {model.NewRole}" });
    }
}

