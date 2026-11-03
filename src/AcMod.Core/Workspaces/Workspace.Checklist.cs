using System.Text.Json;

namespace AcMod.Core.Workspaces;

// LE-2: manual checklist ticks, kept in the workspace root (owner decision, 2026-11-03), never in a car folder, where they would end up in exports.
public sealed partial class Workspace
{
    private const string ChecklistFile = "checklists.json";

    /// <summary>The checklist items the user ticked for a car.</summary>
    public IReadOnlySet<string> ChecklistTicks(string carId) =>
        LoadTicks().TryGetValue(carId, out var ticks) ? ticks.ToHashSet(StringComparer.Ordinal) : new HashSet<string>();

    /// <summary>Ticks or unticks a manual checklist item for a car, written through a temp file and a rename.</summary>
    public string? SetChecklistTick(string carId, string itemId, bool ticked)
    {
        if (CheckName(carId) is { } error) return error;
        if (!Learning.FirstCarChecklist.Items.Any(i => i.Id == itemId && i.Rules.Count == 0)) return $"\"{itemId}\" isn't a checklist item you tick by hand.";

        var all = LoadTicks();
        var ticks = all.TryGetValue(carId, out var list) ? list.ToHashSet(StringComparer.Ordinal) : [];
        if (ticked) ticks.Add(itemId);
        else ticks.Remove(itemId);
        all[carId] = [.. ticks.Order(StringComparer.Ordinal)];

        var (path, pathError) = Resolve(ChecklistFile);
        if (path == null) return pathError;
        File.WriteAllBytes(path + ".tmp", JsonSerializer.SerializeToUtf8Bytes(all, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, overwrite: true);
        return null;
    }

    private Dictionary<string, List<string>> LoadTicks()
    {
        var path = Path.Combine(Root, ChecklistFile);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllBytes(path)) ?? [] : [];
        }
        catch (JsonException)
        {
            return []; // ponytail: a broken checklists.json starts over; it only holds ticks, never car data
        }
    }
}
