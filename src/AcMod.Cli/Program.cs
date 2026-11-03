using System.Globalization;
using AcMod.Core.Workspaces;

// acmod: a thin command line over AcMod.Core. It holds no mod logic of its own (CC-1); each command calls Core and prints the result.
// It never reads from the keyboard, so it also works through Claude Code's "!" prefix.
try
{
    return Run(args);
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Failed: {e.Message}");
    return 1;
}

static int Run(string[] args)
{
    if (args is [] or ["help"]) return Help();
    if (args is ["setup", var root, .. var rest] && rest.Length <= 1) return Setup(root, rest.FirstOrDefault());

    var (settings, settingsError) = Settings.Load(Settings.DefaultPath);
    if (settingsError != null || settings.WorkspaceFolder == null)
    {
        return Fail(settingsError ?? "No workspace yet. Run: acmod setup <workspace folder> [<AC folder>]");
    }

    var (ws, openError) = Workspace.Open(settings.WorkspaceFolder, settings.AcFolder);
    if (ws == null) return Fail(openError!);

    return args switch
    {
        ["list"] => List(ws),
        ["usage", var days] when int.TryParse(days, out var d) => Usage(ws, d),
        ["import", var source] => Report(ws.ImportCar(source), id => $"Imported {id} into {Path.Combine(ws.Root, "cars", id)}\n{Summary(ws, id)}"),
        ["validate", var car] => Validate(ws, car),
        ["snapshot", var car] => Report(ws.Snapshot(car), id => $"Snapshot {id} of {car}"),
        ["snapshots", var car] => Print(ws.Snapshots(car)),
        ["restore-snapshot", var car, var id] => Report(ws.RestoreSnapshot(car, id), before => $"Restored {car} to {id}. To undo, restore {before}."),
        ["delete", var car] => Report(ws.DeleteCar(car), id => $"Moved {car} to the trash as {id}"),
        ["trash"] => Print(ws.Subfolders("trash").Select(f => $"{f.Name,-50} {Megabytes(f.Size),12}")),
        ["restore", var trashId] => Report(ws.RestoreFromTrash(trashId), id => $"Restored {id} from the trash"),
        ["purge", var trashId] => Report(ws.PermanentDeleteWarning(trashId), w => $"{w}\nTo confirm, run: acmod purge {trashId} <car ID>"),
        ["purge", var trashId, var typed] => ws.DeletePermanently(trashId, typed) is { } error ? Fail(error) : Done($"Deleted {trashId} permanently"),
        ["install", var car] => Install(ws, car, force: false),
        ["install", var car, "--force"] => Install(ws, car, force: true),
        ["export", var car, .. var flags] when flags.All(f => f is "--force" or "--pack") =>
            Report(ws.ExportZip(car, ignoreErrors: flags.Contains("--force"), pack: flags.Contains("--pack")),
                path => $"Exported {car} to {path}\nInstall it by dragging the ZIP onto Content Manager."),
        ["save-as", var car, var newId, .. var flags] => SaveAs(ws, car, newId, flags),
        _ => Help(),
    };
}

static int Setup(string root, string? acFolder)
{
    var ac = acFolder ?? AcInstall.Find();
    if (ac == null) return Fail("AC wasn't found through Steam. Run: acmod setup <workspace folder> <AC folder>");
    if (!AcInstall.IsAcFolder(ac)) return Fail($"{ac} has no acs.exe and content\\cars, so it isn't an AC install.");

    var (ws, error) = Workspace.Open(root, ac);
    if (ws == null) return Fail(error!);
    new Settings(ws.Root, ws.AcFolder).Save(Settings.DefaultPath);
    return Done($"Workspace: {ws.Root}\nAC: {ws.AcFolder}\nSaved to {Settings.DefaultPath}");
}

static int List(Workspace ws)
{
    Console.WriteLine($"Workspace: {ws.Root}\nAC: {ws.AcFolder}\n");
    Console.WriteLine($"{"Car ID",-40} {"Name",-30} {"Size",12}  {"Last changed",-16}  Validation");
    foreach (var car in ws.Cars())
    {
        Console.WriteLine($"{car.Id,-40} {car.DisplayName ?? "(no name)",-30} {Megabytes(car.Size),12}  {Local(car.LastChangedUtc),-16}  not run (M5)");
    }

    return 0;
}

static int Usage(Workspace ws, int days)
{
    Console.WriteLine($"Cars unchanged for {days} days or more:");
    foreach (var car in ws.UnusedCars(TimeSpan.FromDays(days))) Console.WriteLine($"  {car.Id,-40} {Megabytes(car.Size),12}  {Local(car.LastChangedUtc)}");
    foreach (var folder in new[] { "snapshots", "trash" })
    {
        var entries = ws.Subfolders(folder);
        Console.WriteLine($"{folder} ({Megabytes(entries.Sum(e => e.Size))}):");
        foreach (var entry in entries) Console.WriteLine($"  {entry.Name,-50} {Megabytes(entry.Size),12}");
    }

    return 0;
}

