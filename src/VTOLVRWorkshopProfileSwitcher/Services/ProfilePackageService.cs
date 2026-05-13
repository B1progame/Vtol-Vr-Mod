using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Material.Icons;
using VTOLVRWorkshopProfileSwitcher.Models;

namespace VTOLVRWorkshopProfileSwitcher.Services;

public sealed class ProfilePackageService
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task ExportAsync(
        Stream destination,
        string packageName,
        IEnumerable<ModProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        var profileDocs = profiles
            .Select(profile => new ProfilePackageProfile
            {
                Name = profile.Name,
                Notes = profile.Notes,
                IconKind = NormalizeIconKind(profile.IconKind),
                EnabledWorkshopIds = profile.EnabledMods
                    .Where(IsNumericWorkshopId)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                IncludedWorkshopIds = (profile.IncludedMods.Count == 0 ? profile.EnabledMods : profile.IncludedMods)
                    .Where(IsNumericWorkshopId)
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            })
            .ToList();

        var document = new ProfilePackageDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            PackageName = string.IsNullOrWhiteSpace(packageName) ? "VTOL VR Profiles" : packageName.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            Profiles = profileDocs
        };

        destination.SetLength(0);
        await JsonSerializer.SerializeAsync(destination, document, JsonOptions, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    public async Task<ProfilePackageImportResult> ImportAsync(
        Stream source,
        IReadOnlyCollection<ModProfile> existingProfiles,
        ProfileImportConflictPolicy conflictPolicy,
        CancellationToken cancellationToken = default)
    {
        var document = await JsonSerializer.DeserializeAsync<ProfilePackageDocument>(source, JsonOptions, cancellationToken);
        if (document is null)
        {
            throw new InvalidDataException("Package file is empty or invalid JSON.");
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported schemaVersion '{document.SchemaVersion}'. Supported version is {CurrentSchemaVersion}.");
        }

        if (document.Profiles.Count == 0)
        {
            throw new InvalidDataException("Package does not contain any profiles.");
        }

        var now = DateTime.UtcNow;
        var result = new ProfilePackageImportResult();
        var knownNames = existingProfiles
            .Select(profile => profile.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var profileDocument in document.Profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedName = profileDocument.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                result.InvalidProfileCount++;
                continue;
            }

            var normalizedIds = new List<string>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var workshopId in profileDocument.EnabledWorkshopIds ?? new List<string>())
            {
                if (!IsNumericWorkshopId(workshopId))
                {
                    result.RemovedInvalidWorkshopIdsCount++;
                    continue;
                }

                if (seenIds.Add(workshopId))
                {
                    normalizedIds.Add(workshopId);
                }
            }

            var normalizedIncludedIds = new List<string>();
            var seenIncludedIds = new HashSet<string>(StringComparer.Ordinal);
            var includedSource = profileDocument.IncludedWorkshopIds?.Count > 0
                ? profileDocument.IncludedWorkshopIds
                : profileDocument.EnabledWorkshopIds;
            foreach (var workshopId in includedSource ?? new List<string>())
            {
                if (!IsNumericWorkshopId(workshopId))
                {
                    result.RemovedInvalidWorkshopIdsCount++;
                    continue;
                }

                if (seenIncludedIds.Add(workshopId))
                {
                    normalizedIncludedIds.Add(workshopId);
                }
            }

            var finalName = normalizedName;
            if (knownNames.Contains(normalizedName))
            {
                switch (conflictPolicy)
                {
                    case ProfileImportConflictPolicy.Skip:
                        result.SkippedCount++;
                        continue;
                    case ProfileImportConflictPolicy.Overwrite:
                        result.OverwrittenCount++;
                        break;
                    case ProfileImportConflictPolicy.Rename:
                        finalName = BuildUniqueImportedName(normalizedName, knownNames);
                        result.RenamedCount++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(conflictPolicy), conflictPolicy, null);
                }
            }

            knownNames.Add(finalName);
            result.ImportedProfiles.Add(new ModProfile
            {
                Name = finalName,
                Notes = profileDocument.Notes?.Trim() ?? string.Empty,
                IconKind = NormalizeIconKind(profileDocument.IconKind),
                EnabledMods = normalizedIds,
                IncludedMods = normalizedIncludedIds,
                CreatedAt = now
            });
        }

        result.ImportedCount = result.ImportedProfiles.Count;
        return result;
    }

    private static string BuildUniqueImportedName(string baseName, IReadOnlySet<string> existingNames)
    {
        var candidate = $"{baseName} (Imported)";
        if (!existingNames.Contains(candidate))
        {
            return candidate;
        }

        var suffix = 2;
        while (true)
        {
            candidate = $"{baseName} (Imported {suffix})";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static bool IsNumericWorkshopId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit);
    }

    private static string NormalizeIconKind(string? iconKind) => Enum.TryParse<MaterialIconKind>(iconKind, out var parsed)
        ? parsed.ToString()
        : "AccountGroup";
}
