using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VTOLVRWorkshopProfileSwitcher.Models;

namespace VTOLVRWorkshopProfileSwitcher.Services;

internal static class ServerBrowserProbeMode
{
    public const string Argument = "--server-browser-probe";

    public static bool IsRequested(string[] args)
    {
        return args.Any(arg => string.Equals(arg, Argument, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var outputPath = GetOutputPath(args);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return 2;
        }

        try
        {
            var result = await ServerBrowserSteamProbe.LoadServersAsync();
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            await using var stream = File.Create(outputPath);
            await JsonSerializer.SerializeAsync(stream, result, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            return 0;
        }
        catch (Exception ex)
        {
            var result = new ServerBrowserResult
            {
                IsSteamRunning = true,
                StatusMessage = $"Server refresh failed: {ex.Message}",
                Servers = []
            };

            try
            {
                await using var stream = File.Create(outputPath);
                await JsonSerializer.SerializeAsync(stream, result);
            }
            catch
            {
                // Ignore secondary write failures.
            }

            return 1;
        }
        finally
        {
            ServerBrowserSteamProbe.ShutdownSteamClient();
        }
    }

    private static string GetOutputPath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--output", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }
}
