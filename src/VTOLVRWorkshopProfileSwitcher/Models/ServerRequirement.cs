namespace VTOLVRWorkshopProfileSwitcher.Models;

public sealed class ServerRequirement
{
    public required string WorkshopId { get; set; }
    public string Title { get; set; } = "Unknown Item";
    public string Subtitle { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public bool IsInstalled { get; set; }
    public bool IsScenario { get; set; }
}
