using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Amazon;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Hangfire;
using Hangfire.Console;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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

// In-memory log sink for remote diagnostics
var logSink = new InMemoryLogSink(maxEntries: 2000);
builder.Services.AddSingleton(logSink);
builder.Logging.AddProvider(new InMemoryLoggerProvider(logSink, LogLevel.Debug));

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
var useSqlite = builder.Environment.IsEnvironment("Test");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (useSqlite)
{
    if (string.IsNullOrWhiteSpace(connectionString) ||
        connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
    {
        connectionString = builder.Configuration["Test:SqliteConnectionString"]
            ?? "Data Source=SelfMxTestDb;Mode=Memory;Cache=Shared";
    }
}
else if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required");
}

if (useSqlite)
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connectionString));

    builder.Services.AddDbContext<AuditDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
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
}

var enableHangfire = !builder.Environment.IsEnvironment("Test");

// Hangfire with SQL Server and Console logging
if (enableHangfire)
{
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
        })
        .UseConsole());

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = Math.Min(Environment.ProcessorCount * 2, 20); // Cap at 20 workers
        options.Queues = ["default"];
    });
}
else
{
    builder.Services.AddSingleton<IBackgroundJobClient, NoopBackgroundJobClient>();
}

// Dual Authentication: Cookie (admin UI) + API Key (programmatic)
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "ApiKey";
        options.DefaultAuthenticateScheme = "ApiKey";
        options.DefaultChallengeScheme = "ApiKey";
        options.DefaultForbidScheme = "ApiKey";
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
    // Default policy: just require authenticated user (no specific claims)
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Admin-only policy: require ActorType=admin claim
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("ActorType", "admin"));
});

// Diagnostic handler to log authorization state (for debugging)
builder.Services.AddSingleton<IAuthorizationHandler, DiagnosticAuthorizationHandler>();

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
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiError.RateLimited.ToResendResponse(StatusCodes.Status429TooManyRequests),
            ct);
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
        await context.Response.WriteAsJsonAsync(
            ApiError.InternalError.ToResendResponse(StatusCodes.Status500InternalServerError));
    });
});

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    // Apply schema updates for SQL Server (EnsureCreated doesn't add new columns)
    await SchemaUpdater.UpdateAppSchemaAsync(db, startupLogger);

    var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await auditDb.Database.EnsureCreatedAsync();
    // Apply schema updates for audit database
    await SchemaUpdater.UpdateAuditSchemaAsync(auditDb, startupLogger);
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Serve the React UI from /ui to avoid collisions with API routes on the same domain.
// Default files enables /ui/ to serve index.html.
app.UseDefaultFiles(new DefaultFilesOptions
{
    RequestPath = "/ui"
});
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/ui"
});

// Health endpoint (no auth)
app.MapHealthChecks("/health");

// Root should just bounce to the UI entrypoint.
app.MapMethods("/", ["GET", "HEAD"], () => Results.Redirect("/ui/", permanent: false));

// System status endpoint - checks AWS, database connectivity (no auth, needed before login)
app.MapGet("/system/status", async (
    IAmazonSimpleEmailServiceV2 ses,
    AppDbContext db,
    IOptions<AwsSettings> awsSettings,
    ILogger<Program> logger) =>
{
    var issues = new List<string>();

    // Check AWS credentials and permissions
    try
    {
        // Try to get account details - this verifies credentials work
        var response = await ses.GetAccountAsync(new GetAccountRequest());
        logger.LogInformation("AWS SES account check passed");
    }
    catch (AmazonSimpleEmailServiceV2Exception ex)
    {
        logger.LogWarning(ex, "AWS SES check failed: {Message}", ex.Message);
        issues.Add($"AWS SES: {ex.Message}");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "AWS connectivity check failed");
        issues.Add($"AWS: Unable to connect - {ex.Message}");
    }

    // Check database connectivity
    try
    {
        await db.Database.CanConnectAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database connectivity check failed");
        issues.Add($"Database: {ex.Message}");
    }

    // Check if AWS settings are configured
    var aws = awsSettings.Value;
    if (string.IsNullOrEmpty(aws.Region))
    {
        issues.Add("AWS: Region not configured (Aws__Region)");
    }
    if (string.IsNullOrEmpty(aws.AccessKeyId))
    {
        issues.Add("AWS: Access Key ID not configured (Aws__AccessKeyId)");
    }
    if (string.IsNullOrEmpty(aws.SecretAccessKey))
    {
        issues.Add("AWS: Secret Access Key not configured (Aws__SecretAccessKey)");
    }

    return Results.Ok(new
    {
        healthy = issues.Count == 0,
        issues = issues.ToArray(),
        timestamp = DateTime.UtcNow
    });
});

