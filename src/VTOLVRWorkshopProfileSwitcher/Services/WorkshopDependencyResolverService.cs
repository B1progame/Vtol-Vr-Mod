using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VTOLVRWorkshopProfileSwitcher.Services;

public sealed class WorkshopDependencyResolverService
{
    private const string DisabledPrefix = "_OFF_";
    private const string ItemMetadataFileName = "item.json";
    private static readonly string[] DependencyPropertyNames =
    {
        "DependenciesIds",
        "DependencyIds",
        "Dependencies",
        "RequiredWorkshopIds",
        "RequiredIds"
    };

    public async Task<DependencyResolutionResult> ResolveAsync(
        string workshopPath,
        IReadOnlyCollection<string> requestedEnabledIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequested = requestedEnabledIds
            .Where(IsNumericId)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (!Directory.Exists(workshopPath))
        {
            return new DependencyResolutionResult(
                normalizedRequested,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var catalog = await BuildCatalogAsync(workshopPath, cancellationToken);
        var resolvedEnabledIds = new HashSet<string>(normalizedRequested, StringComparer.Ordinal);
        var autoEnabledDependencies = new HashSet<string>(StringComparer.Ordinal);
        var missingDependencies = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(normalizedRequested);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentId = queue.Dequeue();
            if (!visited.Add(currentId))
            {
                continue;
            }

            if (!catalog.DependenciesByWorkshopId.TryGetValue(currentId, out var dependencies))
            {
                continue;
            }

            foreach (var dependencyId in dependencies)
            {
                if (!catalog.AllKnownWorkshopIds.Contains(dependencyId))
                {
                    missingDependencies.Add(dependencyId);
                    continue;
                }

                if (resolvedEnabledIds.Add(dependencyId))
                {
                    autoEnabledDependencies.Add(dependencyId);
                    queue.Enqueue(dependencyId);
                }
            }
        }

        return new DependencyResolutionResult(
            resolvedEnabledIds,
            autoEnabledDependencies.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            missingDependencies.OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }

    private static async Task<DependencyCatalog> BuildCatalogAsync(string workshopPath, CancellationToken cancellationToken)
    {
        var allKnownWorkshopIds = new HashSet<string>(StringComparer.Ordinal);
        var dependenciesByWorkshopId = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        var directories = await Task.Run(() => Directory.EnumerateDirectories(workshopPath).ToList(), cancellationToken);
        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folderName = Path.GetFileName(directory);
            if (!TryGetWorkshopId(folderName, out var workshopId))
            {
                continue;
            }

            allKnownWorkshopIds.Add(workshopId);
            var itemJsonPath = Path.Combine(directory, ItemMetadataFileName);
            if (!File.Exists(itemJsonPath))
            {
                continue;
            }

            var dependencies = await ReadDependenciesAsync(itemJsonPath, cancellationToken);
            if (dependencies.Count > 0)
            {
                dependenciesByWorkshopId[workshopId] = dependencies;
            }
        }

        return new DependencyCatalog(allKnownWorkshopIds, dependenciesByWorkshopId);
    }

    private static async Task<IReadOnlySet<string>> ReadDependenciesAsync(string itemJsonPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(itemJsonPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var dependencies = new HashSet<string>(StringComparer.Ordinal);
            CollectDependencies(document.RootElement, dependencies);
            return dependencies;
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static void CollectDependencies(JsonElement element, HashSet<string> dependencies)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (DependencyPropertyNames.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                CollectDependencyIdsFromValue(property.Value, dependencies);
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                CollectDependencies(property.Value, dependencies);
            }
            else if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in property.Value.EnumerateArray())
                {
                    if (child.ValueKind == JsonValueKind.Object)
                    {
                        CollectDependencies(child, dependencies);
                    }
                }
            }
        }
    }

    private static void CollectDependencyIdsFromValue(JsonElement value, HashSet<string> dependencies)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    CollectDependencyIdsFromValue(item, dependencies);
                }

                break;
            case JsonValueKind.Number:
                if (value.TryGetInt64(out var number))
                {
                    dependencies.Add(number.ToString());
                }

                break;
            case JsonValueKind.String:
                var text = value.GetString();
                if (IsNumericId(text))
                {
                    dependencies.Add(text!);
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                    {
                        CollectDependencyIdsFromValue(property.Value, dependencies);
                    }
                }

                break;
        }
    }

    private static bool TryGetWorkshopId(string folderName, out string workshopId)
    {
        if (long.TryParse(folderName, out _))
        {
            workshopId = folderName;
            return true;
        }

        if (folderName.StartsWith(DisabledPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var suffix = folderName[DisabledPrefix.Length..];
            if (long.TryParse(suffix, out _))
            {
                workshopId = suffix;
                return true;
            }
        }

        workshopId = string.Empty;
        return false;
    }

    private static bool IsNumericId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.All(char.IsDigit);
    }

    private sealed record DependencyCatalog(
        IReadOnlySet<string> AllKnownWorkshopIds,
        IReadOnlyDictionary<string, IReadOnlySet<string>> DependenciesByWorkshopId);
}

public sealed record DependencyResolutionResult(
    IReadOnlySet<string> EnabledWorkshopIds,
    IReadOnlyList<string> AutoEnabledDependencyIds,
    IReadOnlyList<string> MissingDependencyIds);
