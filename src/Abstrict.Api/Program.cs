using Abstrict.Api.Data;
using Microsoft.AspNetCore.DataProtection;
using Abstrict.Api.Integrations.Notifications;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Services.Implementations;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dữ liệu gửi lên không hợp lệ"
        };
        problem.Extensions["code"] = "VALIDATION_FAILED";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem);
    };
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("code", "UNEXPECTED_ERROR");
        context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
    };
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ICustomerRegistrationService, CustomerRegistrationService>();
builder.Services.AddScoped<ICustomerLoginService, CustomerLoginService>();
builder.Services.Configure<LocalFileStorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddDataProtection()
    .SetApplicationName("abstrict")
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
var kycHashKey = builder.Configuration["Kyc:CitizenIdHashKey"];
if (string.IsNullOrWhiteSpace(kycHashKey) && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Kyc:CitizenIdHashKey must be configured outside Development.");
builder.Services.AddSingleton(serviceProvider => new CitizenIdProtector(
    serviceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(), kycHashKey));
builder.Services.AddScoped<IFreelancerKycService, FreelancerKycService>();
builder.Services.AddScoped<IAdminKycService, AdminKycService>();
builder.Services.AddScoped<IFreelancerDirectoryService, FreelancerDirectoryService>();
var jwtKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtKey) && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Jwt:SigningKey must be configured outside Development.");
jwtKey ??= "ABSTRICT-development-only-JWT-signing-key-do-not-use-in-production-2026";
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "abstrict-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "abstrict-client";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwtIssuer,
        ValidateAudience = true, ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton(new OtpCodeHasher(builder.Configuration["Otp:HmacKey"]));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPhoneOtpSender>(serviceProvider => builder.Environment.IsDevelopment()
    ? new DevelopmentPhoneOtpSender()
    : new UnconfiguredPhoneOtpSender());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("kyc-write", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    options.AddPolicy("auth-register", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    options.AddPolicy("auth-login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("auth-otp-verify", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("auth-otp-resend", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ABSTRICT API",
        Version = "v1"
    });
});
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

{
    await using var seedScope = app.Services.CreateAsyncScope();
    await AdminAccountSeeder.SeedAsync(seedScope.ServiceProvider, builder.Configuration, app.Logger);
}

app.Run();

public partial class Program;
