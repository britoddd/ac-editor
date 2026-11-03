using System.IO.Compression;
using AcMod.Core.Acd;

namespace AcMod.Core.Workspaces;

// PK-2, the fallback WS-11 names for when installing straight into content\cars fails (2026-11-03: an install kept being refused).
public sealed partial class Workspace
{
    /// <summary>
    /// Files left out of an export: the app's install marker, and the development files of V-15 (PK-3).
    /// texture\NULL.dds is kept, because whether a model needs it is not verified.
    /// </summary>
    private static readonly string[] ExportSkips = [".blend", ".blend1", ".fbx", ".psd", ".tmp", ".bak"];

    /// <summary>
    /// Writes exports\&lt;car ID&gt;_&lt;UTC time&gt;.zip with the car under a top-level &lt;car ID&gt;\ folder (PK-2), which Content Manager installs
    /// when the ZIP is dropped on it (community tutorial, not verified here). Refuses a car whose data can't be read, as install does.
    /// The ZIP is written to a .tmp file first and renamed, so a half-written ZIP never appears. Validation errors refuse it unless ignoreErrors (PK-4).
    /// </summary>
    /// <returns>The ZIP's full path.</returns>
    /// <param name="ignoreErrors">True when the user chose to export despite validation errors (PK-4). Unreadable data is still refused.</param>
    public (string? ZipPath, string? Error) ExportZip(string carId, bool ignoreErrors = false)
    {
        var (car, error) = CarFile(carId, ".");
        if (car == null) return (null, error);
        if (!Directory.Exists(car)) return (null, $"There is no car {carId} in the workspace.");
        var data = CarData.Load(car);
        if (data.Error != null) return (null, $"{carId} can't be exported, because its data can't be read: {data.Error}");
        if (!ignoreErrors && ValidationBlock(carId) is { } block) return (null, block);

        var name = UniqueName(Path.Combine(Root, "exports"), carId + "_") + ".zip";
        var (zipPath, zipError) = Resolve(Path.Combine("exports", name));
        if (zipPath == null) return (null, zipError);

        var temp = zipPath + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.EnumerateFiles(car, "*", NoLinks))
                {
                    var relative = Path.GetRelativePath(car, file);
                    if (relative.Equals(InstallMarker, StringComparison.OrdinalIgnoreCase)) continue;
                    if (ExportSkips.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
                    zip.CreateEntryFromFile(file, $"{carId}/{relative.Replace('\\', '/')}", CompressionLevel.Optimal); // streams the file (QS-7)
                }
            }

            File.Move(temp, zipPath);
            return (zipPath, null);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    /// <summary>Runs the validator (VA-1) on a workspace car, with the workspace's AC folder for AC's shared GUIDs.txt (V-09).</summary>
    public (IReadOnlyList<Validation.Finding>? Findings, string? Error) Validate(string carId)
    {
        var (car, error) = CarFile(carId, ".");
        if (car == null) return (null, error);
        return Directory.Exists(car) ? (Validation.Validator.Run(car, AcFolder), null) : (null, $"There is no car {carId} in the workspace.");
    }

    /// <summary>
    /// Why install or export is refused (PK-4, owner decision 2026-11-03: errors block both, warnings and infos never do), or null when the car has no errors.
    /// </summary>
    private string? ValidationBlock(string carId)
    {
        var errors = Validate(carId).Findings?.Where(f => f.Severity == Validation.Severity.Error).ToList() ?? [];
        return errors.Count == 0 ? null
            : $"Validation found {errors.Count} error(s): " + string.Join("; ", errors.Take(3).Select(f => $"{f.Rule} {f.Message}")) +
              (errors.Count > 3 ? "; ..." : "") + " Fix them, or choose to continue anyway.";
    }
}
