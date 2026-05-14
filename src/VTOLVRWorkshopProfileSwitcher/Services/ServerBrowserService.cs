using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VTOLVRWorkshopProfileSwitcher.Models;

namespace VTOLVRWorkshopProfileSwitcher.Services;

public sealed class ServerBrowserService
{
    private readonly SteamWorkshopInfoService _workshopInfoService = new();
    private readonly object _probeLock = new();
    private Process? _activeProbeProcess;

    public void StopActiveQuery()
    {
        Process? process;
        lock (_probeLock)
        {
            process = _activeProbeProcess;
            _activeProbeProcess = null;
        }

        TryKillProbeProcess(process);
        ServerBrowserSteamProbe.ShutdownSteamClient();
    }

    public async Task<ServerBrowserResult> LoadServersAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSteamRunning())
        {
            return new ServerBrowserResult
            {
                IsSteamRunning = false,
                StatusMessage = "Start Steam to view servers.",
                Servers = []
            };
        }

        var outputPath = Path.Combine(
            Path.GetTempPath(),
            "VTOLVRWorkshopProfileSwitcher",
            "server-probes",
            $"server-probe-{Guid.NewGuid():N}.json");

        try
        {
            var result = await RunProbeProcessAsync(outputPath, cancellationToken);
            var servers = result.Servers.ToList();
            await EnrichWorkshopDataAsync(servers, cancellationToken);

            return new ServerBrowserResult
            {
                IsSteamRunning = result.IsSteamRunning,
                StatusMessage = result.StatusMessage,
                Servers = servers
            };
        }
        finally
        {
            TryDeleteFile(outputPath);
        }
    }

    private async Task<ServerBrowserResult> RunProbeProcessAsync(string outputPath, CancellationToken cancellationToken)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return new ServerBrowserResult
            {
                IsSteamRunning = true,
                StatusMessage = "Server refresh failed: launcher path is unavailable.",
                Servers = []
            };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Path.GetTempPath());

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add(ServerBrowserProbeMode.Argument);
        process.StartInfo.ArgumentList.Add("--output");
        process.StartInfo.ArgumentList.Add(outputPath);

        process.Start();
        lock (_probeLock)
        {
            _activeProbeProcess = process;
        }

        using var cancellationRegistration = cancellationToken.Register(() => StopActiveQuery());

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StopActiveQuery();
            throw;
        }
        finally
        {
            lock (_probeLock)
            {
                if (ReferenceEquals(_activeProbeProcess, process))
                {
                    _activeProbeProcess = null;
                }
            }
        }

        if (!File.Exists(outputPath))
        {
            return new ServerBrowserResult
            {
                IsSteamRunning = true,
                StatusMessage = process.ExitCode == 0
                    ? "Server refresh failed: probe did not return data."
                    : $"Server refresh failed: probe exited with code {process.ExitCode}.",
                Servers = []
            };
        }

        await using var stream = File.OpenRead(outputPath);
        var result = await JsonSerializer.DeserializeAsync<ServerBrowserResult>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        return result ?? new ServerBrowserResult
        {
            IsSteamRunning = true,
            StatusMessage = "Server refresh failed: probe returned invalid data.",
            Servers = []
        };
    }

    private static void TryKillProbeProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(1500);
            }
        }
        catch
        {
            // Best-effort only.
        }
    }

    private static bool IsSteamRunning()
    {
        try
        {
            return Process.GetProcessesByName("steam").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task EnrichWorkshopDataAsync(List<VtolServerLobby> servers, CancellationToken cancellationToken)
    {
        var workshopIds = servers
            .SelectMany(server => server.Requirements.Select(requirement => requirement.WorkshopId))
            .Concat(servers
                .Select(server => server.ScenarioWorkshopId)
                .Where(id => !string.IsNullOrWhiteSpace(id)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (workshopIds.Count == 0)
        {
            return;
        }

        var info = await _workshopInfoService.GetItemInfoAsync(workshopIds, cancellationToken);
        foreach (var server in servers)
        {
            if (!string.IsNullOrWhiteSpace(server.ScenarioWorkshopId) &&
                info.TryGetValue(server.ScenarioWorkshopId, out var scenarioInfo))
            {
                server.ThumbnailPath = scenarioInfo.ThumbnailPath;
                if (string.IsNullOrWhiteSpace(server.ScenarioName) || server.ScenarioName == "Unknown Scenario")
                {
                    server.ScenarioName = scenarioInfo.Title;
                }
            }

            foreach (var requirement in server.Requirements)
            {
                if (!info.TryGetValue(requirement.WorkshopId, out var requirementInfo))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(requirement.Title) || requirement.Title == "Unknown Item")
                {
                    requirement.Title = requirementInfo.Title;
                }

                requirement.ThumbnailPath = requirementInfo.ThumbnailPath;
                server.ThumbnailPath ??= requirementInfo.ThumbnailPath;
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary probe files are best-effort cleanup.
        }
    }
}
