using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VTOLVRWorkshopProfileSwitcher.Services;

public sealed class SteamWorkshopInfoService
{
    private const string SteamWorkshopApi = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
    private static readonly HttpClient HttpClient = new();
    private static readonly string ThumbnailCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VTOLVRWorkshopProfileSwitcher",
        "thumbnail-cache");

    public async Task<Dictionary<string, WorkshopItemInfo>> GetItemInfoAsync(
        IReadOnlyList<string> workshopIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, WorkshopItemInfo>(StringComparer.Ordinal);
        var ids = workshopIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
        {
            return result;
        }

        const int batchSize = 40;
        for (var i = 0; i < ids.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = ids.Skip(i).Take(batchSize).ToList();

            try
            {
                EnsureHttpDefaults();

                var form = new List<KeyValuePair<string, string>>
                {
                    new("itemcount", batch.Count.ToString())
                };

                for (var j = 0; j < batch.Count; j++)
                {
                    form.Add(new($"publishedfileids[{j}]", batch[j]));
                }

                using var content = new FormUrlEncodedContent(form);
                using var response = await HttpClient.PostAsync(SteamWorkshopApi, content, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!document.RootElement.TryGetProperty("response", out var responseRoot) ||
                    !responseRoot.TryGetProperty("publishedfiledetails", out var detailsArray) ||
                    detailsArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var detail in detailsArray.EnumerateArray())
                {
                    var id = FindStringByKeyRecursive(detail, 0, "publishedfileid");
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    var title = FindStringByKeyRecursive(detail, 0, "title") ?? $"Workshop Item {id}";
                    var previewUrl = FindStringByKeyRecursive(detail, 0, "preview_url");
                    var thumbnailPath = await ResolveThumbnailAsync(id, previewUrl, cancellationToken);

                    result[id] = new WorkshopItemInfo
                    {
                        WorkshopId = id,
                        Title = title,
                        PreviewUrl = previewUrl ?? string.Empty,
                        ThumbnailPath = thumbnailPath
                    };
                }
            }
            catch
            {
                // A missing thumbnail or HTTP failure should not break the server browser.
            }
        }

        return result;
    }

    private static async Task<string?> ResolveThumbnailAsync(
        string workshopId,
        string? previewUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(previewUrl) ||
            !Uri.TryCreate(previewUrl, UriKind.Absolute, out var previewUri))
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(ThumbnailCacheDir);

            var extension = Path.GetExtension(previewUri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var cachePath = Path.Combine(ThumbnailCacheDir, $"server-{workshopId}{extension}");
            if (!File.Exists(cachePath) || new FileInfo(cachePath).Length == 0)
            {
                var bytes = await HttpClient.GetByteArrayAsync(previewUri, cancellationToken);
                if (!LooksLikeImage(bytes))
                {
                    return null;
                }

                await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
            }

            return cachePath;
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureHttpDefaults()
    {
        if (HttpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            HttpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("VTOLVRWorkshopProfileSwitcher", "1.1"));
        }
    }

    private static string? FindStringByKeyRecursive(JsonElement element, int depth, params string[] names)
    {
        if (depth > 6)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    var value = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                var nested = FindStringByKeyRecursive(property.Value, depth + 1, names);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindStringByKeyRecursive(item, depth + 1, names);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static bool LooksLikeImage(byte[] bytes)
    {
        if (bytes.Length < 12)
        {
            return false;
        }

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return true;
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return true;
        }

        return bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
               bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;
    }
}

public sealed class WorkshopItemInfo
{
    public required string WorkshopId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string PreviewUrl { get; init; } = string.Empty;
    public string? ThumbnailPath { get; init; }
}
