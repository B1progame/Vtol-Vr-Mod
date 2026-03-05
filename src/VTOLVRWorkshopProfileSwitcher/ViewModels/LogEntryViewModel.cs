using System;
using Avalonia.Media;
using VTOLVRWorkshopProfileSwitcher.Services;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed class LogEntryViewModel
{
    public string SourceFile { get; }
    public string Text { get; }
    public DateTime? Timestamp { get; }
    public string TimestampText { get; }
    public AppLogLevel Level { get; }
    public string LevelLabel { get; }
    public string MessageText { get; }
    public string TerminalLine { get; }
    public bool IsWarning => Level == AppLogLevel.Warning;
    public bool IsInfo => Level == AppLogLevel.Info;
    public bool IsError => Level == AppLogLevel.Error;
    public IBrush LevelBackgroundBrush { get; }
    public IBrush LevelForegroundBrush { get; }
    public IBrush MessageBrush { get; }
    public IBrush BorderBrush { get; }

    public LogEntryViewModel(string text, string sourceFile)
    {
        Text = text;
        SourceFile = sourceFile;

        ParseLine(text, out var timestamp, out var level, out var message);
        Timestamp = timestamp;
        TimestampText = timestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
        Level = level;
        LevelLabel = level switch
        {
            AppLogLevel.Warning => "WARNING",
            AppLogLevel.Error => "ERROR",
            _ => "INFO"
        };
        MessageText = string.IsNullOrWhiteSpace(message) ? text : message;
        TerminalLine = $"[{TimestampText}] [{LevelLabel}] {MessageText}";

        if (level == AppLogLevel.Error)
        {
            LevelBackgroundBrush = Brush.Parse("#A01F2A");
            LevelForegroundBrush = Brush.Parse("#FFF3F4");
            MessageBrush = Brush.Parse("#FFC4CA");
            BorderBrush = Brush.Parse("#5E1A20");
            return;
        }

        if (level == AppLogLevel.Warning)
        {
            LevelBackgroundBrush = Brush.Parse("#9E6D11");
            LevelForegroundBrush = Brush.Parse("#FFF5DE");
            MessageBrush = Brush.Parse("#FFE7B5");
            BorderBrush = Brush.Parse("#5C451A");
            return;
        }

        LevelBackgroundBrush = Brush.Parse("#1C4A8C");
        LevelForegroundBrush = Brush.Parse("#EAF4FF");
        MessageBrush = Brush.Parse("#CBE6FF");
        BorderBrush = Brush.Parse("#204066");
    }

    private static void ParseLine(string line, out DateTime? timestamp, out AppLogLevel level, out string message)
    {
        timestamp = null;
        level = AppLogLevel.Info;
        message = line ?? string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var trimmed = line.Trim();
        var firstClose = trimmed.IndexOf(']');
        if (trimmed.StartsWith("[", StringComparison.Ordinal) &&
            firstClose > 1 &&
            DateTime.TryParse(trimmed[1..firstClose], out var parsedTimestamp))
        {
            timestamp = parsedTimestamp;
            trimmed = trimmed[(firstClose + 1)..].TrimStart();
        }

        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            var levelClose = trimmed.IndexOf(']');
            if (levelClose > 1)
            {
                var token = trimmed[1..levelClose].Trim();
                level = token.Equals("ERROR", StringComparison.OrdinalIgnoreCase)
                    ? AppLogLevel.Error
                    : token.Equals("WARNING", StringComparison.OrdinalIgnoreCase) || token.Equals("WARN", StringComparison.OrdinalIgnoreCase)
                        ? AppLogLevel.Warning
                        : AppLogLevel.Info;
                trimmed = trimmed[(levelClose + 1)..].TrimStart();
            }
        }

        if (level == AppLogLevel.Info)
        {
            if (ContainsAny(trimmed, "error", "failed", "exception", "crash", "fatal"))
            {
                level = AppLogLevel.Error;
            }
            else if (ContainsAny(trimmed, "warning", "warn", "missing", "skip", "canceled"))
            {
                level = AppLogLevel.Warning;
            }
        }

        message = trimmed;
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
}
