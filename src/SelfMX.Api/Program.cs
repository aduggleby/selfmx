using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Amazon;
using Amazon.SimpleEmailV2;
using Hangfire;
using Hangfire.SqlServer;
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

// Configure JSON serialization for consistent datetime format (ISO 8601 with Z)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new UtcDateTimeConverter());
    options.SerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Settings
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));
builder.Services.Configure<AwsSettings>(builder.Configuration.GetSection("Aws"));
builder.Services.Configure<CloudflareSettings>(builder.Configuration.GetSection("Cloudflare"));

// SQL Server database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        sql.CommandTimeout(30);
    }));

builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        sql.CommandTimeout(30);
    }));

// Hangfire with SQL Server
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Math.Min(Environment.ProcessorCount * 2, 20); // Cap at 20 workers
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

// CORS for frontend - derive from Fqdn or use development default
var fqdn = builder.Configuration.GetValue<string>("App:Fqdn");
var corsOrigins = !string.IsNullOrEmpty(fqdn)
    ? new[] { $"https://{fqdn}" }
    : new[] { "http://localhost:17401" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
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

    var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await auditDb.Database.EnsureCreatedAsync();
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

// Sent emails - authenticated users can view their domain's emails, admin can view all
authenticated.MapSentEmailEndpoints();

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

// Schedule sent email cleanup job (daily at 3 AM UTC)
recurringJobManager.AddOrUpdate<CleanupSentEmailsJob>(
    "cleanup-sent-emails",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 3 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

// SPA fallback - serve index.html for client-side routing
app.MapFallbackToFile("index.html");

// Startup banner and diagnostics
var version = typeof(Program).Assembly.GetName().Version;
var appSettings = app.Services.GetRequiredService<IOptions<AppSettings>>().Value;

const string banner = """

 ███████╗███████╗██╗     ███████╗███╗   ███╗██╗  ██╗
 ██╔════╝██╔════╝██║     ██╔════╝████╗ ████║╚██╗██╔╝
 ███████╗█████╗  ██║     █████╗  ██╔████╔██║ ╚███╔╝
 ╚════██║██╔══╝  ██║     ██╔══╝  ██║╚██╔╝██║ ██╔██╗
 ███████║███████╗███████╗██║     ██║ ╚═╝ ██║██╔╝ ██╗
 ╚══════╝╚══════╝╚══════╝╚═╝     ╚═╝     ╚═╝╚═╝  ╚═╝

""";

Console.WriteLine(banner);
app.Logger.LogInformation("SelfMX v{Version} starting on {Fqdn}", version?.ToString(3) ?? "unknown", appSettings.Fqdn);

// Debug: log App__ environment variables to diagnose configuration issues
var appEnvVars = Environment.GetEnvironmentVariables()
    .Cast<System.Collections.DictionaryEntry>()
    .Where(e => e.Key.ToString()!.StartsWith("App", StringComparison.OrdinalIgnoreCase))
    .Select(e => $"{e.Key}={MaskValue(e.Key.ToString()!, e.Value?.ToString())}")
    .ToList();
app.Logger.LogInformation("Environment: {Vars}", string.Join(", ", appEnvVars));
app.Logger.LogInformation("AdminPasswordHash: {Status}",
    !string.IsNullOrEmpty(appSettings.AdminPasswordHash)
        ? $"configured ({appSettings.AdminPasswordHash.Length} chars)"
        : "NOT CONFIGURED");

static string MaskValue(string key, string? value)
{
    if (string.IsNullOrEmpty(value)) return "(empty)";
    if (key.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("Key", StringComparison.OrdinalIgnoreCase))
    {
        return value.Length > 10 ? $"{value[..10]}...({value.Length} chars)" : $"***({value.Length} chars)";
    }
    return value;
}

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Application stopping, waiting for jobs to complete...");
});

await app.RunAsync();

/// <summary>
/// JSON converter that ensures all DateTime values are serialized as UTC with Z suffix
/// to match Zod's datetime() schema expectation.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateTime = reader.GetDateTime();
        return dateTime.Kind == DateTimeKind.Utc ? dateTime : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Ensure the datetime is treated as UTC and formatted with Z suffix
        var utcValue = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        writer.WriteStringValue(utcValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}

/// <summary>
/// JSON converter for nullable DateTime values.
/// </summary>
public class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var dateTime = reader.GetDateTime();
        return dateTime.Kind == DateTimeKind.Utc ? dateTime : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }
        var utcValue = value.Value.Kind == DateTimeKind.Utc
            ? value.Value
            : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        writer.WriteStringValue(utcValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}
