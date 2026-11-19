using System.ComponentModel;
using System.Text.Json;
using AcMod.Core.Workspaces;
using ModelContextProtocol.Server;

namespace AcMod.Mcp;

/// <summary>The tools Claude Code can call. Each opens the workspace from the app's settings, as the CLI does, and returns JSON text.</summary>
[McpServerToolType]
public static class CarTools
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [McpServerTool(Name = "list_cars"), Description("Lists the cars in the AC mod workspace: car ID, display name, size in bytes, and when it last changed (UTC).")]
    public static string ListCars()
    {
        var (ws, error) = Open();
        return ws == null ? Error(error!) : JsonSerializer.Serialize(
            ws.Cars().Select(c => new { c.Id, c.DisplayName, c.Size, c.LastChangedUtc }), Json);
    }

    /// <summary>The workspace the app and CLI use, from %LOCALAPPDATA%\AcMod\settings.json.</summary>
    private static (Workspace? Workspace, string? Error) Open()
    {
        var (settings, settingsError) = Settings.Load(Settings.DefaultPath);
        if (settingsError != null || settings.WorkspaceFolder == null) return (null, settingsError ?? "No workspace yet. The owner sets one in the app or with: acmod setup <folder>");
        return Workspace.Open(settings.WorkspaceFolder, settings.AcFolder);
    }

    private static string Error(string message) => JsonSerializer.Serialize(new { error = message }, Json);
}
