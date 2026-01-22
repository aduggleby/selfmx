using System.Threading.RateLimiting;
using Amazon;
using Amazon.SimpleEmailV2;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfMX.Api.Authentication;
using SelfMX.Api.Contracts.Responses;
using SelfMX.Api.Data;
using SelfMX.Api.Endpoints;
using SelfMX.Api.Jobs;
using SelfMX.Api.Services;
using SelfMX.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

// Settings
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));
builder.Services.Configure<AwsSettings>(builder.Configuration.GetSection("Aws"));
builder.Services.Configure<CloudflareSettings>(builder.Configuration.GetSection("Cloudflare"));

// SQLite with WAL mode - main database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=selfmx.db",
        sqlite => sqlite.CommandTimeout(30)));

// Separate audit database to prevent SQLite lock contention
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("AuditConnection") ?? "Data Source=audit.db",
        sqlite => sqlite.CommandTimeout(30)));

// Hangfire with SQLite
// Hangfire.Storage.SQLite expects a filename, not a connection string
// Handle both "Data Source=/path/file.db" and plain "/path/file.db" formats
var hangfireConnString = builder.Configuration.GetConnectionString("HangfireConnection") ?? "selfmx-hangfire.db";
if (hangfireConnString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    hangfireConnString = hangfireConnString["Data Source=".Length..].Trim();
}
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSQLiteStorage(hangfireConnString));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 1; // Single worker for SQLite
    options.Queues = ["default"];
});

// Dual Authentication: Cookie (admin UI) + API Key (programmatic)
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "ApiKey";
        options.DefaultChallengeScheme = "ApiKey";
    })
    .AddCookie("Cookie", options =>
    {
        options.Cookie.Name = "SelfMX.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("ActorType", "admin"));
});

// AWS SES
builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2>(sp =>
{
    var awsSettings = sp.GetRequiredService<IOptions<AwsSettings>>().Value;
    var config = new AmazonSimpleEmailServiceV2Config
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(awsSettings.Region)
    };

    if (!string.IsNullOrEmpty(awsSettings.AccessKeyId) && !string.IsNullOrEmpty(awsSettings.SecretAccessKey))
    {
        return new AmazonSimpleEmailServiceV2Client(
            awsSettings.AccessKeyId,
            awsSettings.SecretAccessKey,
            config);
    }

    // Use default credentials (IAM role, environment variables, etc.)
    return new AmazonSimpleEmailServiceV2Client(config);
});

// Cloudflare
builder.Services.AddHttpClient<ICloudflareService, CloudflareService>();

// Services
builder.Services.AddScoped<DomainService>();
builder.Services.AddScoped<ISesService, SesService>();
builder.Services.AddSingleton<IDnsVerificationService, DnsVerificationService>();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddSingleton<AuditService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AuditService>());
builder.Services.AddHttpContextAccessor();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database")
    .AddDbContextCheck<AuditDbContext>("audit-database");

// Rate limiting: Fixed window for login (5/min), sliding window for API (100/min)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(ApiError.RateLimited.ToResponse(), ct);
    };

    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
    });

    options.AddSlidingWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.SegmentsPerWindow = 6;
        opt.QueueLimit = 0;
    });
});

// CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for cookie auth
    });
});

var app = builder.Build();

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiError.InternalError.ToResponse());
    });
});

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

    var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await auditDb.Database.EnsureCreatedAsync();
    await auditDb.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Serve static files from wwwroot (React frontend)
app.UseStaticFiles();

// Health endpoint (no auth)
app.MapHealthChecks("/health");

// API v1 routes
var v1 = app.MapGroup("/v1");

// Admin auth endpoints - rate limited for brute force protection
v1.MapAdminEndpoints().RequireRateLimiting("login");

// Authenticated endpoints - require valid API key or cookie
var authenticated = v1.RequireAuthorization().RequireRateLimiting("api");
authenticated.MapDomainEndpoints();
authenticated.MapEmailEndpoints();

// Admin-only endpoints - require admin actor type
var adminOnly = v1.RequireAuthorization("AdminOnly").RequireRateLimiting("api");
adminOnly.MapApiKeyEndpoints();
adminOnly.MapAuditEndpoints();

// Hangfire dashboard (development only)
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

// Schedule recurring domain verification job (every 5 minutes)
var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
recurringJobManager.AddOrUpdate<VerifyDomainsJob>(
    "verify-domains",
    job => job.ExecuteAsync(),
    "*/5 * * * *");

// SPA fallback - serve index.html for client-side routing
app.MapFallbackToFile("index.html");

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Application stopping, waiting for jobs to complete...");
});

await app.RunAsync();