// VA-1, VA-2: every finding with severity, rule, file, message, fix, and evidence. Exit code 1 when there are errors.
static int Validate(Workspace ws, string car)
{
    var (findings, error) = ws.Validate(car);
    if (findings == null) return Fail(error!);
    foreach (var f in findings)
    {
        Console.WriteLine($"{f.Severity.ToString().ToUpperInvariant(),-8} {f.Rule}  {f.File ?? "(car)"}\n         {f.Message}\n         Fix: {f.Fix}  [{f.Evidence.ToString().ToLowerInvariant()}]");
    }

    Console.WriteLine(Summary(ws, car));
    return findings.Any(f => f.Severity == AcMod.Core.Validation.Severity.Error) ? 1 : 0;
}

static string Summary(Workspace ws, string car)
{
    var findings = ws.Validate(car).Findings ?? [];
    int Count(AcMod.Core.Validation.Severity s) => findings.Count(f => f.Severity == s);
    return findings.Count == 0 ? "Validation: no findings." : $"Validation: {Count(AcMod.Core.Validation.Severity.Error)} error(s), {Count(AcMod.Core.Validation.Severity.Warning)} warning(s), {Count(AcMod.Core.Validation.Severity.Info)} info. Run: acmod validate {car}";
}

// WS-7 and WS-8: flags are --name "Display name" and --replace.
static int SaveAs(Workspace ws, string car, string newId, string[] flags)
{
    var nameAt = Array.IndexOf(flags, "--name");
    var name = nameAt >= 0 && nameAt + 1 < flags.Length ? flags[nameAt + 1] : null;
    var (result, error) = ws.SaveAsNewCar(car, newId, name);
    if (result == null) return Fail(error!);

    Console.WriteLine($"Saved {car} as {newId}.");
    foreach (var note in result.Notes) Console.WriteLine($"Note: {note}");
    if (result.MentionsOfOldId.Count > 0) Console.WriteLine($"These files still mention {car}; check them: {string.Join(", ", result.MentionsOfOldId)}");
    Console.WriteLine(Summary(ws, newId));
    if (!flags.Contains("--replace")) return 0;

    var (trashId, replaceError) = ws.ReplacePrevious(car, newId);
    return trashId == null ? Fail(replaceError!) : Done($"Moved {car} to the trash as {trashId}.");
}

static int Install(Workspace ws, string car, bool force)
{
    var (installed, replaced, error) = ws.InstallToAc(car, ignoreErrors: force);
    if (installed == null) return Fail(error!);
    return Done($"Installed {car} to {installed}" + (replaced == null ? "" : $"\nThe previous install is in the trash as {replaced}"));
}

static int Report<T>((T? Value, string? Error) result, Func<T, string> message) where T : class =>
    result.Value == null ? Fail(result.Error!) : Done(message(result.Value));

static int Print(IEnumerable<string> lines)
{
    foreach (var line in lines) Console.WriteLine(line);
    return 0;
}

static int Done(string message)
{
    Console.WriteLine(message);
    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

static string Megabytes(long bytes) => (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

static string Local(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

static int Help()
{
    Console.WriteLine("""
        acmod setup <workspace folder> [<AC folder>]   choose the workspace; AC is found through Steam if left out (WS-1)
        acmod list                                     cars in the workspace (WS-3)
        acmod usage <days>                             cars unchanged for <days>, snapshot and trash sizes (WS-5)
        acmod import <folder or .zip>                  copy one car into the workspace (WS-4)
        acmod snapshot <car ID>                        save the car's data and UI files (WS-9, WS-10)
        acmod snapshots <car ID>                       list a car's snapshots
        acmod restore-snapshot <car ID> <snapshot ID>  put a car's data and UI files back; this can be undone
        acmod delete <car ID>                          move a car to the trash (WS-6)
        acmod trash                                    list the trash
        acmod restore <trash ID>                       move a car back from the trash
        acmod purge <trash ID>                         show what a permanent delete would remove
        acmod purge <trash ID> <car ID>                delete it permanently, with its snapshots; can't be undone
        acmod validate <car ID>                        run the validator: every finding with its fix (VA-1, VA-2)
        acmod install <car ID> [--force]               copy a car into AC's content\cars (WS-11); --force despite validation errors
        acmod export <car ID> [--pack] [--force]       write a ZIP for Content Manager to the exports folder (PK-2, PK-4); --pack ships data.acd (PK-1)
        acmod save-as <car ID> <new ID> [--name "Display name"] [--replace]
                                                       copy a car to a new ID (WS-7); --replace trashes the old one if the copy validates (WS-8)
        """);
    return 1;
}
