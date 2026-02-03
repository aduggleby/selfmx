using Hangfire.Console;
using Hangfire.Console.Progress;
using Hangfire.Server;

namespace SelfMX.Api.Jobs;

/// <summary>
/// Helper class for consistent console logging in Hangfire jobs.
/// Writes to both the ILogger and the Hangfire Console.
/// </summary>
public class JobConsole
{
    private readonly ILogger _logger;
    private readonly PerformContext? _context;

    public JobConsole(ILogger logger, PerformContext? context)
    {
        _logger = logger;
        _context = context;
    }

    public void WriteLine(string message)
    {
        _logger.LogInformation(message);
        _context?.WriteLine(message);
    }

    public void WriteInfo(string message)
    {
        _logger.LogInformation(message);
        _context?.WriteLine(ConsoleTextColor.Cyan, message);
    }

    public void WriteSuccess(string message)
    {
        _logger.LogInformation(message);
        _context?.WriteLine(ConsoleTextColor.Green, message);
    }

    public void WriteWarning(string message)
    {
        _logger.LogWarning(message);
        _context?.WriteLine(ConsoleTextColor.Yellow, $"[WARNING] {message}");
    }

    public void WriteError(string message)
    {
        _logger.LogError(message);
        _context?.WriteLine(ConsoleTextColor.Red, $"[ERROR] {message}");
    }

    public IProgressBar? WriteProgressBar()
    {
        return _context?.WriteProgressBar();
    }
}
