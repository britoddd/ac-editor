using System.Text.Json;
using AcMod.Core.Acd;

namespace AcMod.Core.Workspaces;

// WS-11, the only code that writes into the AC install (QS-6, hard rule 5). Every path it writes is content\cars\<name>,
// where <name> is a car ID that passed CheckName, so it can't name another folder.
public sealed partial class Workspace
{
    /// <summary>The file that marks a folder in content\cars as installed by this app, so a later install may replace it (WS-11).</summary>
    public const string InstallMarker = "acmod_install.json";

    /// <summary>
    /// Copies a workspace car to content\cars\&lt;car ID&gt; and marks it with <see cref="InstallMarker"/> (WS-11). It refuses a car whose
    /// data can't be read (V-13), and any existing folder without the marker, such as a Kunos car, a Content Manager install, or a copy made
    /// by hand. A previous install by this app is copied to the workspace trash before it is replaced, so tuning done in place isn't lost.
    /// ponytail: "validated" means readable data until the M5 validator exists; dev files (PK-3) are copied too until M6.
    /// </summary>
    /// <returns>The installed folder, and the trash ID of the previous install when one was replaced.</returns>
    public (string? InstalledFolder, string? ReplacedTrashId, string? Error) InstallToAc(string carId)
    {
        if (AcFolder == null || !AcInstall.IsAcFolder(AcFolder))
        {
            return (null, null, "The AC folder isn't set, or it has no acs.exe and content\\cars. Choose the AC folder first.");
        }

        var (car, carError) = CarFile(carId, ".");
        if (car == null) return (null, null, carError);
        if (!Directory.Exists(car)) return (null, null, $"There is no car {carId} in the workspace.");
        var data = CarData.Load(car);
        if (data.Error != null) return (null, null, $"{carId} can't be installed, because its data can't be read: {data.Error}");

        var acCars = Path.Combine(AcFolder, "content", "cars");
        var target = Path.Combine(acCars, carId);
        var temp = target + ".acmod-installing";
        var old = target + ".acmod-old";
        if (Directory.Exists(target))
        {
            if (File.GetAttributes(target).HasFlag(FileAttributes.ReparsePoint))
            {
                return (null, null, $"content\\cars\\{carId} is a junction or symlink, so it won't be replaced. Remove it yourself first.");
            }

            if (!File.Exists(Path.Combine(target, InstallMarker)))
            {
                return (null, null,
                    $"content\\cars\\{carId} wasn't installed by this app (it has no {InstallMarker}), so it won't be replaced. " +
                    "It may be a Kunos car, a Content Manager install, or a copy you made. Move or rename it yourself first.");
            }
        }

        string? trashId = null, trashCopy = null;
        try
        {
            foreach (var leftover in new[] { temp, old }.Where(Directory.Exists)) DeleteFolder(leftover); // this app's names, left by a crash

            CopyFolder(car, temp);
            File.WriteAllBytes(
                Path.Combine(temp, InstallMarker),
                JsonSerializer.SerializeToUtf8Bytes(new { installedBy = "AcMod", carId, installedUtc = DateTime.UtcNow }, MarkerJson));

            if (Directory.Exists(target))
            {
                trashId = UniqueName(Path.Combine(Root, "trash"), carId + "_");
                (trashCopy, var trashError) = Resolve(Path.Combine("trash", trashId));
                if (trashCopy == null) return (null, null, trashError);
                CopyFolder(target, trashCopy); // a copy, because the trash may be on another drive
                Directory.Move(target, old); // fails cleanly while AC or another program has a file open in it
            }

            try
            {
                Directory.Move(temp, target);
            }
            catch
            {
                if (Directory.Exists(old)) Directory.Move(old, target);
                throw;
            }

            trashCopy = null; // installed, so the trash copy stays
            try
            {
                if (Directory.Exists(old)) DeleteFolder(old);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // ponytail: a locked file can leave <car ID>.acmod-old behind; its copy is in the trash, and the next install removes it.
            }

            return (target, trashId, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, null,
                $"Installing {carId} failed, and content\\cars was left as it was: {e.Message} " +
                "Close AC and Content Manager and try again. If Windows refuses access to content\\cars, export a ZIP for Content Manager instead (M6).");
        }
        finally
        {
            if (Directory.Exists(temp)) DeleteFolder(temp);
            if (trashCopy != null && Directory.Exists(trashCopy) && Directory.Exists(target)) DeleteFolder(trashCopy); // not needed after a failure
        }
    }

    private static readonly JsonSerializerOptions MarkerJson = new() { WriteIndented = true };

    /// <summary>Copies a folder tree, empty folders included, without following junctions or symlinks. Works across drives.</summary>
    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.EnumerateDirectories(from, "*", NoLinks)) Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        foreach (var file in Directory.EnumerateFiles(from, "*", NoLinks)) File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)));
    }
}
