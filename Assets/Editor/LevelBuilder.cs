using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HouseCreators.EditorTools
{
    // Генератор уровня «Мастерская изобретателя»: три комнаты (спальня, мастерская, кухня),
    // паркур-маршрут к столу и заглушка прибора увеличения.
    // Масштаб мира: игрок — обычная капсула 1.8 м, а вся обстановка увеличена в 25 раз
    // (персонаж ростом ~7 см, как спичечный коробок). Ассеты nappin примерно вдвое крупнее реальных, поэтому для них ×12.5.
    public static class LevelBuilder
    {
        const string ScenePath = "Assets/Scenes/Level_Workshop.unity";
        const string LevelDir = "Assets/Level";
        const string MatDir = "Assets/Level/Materials";

        const float RealScale = 25f;    // ScifiOffice, Next-gen Electronics
        const float NappinScale = 12.5f; // nappin House Interior Pack

        const float RoomHeight = 70f;   // 2.8 м потолок
        const float WallThickness = 1.5f;
        const float DoorHeight = 55f;
        const float BookThickness = 0.75f; // 3 см книга

        const string Nappin = "Assets/nappin/HouseInteriorPack/Prefabs/";
        const string Scifi = "Assets/ScifiOfficeLite/Prefabs/";
        const string Electronics = "Assets/Next-gen Electronics Pack/Prefabs/";

        enum Side { South, North, West, East }

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static System.Random rnd;

        [MenuItem("Tools/HouseCreators/Build Level (3 rooms)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            rnd = new System.Random(7);
            EnsureFolder("Assets", "Level");
            EnsureFolder(LevelDir, "Materials");
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var level = new GameObject("Level").transform;
            var bedroom = BuildRoom(level, "Bedroom", new Rect(-100, 0, 100, 100), mats["WallBedroom"], mats["FloorWood"],
                (Side.East, new Vector2(45, 70)));
            var workshop = BuildRoom(level, "Workshop", new Rect(0, 0, 125, 100), mats["WallWorkshop"], mats["FloorWorkshop"],
                (Side.West, new Vector2(45, 70)), (Side.North, new Vector2(85, 110)));
            var kitchen = BuildRoom(level, "Kitchen", new Rect(0, 100, 125, 90), mats["WallKitchen"], mats["FloorKitchen"],
                (Side.South, new Vector2(85, 110)));

            FurnishWorkshop(workshop);
            FurnishBedroom(bedroom);
            FurnishKitchen(kitchen);
            BuildLighting(level);

            var player = BuildPlayer(new Vector3(25f, 0.05f, 20f), 60f);
            Selection.activeGameObject = player;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            Debug.Log("[LevelBuilder] Level built: " + ScenePath);
        }

        // ---------- Комнаты ----------

        static Transform BuildRoom(Transform parent, string name, Rect r, Material wall, Material floor,
            params (Side side, Vector2 span)[] openings)
        {
            var room = new GameObject(name).transform;
            room.SetParent(parent, false);
            var shell = Group(room, "Shell");

            Vector2 c = r.center;
            Box(shell, "Floor", new Vector3(c.x, -0.5f, c.y), new Vector3(r.width, 1f, r.height), floor);
            Box(shell, "Ceiling", new Vector3(c.x, RoomHeight + 0.5f, c.y), new Vector3(r.width, 1f, r.height), mats["Ceiling"]);

            // Каждая комната строит свои стены внутри своего прямоугольника,
            // поэтому у общей стены с двух сторон разный цвет.
            float h = WallThickness * 0.5f;
            Wall(shell, "Wall_South", true, r.yMin + h, r.xMin, r.xMax, wall, Spans(openings, Side.South));
            Wall(shell, "Wall_North", true, r.yMax - h, r.xMin, r.xMax, wall, Spans(openings, Side.North));
            Wall(shell, "Wall_West", false, r.xMin + h, r.yMin + WallThickness, r.yMax - WallThickness, wall, Spans(openings, Side.West));
            Wall(shell, "Wall_East", false, r.xMax - h, r.yMin + WallThickness, r.yMax - WallThickness, wall, Spans(openings, Side.East));

            // Плинтус
            var trim = mats["Trim"];
            Wall(shell, "Baseboard_South", true, r.yMin + WallThickness + 0.25f, r.xMin + WallThickness, r.xMax - WallThickness, trim, Spans(openings, Side.South), 3f, 0.5f);
            Wall(shell, "Baseboard_North", true, r.yMax - WallThickness - 0.25f, r.xMin + WallThickness, r.xMax - WallThickness, trim, Spans(openings, Side.North), 3f, 0.5f);
            Wall(shell, "Baseboard_West", false, r.xMin + WallThickness + 0.25f, r.yMin + WallThickness, r.yMax - WallThickness, trim, Spans(openings, Side.West), 3f, 0.5f);
            Wall(shell, "Baseboard_East", false, r.xMax - WallThickness - 0.25f, r.yMin + WallThickness, r.yMax - WallThickness, trim, Spans(openings, Side.East), 3f, 0.5f);

            SetStatic(shell.gameObject);
            return room;
        }

        static Vector2[] Spans((Side side, Vector2 span)[] openings, Side side) =>
            openings.Where(o => o.side == side).Select(o => o.span).ToArray();

        // Стена вдоль X (alongX) или вдоль Z с проёмами; height < 0 — до потолка, проёмы — дверные с перемычкой.
        static void Wall(Transform parent, string name, bool alongX, float line, float from, float to, Material m,
            Vector2[] openings, float height = RoomHeight, float thickness = WallThickness)
        {
            bool isFullWall = height >= RoomHeight;
            float cur = from;
            int i = 0;
            foreach (var o in openings.OrderBy(o => o.x))
            {
                if (o.x > cur) WallSegment(parent, $"{name}_{i++}", alongX, line, cur, o.x, 0f, height, thickness, m);
                if (isFullWall) WallSegment(parent, $"{name}_Lintel", alongX, line, o.x, o.y, DoorHeight, RoomHeight, thickness, m);
                cur = Mathf.Max(cur, o.y);
            }
            if (cur < to) WallSegment(parent, $"{name}_{i}", alongX, line, cur, to, 0f, height, thickness, m);
        }

        static void WallSegment(Transform parent, string name, bool alongX, float line, float a, float b, float y0, float y1, float thickness, Material m)
        {
            float mid = (a + b) * 0.5f, len = b - a, hgt = y1 - y0, cy = (y0 + y1) * 0.5f;
            var center = alongX ? new Vector3(mid, cy, line) : new Vector3(line, cy, mid);
            var size = alongX ? new Vector3(len, hgt, thickness) : new Vector3(thickness, hgt, len);
            Box(parent, name, center, size, m);
        }

        // ---------- Мастерская ----------

        static void FurnishWorkshop(Transform room)
        {
            var furniture = Group(room, "Furniture");

            // Рабочий стол учёного у восточной стены, длинной стороной вдоль Z
            var desk = Place(furniture, Scifi + "Tables/Dark Oak/Drawer Table Long Dark Oak.prefab", RealScale, 111f, 50f, 90f);
            var deskBounds = WorldBounds(desk);
            float deskTop = deskBounds.max.y;
            float deskFront = deskBounds.min.x;
            float deskCx = deskBounds.center.x;

            Place(furniture, Scifi + "Shelf with Crates.prefab", RealScale, 45f, 86f, 90f);
            Place(furniture, Scifi + "Tech Accessories/Server Rack.prefab", RealScale, 10f, 10f, 0f);
            Place(furniture, Scifi + "Chairs and Stools/Office Chair.prefab", RealScale, 50f, 60f, 200f);
            Place(furniture, Scifi + "Carpets and Floors/Carpet 11.prefab", RealScale, 55f, 45f, 90f);
            Place(furniture, Electronics + "pc.prefab", RealScale, 92f, 82f, -90f);

            // Предметы на столе
            var onDesk = Group(room, "DeskItems");
            Place(onDesk, Electronics + "monitor.prefab", RealScale, deskCx + 5f, 30f, -90f, deskTop);
            Place(onDesk, Electronics + "keyboard.prefab", RealScale, deskCx - 3f, 30f, -90f, deskTop);
            Place(onDesk, Electronics + "mouse.prefab", RealScale, deskCx - 3f, 20f, -90f, deskTop);
            Place(onDesk, Nappin + "(Prb)DeskLight.prefab", NappinScale, deskCx + 6f, deskBounds.min.z + 8f, -90f, deskTop);
            BookStack(onDesk, "Books_OnDesk", new Vector3(deskCx + 6f, deskTop, 45f), 5, new Vector2(6f, 8f));

            BuildParkourRoute(room, deskFront, deskTop);
            BuildMachine(room, new Vector3(deskCx, deskTop, deskBounds.max.z - 12f), new Vector3(deskCx - 2f, deskTop, deskBounds.max.z - 27f));
        }

        // Маршрут: книжные стопки → коробки → ящик с инструментами → линейка-пандус на стол.
        static void BuildParkourRoute(Transform room, float deskFront, float deskTop)
        {
            var route = Group(room, "ParkourRoute");
            const float step = BookThickness * 2f;  // 1.5 м на ступень
            const float spacing = 8f;
            const float crateHeight = 7.5f;          // коробка ~30 см

            float toolboxTop = 12f;
            var toolbox = new Vector3(deskFront - 26f, 0f, 45f);
            Box(route, "Toolbox", toolbox + Vector3.up * toolboxTop * 0.5f, new Vector3(12f, toolboxTop, 12f), mats["Toolbox"]);
            Box(route, "Toolbox_Handle", toolbox + new Vector3(0f, toolboxTop + 0.6f, 0f), new Vector3(1f, 1.2f, 7f), mats["MachineDark"]);

            // Ступени: сначала на юг от ящика, затем поворот на запад
            int count = Mathf.FloorToInt((toolboxTop - 0.01f) / step);
            Vector3 pos = toolbox + new Vector3(0f, 0f, -6f - 1.5f - 3.5f);
            for (int i = 0; i < count; i++)
            {
                float top = toolboxTop - step * (i + 1);
                string name = $"Step_{count - i:00}";
                if (top > crateHeight + 0.01f)
                {
                    var crate = Crate(route, name + "_Crate", pos, crateHeight);
                    BookStack(route, name + "_Books", pos + Vector3.up * crateHeight, Mathf.RoundToInt((top - crateHeight) / BookThickness), new Vector2(6f, 6.5f));
                }
                else if (Mathf.Approximately(top, crateHeight))
                    Crate(route, name + "_Crate", pos, crateHeight);
                else
                    BookStack(route, name + "_Books", pos, Mathf.RoundToInt(top / BookThickness), new Vector2(6f, 7f));

                pos += i < 1 ? new Vector3(0f, 0f, -spacing) : new Vector3(-spacing, 0f, 0f);
            }

            // Линейка, прислонённая к краю стола
            var start = toolbox + new Vector3(4f, toolboxTop, 0f);
            var end = new Vector3(deskFront + 2.5f, deskTop, toolbox.z);
            Vector3 dir = end - start;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, 0f, angle);
            const float thick = 0.4f;
            var center = (start + end) * 0.5f - rot * Vector3.up * thick * 0.5f;
            var ruler = Box(route, "Ruler_Ramp", center, new Vector3(dir.magnitude + 0.5f, thick, 4f), mats["Ruler"]);
            ruler.transform.rotation = rot;
            for (int t = 1; t < 12; t++)
            {
                float along = -dir.magnitude * 0.5f + t * dir.magnitude / 12f;
                var mark = Box(ruler.transform, "Mark", Vector3.zero, Vector3.one, mats["MachineDark"]);
                Object.DestroyImmediate(mark.GetComponent<Collider>());
                mark.transform.localPosition = new Vector3(along / (dir.magnitude + 0.5f), 0.51f, 0.35f);
                mark.transform.localScale = new Vector3(0.12f / (dir.magnitude + 0.5f), 0.02f, 0.3f);
            }
        }

        // Заглушка прибора увеличения: платформа-капсула + пульт с переключателем режима и кнопкой.
        static void BuildMachine(Transform room, Vector3 machinePos, Vector3 panelPos)
        {
            var machine = Group(room, "EnlargeMachine");
            machine.position = machinePos;

            Cylinder(machine, "Base", new Vector3(0f, 0.5f, 0f), 6f, 1f, mats["Machine"]);
            Cylinder(machine, "Pad", new Vector3(0f, 1.15f, 0f), 4.5f, 0.3f, mats["Glow"]);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Cylinder(machine, $"Pillar_{i}", new Vector3(Mathf.Cos(a) * 5.2f, 5.5f, Mathf.Sin(a) * 5.2f), 0.4f, 9f, mats["Machine"]);
            }
            Cylinder(machine, "Top", new Vector3(0f, 10.3f, 0f), 6f, 0.8f, mats["Machine"]);
            Cylinder(machine, "Emitter", new Vector3(0f, 9.6f, 0f), 2f, 0.6f, mats["Glow"]);

            var glow = new GameObject("PadLight").AddComponent<Light>();
            glow.transform.SetParent(machine, false);
            glow.transform.localPosition = new Vector3(0f, 4f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(0.3f, 0.9f, 1f);
            glow.range = 25f;
            glow.intensity = 60f;

            var panel = Group(room, "ControlPanel");
            panel.position = panelPos;
            var body = Box(panel, "Body", new Vector3(0f, 2f, 0f), new Vector3(5f, 4f, 8f), mats["Machine"]);
            Box(panel, "Face", new Vector3(-2.3f, 4.2f, 0f), new Vector3(3f, 0.6f, 7f), mats["MachineDark"]).transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            var dial = Cylinder(panel, "ModeDial", new Vector3(-2.6f, 4.9f, -1.8f), 1.1f, 0.5f, mats["MachineDark"]);
            dial.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            Box(dial.transform, "Pointer", new Vector3(0f, 0.6f, 0.4f), new Vector3(0.15f, 0.3f, 0.8f), mats["Glow"]);
            var button = Cylinder(panel, "BigButton", new Vector3(-2.6f, 4.9f, 1.8f), 1.2f, 0.6f, mats["Button"]);
            button.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            body.isStatic = true;
        }

        // ---------- Спальня ----------

        static void FurnishBedroom(Transform room)
        {
            var f = Group(room, "Furniture");
            var bed = Place(f, Nappin + "(Prb)DoubleBed.prefab", NappinScale, -72f, 58f, -90f);
            var bedB = WorldBounds(bed);
            Place(f, Nappin + "(Prb)BedsideTable.prefab", NappinScale, -94f, bedB.min.z - 6f, 90f);
            var bedside = Place(f, Nappin + "(Prb)BedsideTable.prefab", NappinScale, -94f, bedB.max.z + 6f, 90f);
            Place(f, Nappin + "(Prb)BedsideLight.prefab", NappinScale, -94f, bedB.max.z + 6f, 90f, WorldBounds(bedside).max.y);
            Place(f, Nappin + "(Prb)Wardrobe.prefab", NappinScale, -55f, 13f, -90f);
            Place(f, Nappin + "(Prb)Dresser.prefab", NappinScale, -40f, 93f, -90f);
            Place(f, Nappin + "(Prb)Drawer1.prefab", NappinScale, -15f, 93f, -90f);
            Place(f, Nappin + "(Prb)Lamp.prefab", NappinScale, -10f, 10f, 0f);
            Place(f, Scifi + "Carpets and Floors/Carpet 8.prefab", RealScale, -45f, 55f, 0f);

            // Стопка журналов у кровати — заодно укрытие для батарейки
            BookStack(f, "Magazines", new Vector3(-30f, 0f, 42f), 3, new Vector2(8f, 10f));
            Pickup(room, "Pickup_Battery", new Vector3(-30f, 0f, 34f), true);
            SetStatic(f.gameObject);
        }

        // ---------- Кухня ----------

        static void FurnishKitchen(Transform room)
        {
            var f = Group(room, "Furniture");
            Place(f, Nappin + "(Prb)Fridge.prefab", NappinScale, 108f, 168f, 180f);
            Place(f, Nappin + "(Prb)KitchenSink.prefab", NappinScale, 72f, 172f, 180f);
            Place(f, Nappin + "(Prb)Stove.prefab", NappinScale, 45f, 178f, 180f);
            Place(f, Nappin + "(Prb)KitchenIsland.prefab", NappinScale, 60f, 132f, 90f);
            var table = Place(f, Nappin + "(Prb)LaunchTable.prefab", NappinScale, 15f, 140f, 0f);
            float tableTop = WorldBounds(table).max.y;
            Place(f, Nappin + "(Prb)DiningChair.prefab", NappinScale, 15f, 108f, 0f);
            Place(f, Nappin + "(Prb)DiningChair.prefab", NappinScale, 15f, 172f, 180f);
            Place(f, Nappin + "(Prb)LaunchTable_vase1.prefab", NappinScale, 15f, 132f, 0f, tableTop);
            Place(f, Nappin + "(Prb)LaunchTable_vase3.prefab", NappinScale, 15f, 150f, 0f, tableTop);

            // Шестерёнка закатилась в щель между раковиной и холодильником
            Pickup(room, "Pickup_Gear", new Vector3(90.5f, 0f, 182f), false);
            SetStatic(f.gameObject);
        }

        // ---------- Свет и игрок ----------

        static void BuildLighting(Transform level)
        {
            var lights = Group(level, "Lights");

            var sun = new GameObject("Directional Light (fill)").AddComponent<Light>();
            sun.transform.SetParent(lights, false);
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            sun.type = LightType.Directional;
            sun.intensity = 0.35f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.None;

            // Тени только у лампы мастерской: три точечных источника с тенями на таком масштабе
            // роняли D3D12-драйвер ноутбучной видеокарты.
            CeilingLight(lights, "Light_Bedroom", new Vector3(-50f, RoomHeight - 6f, 50f), new Color(1f, 0.85f, 0.7f), false);
            CeilingLight(lights, "Light_Workshop", new Vector3(62f, RoomHeight - 6f, 50f), new Color(1f, 0.97f, 0.92f), true);
            CeilingLight(lights, "Light_Kitchen", new Vector3(62f, RoomHeight - 6f, 145f), new Color(1f, 0.95f, 0.85f), false);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.55f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.4f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.2f);

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null && !Mathf.Approximately(urp.shadowDistance, 150f))
            {
                urp.shadowDistance = 150f;
                EditorUtility.SetDirty(urp);
            }
        }

        static void CeilingLight(Transform parent, string name, Vector3 pos, Color color, bool shadows)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = color;
            l.range = 180f;
            l.intensity = 2500f;
            l.shadows = shadows ? LightShadows.Hard : LightShadows.None;
            Cylinder(l.transform, "Lamp", new Vector3(0f, 4f, 0f), 6f, 1f, mats["LampGlow"]).GetComponent<Collider>().enabled = false;
        }

        static GameObject BuildPlayer(Vector3 position, float yaw)
        {
            var player = new GameObject("Player") { tag = "Player", layer = 2 }; // Ignore Raycast
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.05f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.transform.SetParent(pivot, false);
            var camera = cam.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1000f;
            camera.fieldOfView = 70f;
            cam.AddComponent<AudioListener>();
            if (cam.GetComponent<UniversalAdditionalCameraData>() == null) cam.AddComponent<UniversalAdditionalCameraData>();

            var controller = player.AddComponent<TinyPlayerController>();
            controller.cameraPivot = pivot;
            return player;
        }

        // ---------- Пропсы ----------

        static float BookStack(Transform parent, string name, Vector3 basePos, int count, Vector2 footprint)
        {
            var stack = Group(parent, name);
            string[] colors = { "BookRed", "BookBlue", "BookGreen", "BookBeige", "BookBrown" };
            for (int i = 0; i < count; i++)
            {
                float sx = footprint.x * Rand(0.88f, 1f), sz = footprint.y * Rand(0.88f, 1f);
                var center = basePos + new Vector3(Rand(-0.35f, 0.35f), BookThickness * (i + 0.5f), Rand(-0.35f, 0.35f));
                var book = Box(stack, $"Book_{i}", center, new Vector3(sx, BookThickness, sz), mats[colors[rnd.Next(colors.Length)]]);
                book.transform.rotation = Quaternion.Euler(0f, Rand(-10f, 10f), 0f);
            }
            return basePos.y + count * BookThickness;
        }

        static GameObject Crate(Transform parent, string name, Vector3 basePos, float height)
        {
            var crate = Box(parent, name, basePos + Vector3.up * height * 0.5f, new Vector3(7f, height, 7f), mats["Cardboard"]);
            Box(crate.transform, "Tape", new Vector3(0f, 0.5f, 0f), new Vector3(0.15f, 0.02f, 1.002f), mats["Tape"]).GetComponent<Collider>().enabled = false;
            return crate;
        }

        static void Pickup(Transform parent, string name, Vector3 floorPos, bool battery)
        {
            GameObject go;
            if (battery)
            {
                go = Cylinder(parent, name, floorPos + Vector3.up * 0.6f, 1.2f, 3f, mats["Battery"]);
                go.transform.rotation = Quaternion.Euler(0f, 30f, 90f);
            }
            else
            {
                go = Cylinder(parent, name, floorPos + Vector3.up * 0.2f, 3f, 0.4f, mats["Gear"]);
                for (int i = 0; i < 8; i++)
                {
                    var tooth = Box(go.transform, "Tooth", Vector3.zero, Vector3.one, mats["Gear"]);
                    Object.DestroyImmediate(tooth.GetComponent<Collider>());
                    float a = i * 45f;
                    tooth.transform.localRotation = Quaternion.Euler(0f, a, 0f);
                    tooth.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 0.55f);
                    tooth.transform.localScale = new Vector3(0.18f, 1f, 0.15f);
                }
            }
            go.GetComponent<Collider>().isTrigger = true;
        }

        // ---------- Утилиты ----------

        static GameObject Place(Transform parent, string path, float scale, float x, float z, float yaw, float baseY = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[LevelBuilder] Prefab not found: " + path);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localScale = prefab.transform.localScale * scale;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = Vector3.zero;
            var b = WorldBounds(go);
            go.transform.position = new Vector3(x - b.center.x, baseY - b.min.y, z - b.center.z);

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            return go;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static GameObject Cylinder(Transform parent, string name, Vector3 center, float radius, float height, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
            return go;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static void SetStatic(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        }

        static float Rand(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------- Материалы ----------

        static void CreateMaterials()
        {
            mats.Clear();
            var floorTex = AssetDatabase.LoadAssetAtPath<Material>("Assets/nappin/HouseInteriorPack/Materials/(Mat)Floor.mat")?.GetTexture("_BaseMap");

            Mat("WallBedroom", new Color(0.62f, 0.72f, 0.82f));
            Mat("WallWorkshop", new Color(0.70f, 0.74f, 0.66f));
            Mat("WallKitchen", new Color(0.93f, 0.88f, 0.74f));
            Mat("Ceiling", new Color(0.95f, 0.95f, 0.93f));
            Mat("Trim", new Color(0.92f, 0.92f, 0.9f));
            Mat("FloorWood", floorTex ? Color.white : new Color(0.55f, 0.38f, 0.24f), 0f, 0.35f, null, floorTex, new Vector2(6f, 6f));
            Mat("FloorWorkshop", floorTex ? new Color(0.8f, 0.75f, 0.7f) : new Color(0.45f, 0.33f, 0.24f), 0f, 0.3f, null, floorTex, new Vector2(7f, 6f));
            Mat("FloorKitchen", new Color(0.78f, 0.78f, 0.76f), 0f, 0.6f);

            Mat("BookRed", new Color(0.65f, 0.15f, 0.12f));
            Mat("BookBlue", new Color(0.15f, 0.28f, 0.55f));
            Mat("BookGreen", new Color(0.18f, 0.45f, 0.25f));
            Mat("BookBeige", new Color(0.85f, 0.78f, 0.6f));
            Mat("BookBrown", new Color(0.42f, 0.27f, 0.16f));
            Mat("Cardboard", new Color(0.72f, 0.55f, 0.35f));
            Mat("Tape", new Color(0.85f, 0.75f, 0.55f), 0f, 0.7f);
            Mat("Toolbox", new Color(0.75f, 0.12f, 0.1f), 0.6f, 0.55f);
            Mat("Ruler", new Color(0.95f, 0.8f, 0.25f), 0f, 0.5f);

            Mat("Machine", new Color(0.62f, 0.65f, 0.7f), 0.85f, 0.6f);
            Mat("MachineDark", new Color(0.12f, 0.12f, 0.14f), 0.5f, 0.5f);
            Mat("Glow", new Color(0.2f, 0.8f, 1f), 0f, 0.8f, new Color(0.2f, 0.8f, 1f) * 3f);
            Mat("Button", new Color(0.9f, 0.1f, 0.08f), 0f, 0.7f, new Color(1f, 0.1f, 0.05f) * 1.5f);
            Mat("LampGlow", Color.white, 0f, 0.5f, new Color(1f, 0.95f, 0.85f) * 4f);
            Mat("Battery", new Color(0.95f, 0.75f, 0.1f), 0.7f, 0.6f, new Color(0.9f, 0.7f, 0.05f) * 0.6f);
            Mat("Gear", new Color(0.8f, 0.6f, 0.25f), 0.9f, 0.6f, new Color(0.8f, 0.55f, 0.1f) * 0.5f);
            AssetDatabase.SaveAssets();
        }

        static Material Mat(string name, Color color, float metallic = 0f, float smoothness = 0.3f,
            Color? emission = null, Texture texture = null, Vector2? tiling = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            if (texture != null)
            {
                m.SetTexture("_BaseMap", texture);
                m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        // ---------- Конвертация материалов паков (Standard → URP Lit) ----------

        [MenuItem("Tools/HouseCreators/Convert Standard Materials to URP")]
        public static void ConvertStandardMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            int converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || !m.shader.name.StartsWith("Standard")) continue;

                bool specular = m.shader.name.Contains("Specular");
                var mainTex = m.GetTexture("_MainTex");
                var color = m.GetColor("_Color");
                var tiling = m.GetTextureScale("_MainTex");
                var offset = m.GetTextureOffset("_MainTex");
                float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f;
                var bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                var metalMap = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
                var specMap = m.HasProperty("_SpecGlossMap") ? m.GetTexture("_SpecGlossMap") : null;
                var emissionMap = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
                var emissionColor = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                var occlusion = m.HasProperty("_OcclusionMap") ? m.GetTexture("_OcclusionMap") : null;
                bool emissive = m.IsKeywordEnabled("_EMISSION") || emissionMap != null;

                m.shader = lit;
                m.SetTexture("_BaseMap", mainTex);
                m.SetTextureScale("_BaseMap", tiling);
                m.SetTextureOffset("_BaseMap", offset);
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", gloss);
                m.SetFloat("_WorkflowMode", specular ? 0f : 1f);
                if (specular) { m.EnableKeyword("_SPECULAR_SETUP"); if (specMap) { m.SetTexture("_SpecGlossMap", specMap); m.EnableKeyword("_METALLICSPECGLOSSMAP"); } }
                else if (metalMap) { m.SetTexture("_MetallicGlossMap", metalMap); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                if (bump) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
                if (occlusion) { m.SetTexture("_OcclusionMap", occlusion); m.EnableKeyword("_OCCLUSIONMAP"); }
                if (emissive)
                {
                    m.SetTexture("_EmissionMap", emissionMap);
                    m.SetColor("_EmissionColor", emissionColor);
                    m.EnableKeyword("_EMISSION");
                }
                EditorUtility.SetDirty(m);
                converted++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelBuilder] Converted {converted} Standard materials to URP Lit");
        }
    }
}
