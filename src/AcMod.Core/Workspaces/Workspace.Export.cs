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
    /// The ZIP is written to a .tmp file first and renamed, so a half-written ZIP never appears. ponytail: the M5 validator gate (PK-4) isn't here yet.
    /// </summary>
    /// <returns>The ZIP's full path.</returns>
    public (string? ZipPath, string? Error) ExportZip(string carId)
    {
        var (car, error) = CarFile(carId, ".");
        if (car == null) return (null, error);
        if (!Directory.Exists(car)) return (null, $"There is no car {carId} in the workspace.");
        var data = CarData.Load(car);
        if (data.Error != null) return (null, $"{carId} can't be exported, because its data can't be read: {data.Error}");

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
}
