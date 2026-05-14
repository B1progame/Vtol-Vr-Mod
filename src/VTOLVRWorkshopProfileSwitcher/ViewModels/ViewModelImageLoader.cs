using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

internal static class ViewModelImageLoader
{
    private const int DefaultThumbnailDecodeWidth = 384;
    private const int MaxCachedBitmaps = 96;

    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, CacheEntry> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> LruKeys = new();

    public static BitmapLease? TryAcquireBitmap(string? path, int decodeWidth = DefaultThumbnailDecodeWidth)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                path = uri.LocalPath;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            var normalizedPath = Path.GetFullPath(path);
            var key = $"{decodeWidth}|{normalizedPath}";

            lock (SyncRoot)
            {
                if (Entries.TryGetValue(key, out var existing))
                {
                    existing.RefCount++;
                    TouchEntry(existing);
                    return new BitmapLease(key, existing.Bitmap, ReleaseBitmap);
                }
            }

            using var stream = File.OpenRead(normalizedPath);
            var bitmap = decodeWidth > 0
                ? Bitmap.DecodeToWidth(stream, decodeWidth)
                : new Bitmap(stream);

            lock (SyncRoot)
            {
                if (Entries.TryGetValue(key, out var racedExisting))
                {
                    racedExisting.RefCount++;
                    TouchEntry(racedExisting);
                    bitmap.Dispose();
                    return new BitmapLease(key, racedExisting.Bitmap, ReleaseBitmap);
                }

                var entry = new CacheEntry(bitmap, key)
                {
                    RefCount = 1
                };

                entry.LruNode = LruKeys.AddLast(key);
                Entries[key] = entry;
                EvictIfNeeded();
                return new BitmapLease(key, bitmap, ReleaseBitmap);
            }
        }
        catch
        {
            return null;
        }
    }

    public static void DisposeAll()
    {
        lock (SyncRoot)
        {
            foreach (var entry in Entries.Values)
            {
                entry.Bitmap.Dispose();
            }

            Entries.Clear();
            LruKeys.Clear();
        }
    }

    private static void ReleaseBitmap(string key)
    {
        lock (SyncRoot)
        {
            if (!Entries.TryGetValue(key, out var entry))
            {
                return;
            }

            if (entry.RefCount > 0)
            {
                entry.RefCount--;
            }

            TouchEntry(entry);
            EvictIfNeeded();
        }
    }

    private static void EvictIfNeeded()
    {
        while (Entries.Count > MaxCachedBitmaps)
        {
            var node = LruKeys.First;
            var removedAny = false;

            while (node is not null)
            {
                var next = node.Next;
                var key = node.Value;

                if (Entries.TryGetValue(key, out var entry) && entry.RefCount == 0)
                {
                    Entries.Remove(key);
                    LruKeys.Remove(node);
                    entry.Bitmap.Dispose();
                    removedAny = true;
                    break;
                }

                node = next;
            }

            if (!removedAny)
            {
                break;
            }
        }
    }

    private static void TouchEntry(CacheEntry entry)
    {
        if (entry.LruNode is null)
        {
            entry.LruNode = LruKeys.AddLast(entry.Key);
            return;
        }

        if (entry.LruNode.List is not null)
        {
            LruKeys.Remove(entry.LruNode);
        }

        entry.LruNode = LruKeys.AddLast(entry.Key);
    }

    private sealed class CacheEntry
    {
        public CacheEntry(Bitmap bitmap, string key)
        {
            Bitmap = bitmap;
            Key = key;
        }

        public string Key { get; }
        public Bitmap Bitmap { get; }
        public int RefCount { get; set; }
        public LinkedListNode<string>? LruNode { get; set; }
    }

    internal sealed class BitmapLease : IDisposable
    {
        private readonly string _key;
        private readonly Action<string> _release;
        private bool _disposed;

        public BitmapLease(string key, Bitmap bitmap, Action<string> release)
        {
            _key = key;
            Bitmap = bitmap;
            _release = release;
        }

        public Bitmap Bitmap { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _release(_key);
        }
    }
}
