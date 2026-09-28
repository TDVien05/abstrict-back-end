using Abstrict.Api.BackgroundJobs;
using Abstrict.Api.Data;
using Abstrict.Api.Integrations.Identity;
using Abstrict.Api.Integrations.Notifications;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Options;
using Abstrict.Api.Repositories.Implementations;
using Abstrict.Api.Repositories.Interfaces;
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

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services.AddControllers();
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

builder.Services.Configure<KycOptions>(builder.Configuration.GetSection(KycOptions.SectionName));
var kycEnabled = builder.Configuration.GetValue<bool>("Kyc:Enabled");
if (kycEnabled)
    KycOptionsValidator.Validate(builder.Configuration.GetSection(KycOptions.SectionName).Get<KycOptions>() ?? new KycOptions());

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFreelancerApplicationRepository, FreelancerApplicationRepository>();
builder.Services.AddScoped<IVerificationDocumentRepository, VerificationDocumentRepository>();
builder.Services.AddScoped<IIdentityAttemptRepository, IdentityAttemptRepository>();
builder.Services.AddScoped<IKycOperationRepository, KycOperationRepository>();
builder.Services.AddScoped<IApplicationSubmissionRepository, ApplicationSubmissionRepository>();
builder.Services.AddScoped<IIdentityClaimRepository, IdentityClaimRepository>();
builder.Services.AddScoped<IKycConsentRepository, KycConsentRepository>();

builder.Services.AddSingleton<IPrivateFileStorage, LocalPrivateFileStorage>();
builder.Services.AddSingleton<ISensitiveDataProtector, SensitiveDataProtector>();
builder.Services.AddSingleton<IIdentityFingerprintService, IdentityFingerprintService>();
builder.Services.AddSingleton<JwtTokenFactory>();

builder.Services.AddScoped<IFreelancerRegistrationService, FreelancerRegistrationService>();
builder.Services.AddScoped<IFreelancerLoginService, FreelancerLoginService>();
builder.Services.AddScoped<IFreelancerOnboardingService, FreelancerOnboardingService>();
builder.Services.AddScoped<IKycService, KycService>();
builder.Services.AddScoped<IProviderApprovalService, ProviderApprovalService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();

if (kycEnabled)
{
    var fptBaseUrl = builder.Configuration["Kyc:FptAi:BaseUrl"] ?? "https://api.fpt.ai/";
    var fptTimeout = builder.Configuration.GetValue<int?>("Kyc:FptAi:TimeoutSeconds") ?? 30;
    builder.Services.AddHttpClient<IFptAiIdentityClient, FptAiIdentityClient>(client =>
    {
        client.BaseAddress = new Uri(fptBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(fptTimeout);
    });

    var faceBaseUrl = builder.Configuration["Kyc:FacePlusPlus:BaseUrl"]!;
    var faceTimeout = builder.Configuration.GetValue<int?>("Kyc:FacePlusPlus:TimeoutSeconds") ?? 30;
    builder.Services.AddHttpClient<IFaceVerificationClient, FacePlusPlusClient>(client =>
    {
        client.BaseAddress = new Uri(faceBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(faceTimeout);
    });
}
else
{
    builder.Services.AddSingleton<IFptAiIdentityClient, StubFptAiIdentityClient>();
    builder.Services.AddSingleton<IFaceVerificationClient, StubFaceVerificationClient>();
}

builder.Services.AddHostedService<KycOperationWorker>();
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
    options.AddPolicy("kyc-operation", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();

public partial class Program;