// Version endpoint (no auth - needed by frontend)
app.MapGet("/system/version", () =>
{
    var version = typeof(Program).Assembly.GetName().Version;
    var versionString = version is not null
        ? $"{version.Major}.{version.Minor}.{version.Build}"
        : "unknown";

    return Results.Ok(new
    {
        version = versionString,
        buildDate = File.GetLastWriteTimeUtc(typeof(Program).Assembly.Location),
        environment = app.Environment.EnvironmentName
    });
});

// Logs endpoint (admin only - for remote diagnostics)
app.MapGet("/system/logs", (
    InMemoryLogSink sink,
    ClaimsPrincipal user,
    int count = 1000,
    string? level = null,
    string? category = null) =>
{
    // Require admin
    if (user.FindFirst("ActorType")?.Value != "admin")
    {
        return Results.Forbid();
    }

    var logs = sink.GetLogs(Math.Min(count, 2000));

    // Filter by level if specified
    if (!string.IsNullOrEmpty(level))
    {
        logs = logs.Where(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // Filter by category if specified
    if (!string.IsNullOrEmpty(category))
    {
        logs = logs.Where(l => l.Category.Contains(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    return Results.Ok(new
    {
        count = logs.Count,
        logs = logs.Select(l => new
        {
            timestamp = l.Timestamp,
            level = l.Level,
            category = l.Category,
            message = l.Message,
            exception = l.Exception
        })
    });
}).RequireAuthorization();

// Admin auth endpoints - rate limited for brute force protection
var root = app.MapGroup("");
root.MapAdminEndpoints().RequireRateLimiting("login");

// Authenticated endpoints - require valid API key or cookie (uses DefaultPolicy)
var authenticated = app.MapGroup("")
    .RequireAuthorization()
    .RequireRateLimiting("api");
authenticated.MapDomainEndpoints();
authenticated.MapEmailEndpoints();
authenticated.MapSentEmailEndpoints();

// Admin-only endpoints - require admin actor type (uses AdminOnly policy)
var adminOnly = app.MapGroup("")
    .RequireAuthorization("AdminOnly")
    .RequireRateLimiting("api");
adminOnly.MapApiKeyEndpoints();
adminOnly.MapAuditEndpoints();

// Hangfire dashboard - admin only, available in all environments
if (enableHangfire)
{
    app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
    {
        Authorization = [new HangfireAdminAuthorizationFilter()]
    });
}

if (enableHangfire)
{
    // Schedule recurring domain verification job (every 5 minutes)
    var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<VerifyDomainsJob>(
        "verify-domains",
        job => job.ExecuteAsync(null),
        "*/5 * * * *");

    // Schedule sent email cleanup job (daily at 3 AM UTC)
    recurringJobManager.AddOrUpdate<CleanupSentEmailsJob>(
        "cleanup-sent-emails",
        job => job.ExecuteAsync(CancellationToken.None, null),
        "0 3 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    // Schedule revoked API keys cleanup job (daily at 4 AM UTC)
    recurringJobManager.AddOrUpdate<CleanupRevokedApiKeysJob>(
        "cleanup-revoked-api-keys",
        job => job.ExecuteAsync(null),
        "0 4 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
}

// SPA fallback - serve the UI index only for /ui client-side routing.
app.MapFallbackToFile("/ui/{*path:nonfile}", "index.html");

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

/// <summary>
/// Hangfire dashboard authorization filter - requires admin cookie authentication.
/// </summary>
public class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        // Check if user is authenticated via cookie with admin ActorType
        return user.Identity?.IsAuthenticated == true
               && user.FindFirst("ActorType")?.Value == "admin";
    }
}
