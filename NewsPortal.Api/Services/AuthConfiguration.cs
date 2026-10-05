using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using NewsPortal.Api.Models;
namespace NewsPortal.Api.Services;
public static class AuthConfiguration
{
    public static IServiceCollection AddPortalAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var key = config["Jwt:Key"];
        if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("Set Jwt__Key to a random secret of at least 32 bytes. Use scripts/Start-Development.ps1 for local demo.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
            o.TokenValidationParameters = new() { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                ValidateIssuerSigningKey = true, ValidIssuer = config["Jwt:Issuer"] ?? "NewsPortal.Api", ValidAudience = config["Jwt:Audience"] ?? "NewsPortal.Web",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ClockSkew = TimeSpan.FromSeconds(30) };
            o.Events = new JwtBearerEvents { OnTokenValidated = async context => {
                var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = id == null ? null : await manager.FindByIdAsync(id);
                if (user == null || !user.IsActive || await manager.IsLockedOutAsync(user) ||
                    context.Principal?.FindFirstValue("stamp") != user.SecurityStamp) context.Fail("Account disabled or token revoked.");
            }};
        });
        return services;
    }
}
