using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using NewsPortal.Api.Data;
using NewsPortal.Api.Models;
using NewsPortal.Api.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllers(o => { o.Filters.Add<NewsPortal.Api.Middleware.AuditFilter>(); o.Filters.Add<NewsPortal.Api.Middleware.PublicCacheFilter>(); }).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<NewsPortal.Api.Services.PublicCache>();
builder.Services.AddResponseCaching();
builder.Services.AddScoped<NewsPortal.Api.Services.ArticleService>();
builder.Services.AddScoped<EditorialService>();
builder.Services.AddHostedService<ScheduledPublisher>();
builder.Services.AddAuthorization();
builder.Services.AddPortalAuthentication(builder.Configuration);
builder.Services.Configure<IdentityOptions>(o => { o.Password.RequiredLength = 10; o.User.RequireUniqueEmail = true; o.Lockout.MaxFailedAccessAttempts = 5; });
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5200"]).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => {
    o.SwaggerDoc("v1", new() { Title = "NewsPortal API", Version = "v1", Description = "REST API cho độc giả và tòa soạn. Nội dung bài viết là văn bản thuần." });
    o.AddSecurityDefinition("Bearer", new() { Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Đăng nhập qua /api/v1/auth/login và nhập token." });
    o.OperationFilter<NewsPortal.Api.Documentation.ResponseDocumentation>();
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "NewsPortal.Api.xml"));
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<NewsPortal.Api.Middleware.ExceptionHandler>();
var app = builder.Build();
if (args.Contains("--initialize"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    if (app.Environment.IsDevelopment()) await SeedData.InitializeAsync(scope.ServiceProvider, app.Configuration);
    return;
}
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads"));
app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot")) });
app.UseResponseCaching();
app.UseCors();
app.UseAuthentication();
app.Use(async (context, next) => {
    await next();
    if (context.Response.StatusCode is 401 or 403)
        app.Logger.LogWarning("Authorization denied {Method} {Path} with status {Status}", context.Request.Method, context.Request.Path, context.Response.StatusCode);
});
app.UseAuthorization();
app.Logger.LogInformation("NewsPortal API starting in {Environment}", app.Environment.EnvironmentName);
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
public partial class Program;
