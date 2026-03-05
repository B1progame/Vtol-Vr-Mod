using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VTOLVRWorkshopProfileSwitcher.Services;

public enum AppLogLevel
{
    Info,
    Warning,
    Error
}

public sealed class AppLogger
{
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AppLogger(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task LogAsync(string message, CancellationToken cancellationToken = default)
    {
        await LogAsync(InferLevel(message), message, cancellationToken);
    }

    public async Task LogAsync(AppLogLevel level, string message, CancellationToken cancellationToken = default)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{ToLabel(level)}] {message}{Environment.NewLine}";

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_paths.LogFile, line, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task InfoAsync(string message, CancellationToken cancellationToken = default)
        => LogAsync(AppLogLevel.Info, message, cancellationToken);

    public Task WarnAsync(string message, CancellationToken cancellationToken = default)
        => LogAsync(AppLogLevel.Warning, message, cancellationToken);

    public Task ErrorAsync(string message, CancellationToken cancellationToken = default)
        => LogAsync(AppLogLevel.Error, message, cancellationToken);

    private static AppLogLevel InferLevel(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return AppLogLevel.Info;
        }

        if (ContainsAny(message, "error", "failed", "exception", "crash", "fatal"))
        {
            return AppLogLevel.Error;
        }

        if (ContainsAny(message, "warning", "warn", "missing", "skip", "canceled"))
        {
            return AppLogLevel.Warning;
        }

        return AppLogLevel.Info;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        foreach (var term in terms)
        {
            if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ToLabel(AppLogLevel level)
    {
        return level switch
        {
            AppLogLevel.Warning => "WARNING",
            AppLogLevel.Error => "ERROR",
            _ => "INFO"
        };
    }
}
