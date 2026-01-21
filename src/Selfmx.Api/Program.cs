using Amazon;
using Amazon.SimpleEmailV2;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Selfmx.Api.Authentication;
using Selfmx.Api.Contracts.Responses;
using Selfmx.Api.Data;
using Selfmx.Api.Endpoints;
using Selfmx.Api.Jobs;
using Selfmx.Api.Services;
using Selfmx.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

// Settings
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));
builder.Services.Configure<AwsSettings>(builder.Configuration.GetSection("Aws"));
builder.Services.Configure<CloudflareSettings>(builder.Configuration.GetSection("Cloudflare"));

// SQLite with WAL mode
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=selfmx.db",
        sqlite => sqlite.CommandTimeout(30)));

// Hangfire with SQLite
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSQLiteStorage(builder.Configuration.GetConnectionString("HangfireConnection") ?? "selfmx-hangfire.db"));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 1; // Single worker for SQLite
    options.Queues = ["default"];
});

// Authentication
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);
builder.Services.AddAuthorization();

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

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod();
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

    // Enable WAL mode
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}

app.UseCors();
app.UseMiddleware<RateLimitMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Health endpoint (no auth)
app.MapHealthChecks("/health");
app.MapGet("/", () => new HealthResponse("ok", DateTime.UtcNow));

// API v1 routes
var v1 = app.MapGroup("/v1").RequireAuthorization();
v1.MapDomainEndpoints();
v1.MapEmailEndpoints();

// Hangfire dashboard (development only)
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

// Schedule recurring domain verification job (every 5 minutes)
RecurringJob.AddOrUpdate<VerifyDomainsJob>(
    "verify-domains",
    job => job.ExecuteAsync(),
    "*/5 * * * *");

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Application stopping, waiting for jobs to complete...");
});

await app.RunAsync();
