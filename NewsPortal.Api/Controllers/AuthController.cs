using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Models;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Controllers;
[ApiController, Route("api/v1/auth")]
public class AuthController(UserManager<ApplicationUser> users, IConfiguration config, ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>Register a reader account. Roles supplied by a client are never accepted.</summary>
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto input)
    {
        var user = new ApplicationUser { Email = input.Email, UserName = input.Email, FullName = input.FullName.Trim() };
        var result = await users.CreateAsync(user, input.Password);
        if (!result.Succeeded) return BadRequest(new ProblemDetails { Status = 400, Title = string.Join(" ", result.Errors.Select(e => e.Description)) });
        var role = await users.AddToRoleAsync(user, "User");
        if (!role.Succeeded) { await users.DeleteAsync(user); throw new ApiException(500, "Không thể tạo tài khoản."); }
        return StatusCode(201, await Token(user));
    }
    /// <summary>Authenticate an active account and return a two-hour JWT bearer token.</summary>
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto input)
    {
        var user = await users.FindByEmailAsync(input.Email);
        if (user == null || !user.IsActive || await users.IsLockedOutAsync(user) || !await users.CheckPasswordAsync(user, input.Password)) {
            if (user != null) await users.AccessFailedAsync(user);
            logger.LogWarning("Login rejected for account {UserId}", user?.Id ?? "unknown");
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Email hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa." });
        }
        await users.ResetAccessFailedCountAsync(user); user.LastLoginAt = DateTime.UtcNow; await users.UpdateAsync(user);
        return await Token(user);
    }
    [HttpGet("me"), Authorize] public async Task<UserDto> Me() => await ToDto((await users.FindByIdAsync(ArticleService.UserId(User)))!);
    [HttpPut("me"), Authorize] public async Task<UserDto> Update(ProfileInput input)
    {
        var user = (await users.FindByIdAsync(ArticleService.UserId(User)))!;
        user.FullName = input.FullName.Trim(); user.Bio = input.Bio; await users.UpdateAsync(user); return await ToDto(user);
    }
    [HttpPost("logout"), Authorize] public async Task<IActionResult> Logout()
    { var user = (await users.FindByIdAsync(ArticleService.UserId(User)))!; await users.UpdateSecurityStampAsync(user); return NoContent(); }
    private async Task<UserDto> ToDto(ApplicationUser u) => new(u.Id, u.Email!, u.FullName, u.Bio, u.AvatarUrl, u.IsActive, await users.GetRolesAsync(u));
    private async Task<AuthResponseDto> Token(ApplicationUser user)
    {
        var dto = await ToDto(user); var expires = DateTime.UtcNow.AddHours(2);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.FullName), new("stamp", user.SecurityStamp!), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) };
        claims.AddRange(dto.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var token = new JwtSecurityToken(config["Jwt:Issuer"] ?? "NewsPortal.Api", config["Jwt:Audience"] ?? "NewsPortal.Web", claims,
            expires: expires, signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, dto);
    }
}
