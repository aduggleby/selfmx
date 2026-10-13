using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Events;

namespace SelfMX.Api.Settings;

/// <summary>
/// Hosting concerns for running behind a reverse proxy in a hardened container:
/// rolling file logs, persisted DataProtection keys and trusted forwarded headers.
/// All are opt-in via configuration, so existing installs behave as before.
/// </summary>
public static class HostingSetup
{
    /// <summary>
    /// App:Logging:File:Enabled/Path/RetainedDays — daily files named selfmx-YYYYMMDD.log.
    /// Added as an extra provider, so console output and Logging:LogLevel filters stay as they are.
    /// </summary>
    public static void AddSelfMxFileLogging(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection("App:Logging:File");
        if (!section.GetValue<bool>("Enabled"))
            return;

        var path = section["Path"] is { Length: > 0 } configured ? configured : "/var/log/selfmx";
        var retainedDays = section.GetValue<int?>("RetainedDays") ?? 90;

        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Verbose)
            .WriteTo.File(
                Path.Combine(path, "selfmx-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retainedDays,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(2),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        builder.Logging.AddSerilog(logger, dispose: true);
    }

    /// <summary>
    /// App:DataProtectionKeysPath — persists the keys that protect admin session cookies,
    /// so sessions survive restarts on read-only containers.
    /// </summary>
    public static void AddSelfMxDataProtection(this WebApplicationBuilder builder)
    {
        var dataProtection = builder.Services.AddDataProtection().SetApplicationName("SelfMX");
        if (builder.Configuration["App:DataProtectionKeysPath"] is { Length: > 0 } path)
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(path));
    }

    /// <summary>
    /// App:TrustedProxies — comma-separated IPs or CIDR ranges of reverse proxies whose
    /// X-Forwarded-For/-Proto headers are trusted (client IPs for audit/rate limits, https for cookies).
    /// </summary>
    public static void AddSelfMxForwardedHeaders(this WebApplicationBuilder builder)
    {
        var trusted = (builder.Configuration["App:TrustedProxies"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var entry in trusted)
            {
                if (entry.Contains('/'))
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(entry));
                else
                    options.KnownProxies.Add(IPAddress.Parse(entry));
            }
        });
    }
}
