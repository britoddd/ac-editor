using System.Text.Json;

namespace AcMod.Core.Workspaces;

/// <summary>A car's checklist state: the items ticked by hand, and the car it was copied from, if any.</summary>
public sealed record CarChecklist(List<string> Ticks, string? CopiedFrom);

// LE-2: checklist state, kept in the workspace root (owner decision, 2026-11-03), never in a car folder, where it would end up in exports.
public sealed partial class Workspace
{
    private const string ChecklistFile = "checklists.json";

    /// <summary>Manual items, plus "own:&lt;item&gt;" ticks that say a copied model or collider was replaced by the user's own.</summary>
    private static bool Tickable(string itemId) =>
        Learning.FirstCarChecklist.Items.Any(i => i.Rules.Count == 0 && i.Id == itemId) || Learning.FirstCarChecklist.CopySensitive.Any(id => "own:" + id == itemId);

    /// <summary>The checklist items the user ticked for a car.</summary>
    public IReadOnlySet<string> ChecklistTicks(string carId) =>
        LoadChecklists().TryGetValue(carId, out var state) ? state.Ticks.ToHashSet(StringComparer.Ordinal) : new HashSet<string>();

    /// <summary>The car this one was copied from by Save as new car, or null (LE-2: its model and collider aren't the user's own yet).</summary>
    public string? CopiedFrom(string carId) => LoadChecklists().TryGetValue(carId, out var state) ? state.CopiedFrom : null;

    /// <summary>Ticks or unticks a manual checklist item for a car, written through a temp file and a rename.</summary>
    public string? SetChecklistTick(string carId, string itemId, bool ticked)
    {
        if (CheckName(carId) is { } error) return error;
        if (!Tickable(itemId)) return $"\"{itemId}\" isn't a checklist item you tick by hand.";
        return UpdateChecklist(carId, state =>
        {
            var ticks = state.Ticks.ToHashSet(StringComparer.Ordinal);
            if (ticked) ticks.Add(itemId);
            else ticks.Remove(itemId);
            return state with { Ticks = [.. ticks.Order(StringComparer.Ordinal)] };
        });
    }

    private string? SetCopiedFrom(string carId, string sourceId) => UpdateChecklist(carId, state => state with { CopiedFrom = sourceId });

    /// <summary>Gives a renamed car the old ID's checklist state (its ticks and its source car).</summary>
    private string? MoveChecklist(string oldId, string newId) =>
        LoadChecklists().TryGetValue(oldId, out var old) ? UpdateChecklist(newId, _ => old) : UpdateChecklist(newId, state => state with { CopiedFrom = null });

    private string? UpdateChecklist(string carId, Func<CarChecklist, CarChecklist> change)
    {
        var all = LoadChecklists();
        all[carId] = change(all.TryGetValue(carId, out var state) ? state : new CarChecklist([], null));
        var (path, pathError) = Resolve(ChecklistFile);
        if (path == null) return pathError;
        File.WriteAllBytes(path + ".tmp", JsonSerializer.SerializeToUtf8Bytes(all, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, overwrite: true);
        return null;
    }

    private Dictionary<string, CarChecklist> LoadChecklists()
    {
        var path = Path.Combine(Root, ChecklistFile);
        if (!File.Exists(path)) return [];
        var bytes = File.ReadAllBytes(path);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, CarChecklist>>(bytes) ?? [];
        }
        catch (JsonException)
        {
            try
            {
                // The first version (M7 step 4) stored only the ticks: { "car": ["dummies"] }.
                return (JsonSerializer.Deserialize<Dictionary<string, List<string>>>(bytes) ?? []).ToDictionary(p => p.Key, p => new CarChecklist(p.Value, null));
            }
            catch (JsonException)
            {
                return []; // ponytail: a broken checklists.json starts over; it only holds ticks, never car data
            }
        }
    }
}
