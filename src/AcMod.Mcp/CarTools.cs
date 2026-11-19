using System.ComponentModel;
using System.Text.Json;
using AcMod.Core.Formats;
using AcMod.Core.Learning;
using AcMod.Core.Tuning;
using AcMod.Core.Workspaces;
using ModelContextProtocol.Server;

namespace AcMod.Mcp;

/// <summary>
/// The tools Claude Code can call (CC-3). Each opens the workspace from the app's settings, as the CLI does, calls Core, and returns JSON text.
/// A car whose data can't be read (V-13, such as a protected data.acd) returns only its status, never its bytes (CC-9).
/// </summary>
[McpServerToolType]
public static partial class CarTools
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [McpServerTool(Name = "list_cars"), Description("Lists the cars in the AC mod workspace: car ID, display name, size in bytes, and when it last changed (UTC).")]
    public static string ListCars() => Run(ws => ws.Cars().Select(c => new { c.Id, c.DisplayName, c.Size, c.LastChangedUtc }));

    [McpServerTool(Name = "read_car"), Description(
        "Reads a car's effective data, the data AC loads: data.acd when it exists, otherwise the data folder. Without fileName, returns the data source " +
        "and the list of data files. With fileName (such as engine.ini or power.lut), returns that file's text. Unreadable data returns only its status.")]
    public static string ReadCar([Description("The car ID, the car's folder name.")] string carId,
                                 [Description("A data file name, such as engine.ini. Leave empty to list the files.")] string? fileName = null) =>
        RunCar(carId, edit =>
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return new { carId, source = edit.Data.Source.ToString(), dataFolderIgnored = edit.Data.FolderIgnored,
                             files = edit.Data.Files.Select(f => new { f.Name, size = f.Data.Length }) };
            }

            var name = edit.FileName(fileName);
            return name == null ? new { error = $"{carId} has no data file {fileName}." } : new { carId, file = name, text = edit.Text(name) };
        });

    [McpServerTool(Name = "validate"), Description("Runs the app's validator on a car: each finding's rule (V-01 to V-16), severity, file, message, fix, and evidence label.")]
    public static string Validate([Description("The car ID.")] string carId) =>
        Run(ws =>
        {
            var (findings, error) = ws.Validate(carId);
            return findings == null ? new { error }
                : findings.Select(f => new { f.Rule, severity = f.Severity.ToString(), f.File, f.Message, f.Fix, evidence = f.Evidence.ToString() });
        });

    [McpServerTool(Name = "gears"), Description(
        "Calculates a car's top speed in each gear at the rev limiter, from its gear ratios, final drive, and tyre radii, as the app's Gears tab does.")]
    public static string Gears([Description("The car ID.")] string carId) =>
        RunCar(carId, edit =>
        {
            var (table, error) = GearTable.FromData(edit.Data.Files);
            return table == null ? new { error } : table;
        });

    [McpServerTool(Name = "list_snapshots"), Description("Lists a car's snapshots, oldest first. Every change in the app takes one before writing.")]
    public static string ListSnapshots([Description("The car ID.")] string carId) =>
        Run(ws => ws.Snapshots(carId).Select(id => new { snapshotId = id, takenUtc = Workspace.SnapshotTimeUtc(id) }));

    [McpServerTool(Name = "compare_snapshots"), Description(
        "Lists the data fields that differ between a snapshot and the car as it is now, or between two snapshots. Each change gives the file, field, before and after.")]
    public static string CompareSnapshots([Description("The car ID.")] string carId,
                                          [Description("The older snapshot ID, from list_snapshots.")] string snapshotId,
                                          [Description("A newer snapshot ID. Leave empty to compare with the car as it is now.")] string? otherSnapshotId = null) =>
        RunCar(carId, (ws, edit) =>
        {
            var (before, beforeError) = Snapshot(ws, carId, snapshotId);
            if (before == null) return new { error = beforeError };
            if (string.IsNullOrEmpty(otherSnapshotId)) return DataDiff.Compare(before.Files, edit.Data.Files);
            var (after, afterError) = Snapshot(ws, carId, otherSnapshotId);
            return after == null ? new { error = afterError } : DataDiff.Compare(before.Files, after.Files);
        });

    /// <summary>A snapshot's data, or an error when it doesn't exist or can't be read (CC-9: only its status).</summary>
    private static (AcMod.Core.Acd.CarData? Data, string? Error) Snapshot(Workspace ws, string carId, string snapshotId)
    {
        var (data, error) = ws.SnapshotData(carId, snapshotId);
        return data == null ? (null, error) : data.Error != null ? (null, $"Snapshot {snapshotId}'s data can't be read: {data.Error}") : (data, null);
    }

    [McpServerTool(Name = "read_ac_log"), Description(
        "Reads AC's latest log (Documents\\Assetto Corsa\\logs\\log.txt): the car driven, its logged power next to the app's estimate, whether AC exited cleanly, " +
        "and the highlighted lines (likely problems, and known harmless ones with a note). Read it right after the owner quits AC; the next start overwrites it.")]
    public static string ReadAcLog() => Run(ws =>
    {
        if (!File.Exists(AcLog.DefaultPath)) return new { error = $"No AC log at {AcLog.DefaultPath}." };
        var report = AcLog.Read(AcLog.DefaultPath);
        double? estimate = report.CarId != null && ws.OpenCar(report.CarId).Edit is { ReadOnly: false } edit
            ? PowerEstimate.FromData(edit.Data.Files).Estimate?.PeakPowerW
            : null;
        return new
        {
            path = AcLog.DefaultPath, report.WrittenUtc, report.CarId, loggedPowerW = report.PowerW, workspaceEstimateW = estimate, report.CleanExit,
            lines = report.Lines.Select(l => new { l.Number, l.Text, kind = l.Kind.ToString(), l.Note }),
        };
    });

    /// <summary>Runs a tool on the workspace and returns its result as JSON, or {"error": ...} when the workspace or a file can't be used.</summary>
    private static string Run(Func<Workspace, object?> tool)
    {
        try
        {
            var (ws, error) = Open();
            return JsonSerializer.Serialize(ws == null ? new { error } : tool(ws), Json);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return JsonSerializer.Serialize(new { error = e.Message }, Json);
        }
    }

    /// <summary>Like <see cref="Run"/> for one car. Unreadable data returns only the car's status (CC-9), so no tool sees protected content.</summary>
    private static string RunCar(string carId, Func<Workspace, CarEdit, object?> tool) => Run(ws =>
    {
        var (edit, error) = ws.OpenCar(carId);
        if (edit == null) return new { error };
        return edit.ReadOnly
            ? new { carId, status = "unreadable", detail = $"The car's data can't be read, so it opens read-only and no tool shows it: {edit.Data.Error}" }
            : tool(ws, edit);
    });

    private static string RunCar(string carId, Func<CarEdit, object?> tool) => RunCar(carId, (_, edit) => tool(edit));

    /// <summary>Opens the workspace for each call. Tests point it at a temporary workspace; otherwise it's the app's (<see cref="OpenFromSettings"/>).</summary>
    internal static Func<(Workspace? Workspace, string? Error)> Open = OpenFromSettings;

    /// <summary>The workspace the app and CLI use, from %LOCALAPPDATA%\AcMod\settings.json.</summary>
    private static (Workspace? Workspace, string? Error) OpenFromSettings()
    {
        var (settings, settingsError) = Settings.Load(Settings.DefaultPath);
        if (settingsError != null || settings.WorkspaceFolder == null) return (null, settingsError ?? "No workspace yet. The owner sets one in the app or with: acmod setup <folder>");
        return Workspace.Open(settings.WorkspaceFolder, settings.AcFolder);
    }
}
