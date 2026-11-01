using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HouseCreators.Blockout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HouseCreators.EditorTools
{
    // Имена объектов мастерской: простые английские слова (Floor, Wall, Lamp) остаются,
    // сложные заменяются русским транслитом (Joist → Balka, Wardrobe → Shkaf).
    public static class WorkshopNaming
    {
        // Целые имена, которые по словам переводятся плохо
        static readonly Dictionary<string, string> Full = new Dictionary<string, string>
        {
            { "Glass_Sky", "Glass" },
            { "Cap_Emitter", "Izluchatel" },
            { "Cap_Dome", "Kupol" },
            { "Top_Grille", "Reshetka" },
            { "Vent_Slots", "Ventilyaciya" },
            { "Ledge_Indicators", "Ustup_indikatorov" },
            { "Ledge_Controls", "Ustup_upravleniya" },
            { "Mode_Label_Shrink", "Metka_Umenshenie" },
            { "Mode_Label_Grow", "Metka_Uvelichenie" },
            { "Hammer_Handle", "Hammer_Ruchka" },
            { "Hammer_Head", "Hammer_Boyok" },
            { "Monitor_Stand", "Monitor_Podstavka" },
            { "Backrest_Post", "Spinka_Stoyka" },
            { "Bin_Lid", "Musorka_Kryshka" },
            { "HatBox_Ribbon", "Lenta" },
            { "MouseTrap_Bar", "Skoba" },
            { "Cable_Run", "Cable" },
            { "PC_Tower", "Sistemnik" },
            { "Pickup_Notebook", "Pickup_Bloknot" },
            { "Desk_DrawerUnit", "Desk_Tumba" },
            { "Route_2_Chest", "Route_2_Komod" },
            { "Route_4_Bookcase", "Route_4_Knizhnyy_shkaf" },
            { "Route_5_Door_Wardrobe", "Route_5_Door_Shkaf" },
            { "DiffuserLight_Main", "Light_Rasseivatel" },
            { "DiffuserLight_South", "Light_Rasseivatel_South" },
            { "Fill_East", "Podsvetka" },
            { "Attic_Hatch", "Light_Cherdak_Lyuk" },
            { "Attic_Lamp", "Light_Cherdak_Lamp" },
            { "Tunnel_FallenBook_Pages", "Stranicy" },
        };

        // Только сложные слова; всё, чего здесь нет, остаётся по-английски
        static readonly Dictionary<string, string> Words = new Dictionary<string, string>
        {
            { "Workshop", "Masterskaya" }, { "Baseboard", "Plintus" }, { "Cornice", "Karniz" },
            { "LampDiffuser", "Rasseivatel" }, { "Diffuser", "Rasseivatel" }, { "Rim", "Obodok" }, { "Shard", "Oskolok" },
            { "Hatch", "Lyuk" }, { "Attic", "Cherdak" }, { "Roof", "Perekrytie" }, { "Boards", "Nastil" }, { "Joist", "Balka" },
            { "Insulation", "Uteplitel" }, { "Bale", "Tyuk" }, { "Pile", "Kucha" }, { "WaterPipe", "Truba" },
            { "VentDuct", "Ventkorob" }, { "Seam", "Shov" }, { "MouseTrap", "Myshelovka" }, { "DustBunny", "Pyl" },
            { "LampHousing", "Korpus_lampy" }, { "Ballast", "Drossel" },
            { "Sill", "Podokonnik" }, { "Curtain", "Shtora" }, { "CurtainRod", "Karniz_shtor" },
            { "Radiator", "Batareya" }, { "Fin", "Sekciya" }, { "BlueprintPoster", "Chertezh" }, { "Pegboard", "Perfopanel" },
            { "Wrench", "Klyuch" }, { "Pliers", "Ploskogubcy" }, { "Rug", "Kovyor" }, { "PaperBall", "Bumazhka" },
            { "Nut", "Gayka" }, { "Screw", "Vint" },
            { "ShrinkCapsule", "Kapsula_umensheniya" }, { "Footprint", "Sled" }, { "DoorPost", "Stoyka_dveri" },
            { "Cap", "Kryshka" }, { "CapsuleConsole", "Pult_kapsuly" }, { "Ledge", "Ustup" },
            { "ModeSwitch", "Pereklyuchatel" }, { "Lever", "Rychag" }, { "Knob", "Ruchka" },
            { "Shoebox", "Korobka_ot_obuvi" }, { "PaintCan", "Banka_kraski" }, { "Toolbox", "Yashchik_instrumentov" },
            { "Latch", "Zashchelka" }, { "Caster", "Kolesiko" }, { "GasLift", "Gazlift" }, { "Backrest", "Spinka" },
            { "Armrest", "Podlokotnik" }, { "Bin", "Musorka" }, { "Tape", "Skotch" }, { "Drawer", "Yashchik" },
            { "Tray", "Lotok" }, { "Handle", "Ruchka" }, { "Bracket", "Kronshteyn" }, { "Board", "Shelf" },
            { "Divider", "Peregorodka" }, { "Hurdle", "Barer" }, { "StandingBooks", "Knigi_stoya" }, { "Tunnel", "Laz" },
            { "FallenBook", "Upavshaya_kniga" }, { "Stack", "Stopka" }, { "Leaf", "Polotno" }, { "Jamb", "Kosyak" },
            { "Lintel", "Nalichnik" }, { "Wardrobe", "Shkaf" }, { "Suitcase", "Chemodan" }, { "Strap", "Remen" },
            { "HatBox", "Shlyapnaya_korobka" }, { "HatchCover", "Kryshka_lyuka" }, { "CapsuleGlow", "Svechenie_kapsuly" },
            { "CameraPivot", "Opora_kamery" },
        };

        [MenuItem("Tools/HouseCreators/Workshop: Rename To Russian")]
        public static void RenameOpenScene()
        {
            int n = Apply();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Workshop] Переименовано объектов: {n}");
        }

        // Переименовывает все объекты активной сцены. Имена родителей берутся до переименования.
        public static int Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
            var original = all.ToDictionary(t => t, t => t.name);
            int changed = 0;
            foreach (var t in all)
            {
                string parent = t.parent != null ? original[t.parent] : "";
                string ru = Translate(original[t], parent, t.GetComponent<BlockoutLabel>() != null);
                if (ru != t.name)
                {
                    t.name = ru;
                    changed++;
                }
            }
            return changed;
        }

        static string Translate(string name, string parent, bool isLabel)
        {
            if (isLabel) return name;
            name = Regex.Replace(name, @"(\d),(\d)", "$1.$2"); // 60,5 → 60.5 (русская локаль)
            if (name == "Label") return parent.Contains("PaintCan") ? "Etiketka" : "Nakleyka";
            if (name == "Board" && parent == "Pegboard") return "Board";
            if (name == "Top" && parent == "Desk") return "Stoleshnica";
            if (Full.TryGetValue(name, out var full)) return full;

            var parts = name.Split('_').ToList();
            // Префиксы ступеней (R1_, D2_, E1_, C_, W_) не нужны — высота уже есть в имени
            if (parts.Count > 1 && Regex.IsMatch(parts[0], @"^([RDE]\d|C|W)$")) parts.RemoveAt(0);

            // Сначала пробуем длинные сочетания (Ledge_Indicators_46 → Ustup_indikatorov_46)
            for (int len = parts.Count; len >= 2; len--)
            {
                string head = string.Join("_", parts.Take(len));
                if (Full.TryGetValue(head, out var hit))
                    return string.Join("_", new[] { hit }.Concat(parts.Skip(len).Select(Word)));
            }
            return string.Join("_", parts.Select(Word));
        }

        static string Word(string w)
        {
            if (float.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return w;
            return Words.TryGetValue(w, out var ru) ? ru : w;
        }
    }
}
