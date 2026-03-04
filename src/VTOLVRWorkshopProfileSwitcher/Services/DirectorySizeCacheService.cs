using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace VTOLVRWorkshopProfileSwitcher.Services;

public sealed class DirectorySizeCacheService
{
    private readonly ConcurrentDictionary<string, (DateTime LastWriteUtc, long SizeBytes)> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public long? GetDirectorySizeBytes(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return null;
        }

        DateTime stamp;
        try
        {
            stamp = Directory.GetLastWriteTimeUtc(path);
        }
        catch
        {
            stamp = DateTime.MinValue;
        }

        if (_cache.TryGetValue(path, out var entry) && entry.LastWriteUtc == stamp)
        {
            return entry.SizeBytes;
        }

        var size = ComputeDirectorySizeSafe(path);
        _cache[path] = (stamp, size);
        return size;
    }

    public static string FormatBytes(long? bytes)
    {
        if (!bytes.HasValue || bytes.Value < 0)
        {
            return "n/a";
        }

        double value = bytes.Value;
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        var format = unitIndex == 0 ? "0" : "0.##";
        return $"{value.ToString(format, System.Globalization.CultureInfo.InvariantCulture)} {units[unitIndex]}";
    }

    private static long ComputeDirectorySizeSafe(string rootPath)
    {
        long total = 0;
        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // Skip inaccessible files.
                }
            }

            IEnumerable<string> dirs;
            try
            {
                dirs = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var dir in dirs)
            {
                stack.Push(dir);
            }
        }

        return total;
    }
}
