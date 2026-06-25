using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static partial class GameManager {

    // Catalog of every Weapon/Armour template loaded from disk, keyed by template name.
    private static Dictionary<string, Equipment> equipmentMap = new Dictionary<string, Equipment>();

    // Party-owned instances, keyed by unique instance id.
    private static Dictionary<string, Equipment> ownedInstances = new Dictionary<string, Equipment>();

    // Insertion order so "Newest unlocked" sort works without timestamps.
    private static List<string> ownedOrder = new List<string>();

    private static bool catalogLoaded = false;

    private static readonly string[] EQUIPMENT_SCAN_ROOTS = {
        "res://Resources/Prefabs/Equipment",
        "res://Resources/Prefabs/Player",
    };

    public static void EnsureCatalogLoaded() {
        if (catalogLoaded) return;
        LoadAllEquipment();
        catalogLoaded = true;
    }

    public static void LoadAllEquipment() {
        equipmentMap.Clear();
        foreach (string root in EQUIPMENT_SCAN_ROOTS) {
            if (!DirAccess.DirExistsAbsolute(root)) continue;
            foreach (string path in GetAllFilePaths(root)) {
                if (!path.EndsWith(".tres")) continue;
                var res = ResourceLoader.Load(path);
                if (res is Equipment eq && !string.IsNullOrEmpty(eq.name)) {
                    if (!equipmentMap.ContainsKey(eq.name))
                        equipmentMap[eq.name] = eq;
                }
            }
        }
    }

    public static string[] GetAllFilePaths(string path) {
        var results = new List<string>();
        DirAccess dir = DirAccess.Open(path);
        if (dir == null) return results.ToArray();
        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName)) {
            string childPath = path + "/" + fileName;
            if (dir.CurrentIsDir())
                results.AddRange(GetAllFilePaths(childPath));
            else
                results.Add(childPath);
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
        return results.ToArray();
    }

    // ---- Owned instance pool ----

    public static Equipment GetTemplate(string templateName) {
        EnsureCatalogLoaded();
        return equipmentMap.TryGetValue(templateName ?? "", out var t) ? t : null;
    }

    public static IEnumerable<string> GetAllTemplateNames() {
        EnsureCatalogLoaded();
        return equipmentMap.Keys;
    }

    // Duplicate a template, assign a fresh GUID id, add to the owned pool, return it.
    public static Equipment MintInstance(string templateName) {
        var template = GetTemplate(templateName);
        if (template == null) {
            GD.PushWarning($"GameManager.MintInstance: unknown template '{templateName}'");
            return null;
        }
        Resource res = (Resource)template;
        Equipment inst = (Equipment)res.Duplicate(true);
        inst.id = Guid.NewGuid().ToString();
        ownedInstances[inst.id] = inst;
        ownedOrder.Add(inst.id);
        return inst;
    }

    // Re-add an instance to the pool (used when an item is unequipped or when loading a save).
    public static void AddInstance(Equipment inst) {
        if (inst == null || string.IsNullOrEmpty(inst.id)) return;
        if (ownedInstances.ContainsKey(inst.id)) return;
        ownedInstances[inst.id] = inst;
        ownedOrder.Add(inst.id);
    }

    public static Equipment GetInstance(string id) {
        if (string.IsNullOrEmpty(id)) return null;
        return ownedInstances.TryGetValue(id, out var e) ? e : null;
    }

    public static IEnumerable<Equipment> GetAllInstances() {
        return ownedOrder.Where(id => ownedInstances.ContainsKey(id))
                         .Select(id => ownedInstances[id]);
    }

    public static int OwnedOrderIndex(string id) {
        int i = ownedOrder.IndexOf(id);
        return i < 0 ? int.MaxValue : i;
    }

    public static void ResetOwnedPool() {
        ownedInstances.Clear();
        ownedOrder.Clear();
    }

    // ---- Save / Load serialization ----

    public sealed class OwnedRecord {
        public string id;
        public string templateName;
    }

    public static OwnedRecord[] SerializeOwnedPool() {
        var list = new List<OwnedRecord>();
        foreach (string id in ownedOrder) {
            if (!ownedInstances.TryGetValue(id, out var inst)) continue;
            list.Add(new OwnedRecord { id = inst.id, templateName = inst.name });
        }
        return list.ToArray();
    }

    // SaveGame stores instances as (id, templateName) string-pair arrays.
    public static string[] SerializeOwnedFlat() {
        var pairs = SerializeOwnedPool();
        var result = new string[pairs.Length * 2];
        for (int i = 0; i < pairs.Length; i++) {
            result[i * 2] = pairs[i].id;
            result[i * 2 + 1] = pairs[i].templateName;
        }
        return result;
    }

    public static void DeserializeOwnedFlat(string[] flat) {
        ResetOwnedPool();
        if (flat == null) return;
        EnsureCatalogLoaded();
        for (int i = 0; i + 1 < flat.Length; i += 2) {
            string id = flat[i];
            string templateName = flat[i + 1];
            var template = GetTemplate(templateName);
            if (template == null) {
                GD.PushWarning($"GameManager.Deserialize: missing template '{templateName}', skipping id {id}");
                continue;
            }
            Resource res = (Resource)template;
            Equipment inst = (Equipment)res.Duplicate(true);
            inst.id = id;
            ownedInstances[id] = inst;
            ownedOrder.Add(id);
        }
    }
}
