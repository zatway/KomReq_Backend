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
using Microsoft.EntityFrameworkCore;

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

        var rolesToAdd = model.Roles?.Distinct().ToList() ?? new List<string>();
        if (rolesToAdd.Count == 0)
        {
            rolesToAdd.Add("Client");
        }

        foreach (var role in rolesToAdd)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new ApplicationRole { Name = role, Description = $"Роль {role}" });
            }
        }

        await _userManager.AddToRolesAsync(user, rolesToAdd);
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
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),

            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            new Claim(ClaimTypes.NameIdentifier, user.Id),

            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),

            new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
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

        // Soft-delete: deactivate instead of physical delete to satisfy FK constraints
        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok(new { Message = "Пользователь деактивирован." });
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

        var rolesToAdd = model.NewRoles?.Distinct().ToList() ?? new List<string>();
        if (rolesToAdd.Count == 0)
        {
            return BadRequest(new { Message = "Не указаны роли для назначения." });
        }

        foreach (var role in rolesToAdd)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new ApplicationRole
                {
                    Name = role,
                    Description = $"Роль {role}"
                });
            }
        }

        await _userManager.AddToRolesAsync(user, rolesToAdd);
        return Ok(new { Message = $"Роли пользователя изменены." });
    }

    // ---------------------- Получение пользователей по роли для выбора в заявке (Админ/Менеджер) ----------------------
    [Authorize(Roles = "Admin,Manager")]
    [HttpGet("search-users")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? query, [FromQuery] string? role)
    {
        var usersQuery = _userManager.Users.AsQueryable();

        if (!string.IsNullOrEmpty(role))
        {
            var usersInRoleIds = await _userManager.GetUsersInRoleAsync(role);
            usersQuery = usersQuery.Where(u => usersInRoleIds.Contains(u));
        }

        if (!string.IsNullOrEmpty(query))
        {
            usersQuery = usersQuery.Where(u =>
                u.UserName!.Contains(query) ||
                u.Email!.Contains(query) ||
                u.FullName!.Contains(query));
        }

        var result = await usersQuery.Select(u => new
        {
            u.Id,
            u.UserName,
            u.FullName,
            u.Email
        }).ToListAsync();

        return Ok(result);
    }
}

