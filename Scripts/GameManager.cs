using Godot;
using System.Collections.Generic;
using System.Linq;

public static partial class GameManager {

    // private enum Scene { MainMenu, Bonfire, Encounter };

    private static Dictionary<string, Equipment> equipmentMap;
    private static Dictionary<string, bool> unlockedEquipment;

    public static string[] GetAllFilePaths(string path) {
        string[] filePaths = {};
        DirAccess directory = DirAccess.Open(path);
        directory.ListDirBegin();
        string fileName = directory.GetNext();
        while (fileName != "") {
            var filePath = path + "/" + fileName;
            if (directory.CurrentIsDir()) {
                filePaths = filePaths.Concat(GetAllFilePaths(filePath)).ToArray();
            }
            else {
                filePaths = filePaths.Append(filePath).ToArray();
                fileName = directory.GetNext();
            }
        }
        return filePaths;
    }

    public static void LoadAllEquipment() {
        string[] filePaths = GetAllFilePaths("res://Resources/Prefabs/Equipment/");
        foreach (string fileName in filePaths) {
            Equipment equipment = ResourceLoader.Load<Equipment>(fileName);
            equipmentMap.Add(equipment.name, equipment);
        }
    }

    
}
