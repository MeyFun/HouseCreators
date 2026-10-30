using System.Collections.Generic;
using System.Linq;
using System.Text;
using HouseCreators.Blockout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HouseCreators.EditorTools
{
    // Мастерская учёного: он зашёл в капсулу, включил её и уменьшился.
    // Чтобы переключить капсулу в режим увеличения, нужно добраться до пульта — длинный паркур через всю комнату.
    // Масштаб: игрок 1.8 м, обстановка ×25 (стол 25 м, потолок 70 м, чердак над потолком 71–77 м).
    // Все объекты с коллайдерами.
    //
    // Маршрут:
    //  1. Капсула → пол: книги, коробки, банка краски, ящик с инструментами, кресло, ведро
    //  2. Комод с выдвинутыми ящиками-лесенкой → рабочий стол
    //  3. Стопки на столе → нижняя полка → стопки → верхняя полка → стопки
    //  4. Книжный шкаф (4-й ярус): барьер, провал 6 м, лаз под упавшей книгой, стопки
    //  5. Наличник двери → платяной шкаф → чемодан, шляпная коробка, коробки лесенкой → люк в потолке
    //  6. Чердак: балки, утеплитель, труба, вентиляционный короб (ползком)
    //  7. Корпус потолочного светильника (ползком) → разбитый рассеиватель → падение на пульт
    //  8. Уступы пульта → переключатель режима и кнопка
    public static class WorkshopRoomBuilder
    {
        const string ScenePath = "Assets/Scenes/Workshop_Room.unity";
        const string MatDir = "Assets/WorkshopRoom/Materials";

        const float W = 150f, D = 125f, H = 70f, T = 1.5f;
        const float AtticFloor = 71f, AtticRoof = 77f;
        const float Book = 0.75f;
        const float CrawlGap = 1.3f;

        static readonly Rect Hatch = new Rect(2, 104, 16, 14);           // люк на чердак (x 2–18, z 104–118)
        static readonly Rect LampHousing = new Rect(66, 37, 10, 45);      // корпус светильника (x 66–76, z 37–82)
        static readonly Rect LampHole = new Rect(67, 37, 8, 7);           // разбитый рассеиватель (x 67–75, z 37–44)

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static System.Random rnd;

        struct Node
        {
            public Vector3 pos;
            public bool jump, run, crouch;
            public string name;
            public Node(string name, float x, float y, float z, bool jump = false, bool run = false, bool crouch = false)
            {
                this.name = name;
                pos = new Vector3(x, y, z);
                this.jump = jump;
                this.run = run;
                this.crouch = crouch;
            }
        }

        // Маршрут бота: точка на поверхности + прыжок / бег (Ctrl) / присед (Shift), нужные чтобы туда попасть.
        static readonly Node[] Route =
        {
            new Node("Старт в капсуле", 32, 1.5f, 40),
            new Node("Дверь капсулы", 39, 1.5f, 33),
            new Node("Пол", 46, 0, 26),
            // 1. Пол
            new Node("Книги 1.5", 33, 1.5f, 10, jump: true),
            new Node("Книги 3", 42, 3f, 10, jump: true),
            new Node("Обувная коробка 4.5", 52.5f, 4.5f, 10, jump: true),
            new Node("Коробка 6", 64, 6f, 10, jump: true),
            new Node("Коробка 7.5", 75, 7.5f, 10, jump: true),
            new Node("Банка краски 9", 85, 9f, 9, jump: true),
            new Node("Ящик с инструментами 10.5", 74, 10.5f, 20, jump: true, run: true),
            new Node("Сиденье кресла 12", 84, 12f, 21, jump: true),
            new Node("Подлокотник 13.5", 86, 13.5f, 25.5f, jump: true),
            new Node("Мусорное ведро 15", 85, 15f, 32, jump: true),
            // 2. Комод-лесенка → стол
            new Node("Ящик 16.5", 91.5f, 16.5f, 39, jump: true),
            new Node("Ящик 18", 94.5f, 18f, 39, jump: true),
            new Node("Ящик 19.5", 97.5f, 19.5f, 39, jump: true),
            new Node("Ящик 21", 100.5f, 21f, 39, jump: true),
            new Node("Ящик 22.5", 103.5f, 22.5f, 39, jump: true),
            new Node("Ящик 24", 106.5f, 24f, 39, jump: true),
            new Node("Крышка комода 25.5", 112, 25.5f, 39, jump: true),
            new Node("Рабочий стол 25", 127, 25f, 40, jump: true),
            // 3. Стол и полки
            new Node("Вдоль стола", 128.5f, 25f, 66),
            new Node("Книги 26.5", 135.5f, 26.5f, 65, jump: true),
            new Node("Книги 28", 135.5f, 28f, 57, jump: true),
            new Node("Книги 29.5", 135.5f, 29.5f, 49, jump: true),
            new Node("Книги 31", 135.5f, 31f, 41, jump: true),
            new Node("Нижняя полка 32.5", 144.5f, 32.5f, 34, jump: true),
            new Node("Книги 34", 144.5f, 34f, 45, jump: true),
            new Node("Книги 35.5", 144.5f, 35.5f, 53, jump: true),
            new Node("Книги 37", 144.5f, 37f, 61, jump: true),
            new Node("Книги 38.5", 144.5f, 38.5f, 69, jump: true),
            new Node("Коробка 40", 144.5f, 40f, 77, jump: true),
            new Node("Верхняя полка 41.5", 144.5f, 41.5f, 85, jump: true),
            new Node("Книги 43", 144.5f, 43f, 91, jump: true),
            new Node("Книги 44.5", 144.5f, 44.5f, 99, jump: true),
            new Node("Книги 46", 144.5f, 46f, 107, jump: true),
            new Node("Книги 47.5", 144.5f, 47.5f, 115, jump: true),
            // 4. Книжный шкаф
            new Node("Книжный шкаф 49", 137, 49f, 113, jump: true),
            new Node("Барьер из книг 50.5", 130, 50.5f, 112, jump: true),
            new Node("Край перед провалом", 124.6f, 49f, 112),
            new Node("Провал в полке 6 м", 114, 49f, 112, jump: true, run: true),
            new Node("Лаз под упавшей книгой", 103.8f, 49f, 113, crouch: true),
            new Node("Стопка 50.5", 100, 50.5f, 113.5f, jump: true),
            new Node("Стопка 52", 95.5f, 52f, 115.5f, jump: true),
            new Node("Стопка 53.5", 91.5f, 53.5f, 115, jump: true),
            new Node("Стопка 55", 91.5f, 55f, 121, jump: true),
            // 5. Наличник → шкаф → люк
            new Node("Наличник двери 54.5", 85, 54.5f, 122.6f, jump: true),
            new Node("По наличнику", 57.5f, 54.5f, 122.6f),
            new Node("Платяной шкаф 56", 47, 56f, 120, jump: true, run: true),
            new Node("Чемодан 57.5", 41, 57.5f, 112, jump: true),
            new Node("Шляпная коробка 59", 30, 59f, 112, jump: true),
            new Node("Коробка 60.5", 23.5f, 60.5f, 111, jump: true),
            new Node("Коробка 62", 20.5f, 62f, 111, jump: true),
            new Node("Коробка 63.5", 17.5f, 63.5f, 111, jump: true),
            new Node("Коробка 65", 14.5f, 65f, 111, jump: true),
            new Node("Коробка 66.5", 11.5f, 66.5f, 111, jump: true),
            new Node("Коробка 68", 8.5f, 68f, 111, jump: true),
            new Node("Коробка 69.5", 5.5f, 69.5f, 113, jump: true),
            new Node("Чердак 71", 5.5f, 71f, 121, jump: true, run: true),
            // 6. Чердак
            new Node("Через балку 1", 35, 71f, 120.5f, jump: true),
            new Node("Через утеплитель", 42.5f, 71f, 120.5f, jump: true),
            new Node("Через балку 2", 51, 71f, 120.5f, jump: true),
            new Node("Через балку 3", 66, 71f, 120.5f, jump: true),
            new Node("Поворот к светильнику", 71, 71f, 113),
            new Node("Через трубу", 71, 71f, 103, jump: true),
            new Node("Под вентиляционным коробом", 71, 71f, 91, crouch: true),
            new Node("Вход в светильник", 71, 71f, 84),
            // 7. Светильник
            new Node("Внутри светильника", 71, 71f, 46, crouch: true),
            new Node("Падение на пульт 56", 71, 56f, 40, crouch: true),
            // 8. Пульт
            new Node("Уступ индикаторов 46", 68, 46f, 27.5f),
            new Node("Панель управления 34", 68, 34f, 22),
            new Node("Переключатель режима", 64.5f, 34f, 24),
            new Node("КНОПКА", 72, 34f, 24.5f),
        };

        [MenuItem("Tools/HouseCreators/Workshop: Build Room")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            rnd = new System.Random(11);
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var room = new GameObject("Workshop").transform;

            Shell(Group(room, "Shell"));
            Attic(Group(room, "Attic"));
            Decor(Group(room, "Decor"));
            Capsule(Group(room, "ShrinkCapsule"));
            ControlConsole(Group(room, "CapsuleConsole"));
            FloorRoute(Group(room, "Route_1_Floor"));
            ChestOfDrawers(Group(room, "Route_2_Chest"));
            Desk(Group(room, "Route_3_Desk_Shelves"));
            Bookcase(Group(room, "Route_4_Bookcase"));
            DoorAndWardrobe(Group(room, "Route_5_Door_Wardrobe"));
            Lighting(Group(room, "Lights"));

            var routeGizmo = new GameObject("RouteGizmo").AddComponent<BlockoutRoute>();
            routeGizmo.transform.SetParent(room, false);
            routeGizmo.color = new Color(1f, 0.3f, 0.8f);
            routeGizmo.points = Route.Select(n => n.pos + Vector3.up * 0.3f).ToArray();

            BuildPlayer(Route[0].pos + Vector3.up * 0.05f, 135f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log("[Workshop] Built " + ScenePath);
            ValidateRoute();
        }

        // ================= Комната и чердак =================

        static void Shell(Transform p)
        {
            Box(p, "Floor", V(0, -1, 0), V(W, 0, D), "FloorWood");
            Box(p, "Wall_S", V(0, 0, 0), V(W, AtticRoof + 1, T), "Wall");
            Box(p, "Wall_N", V(0, 0, D - T), V(W, AtticRoof + 1, D), "Wall");
            Box(p, "Wall_W", V(0, 0, T), V(T, AtticRoof + 1, D - T), "Wall");
            Box(p, "Wall_E", V(W - T, 0, T), V(W, AtticRoof + 1, D - T), "Wall");
            Box(p, "Baseboard_S", V(T, 0, T), V(W - T, 3, T + 0.5f), "Trim");
            Box(p, "Baseboard_N", V(T, 0, D - T - 0.5f), V(W - T, 3, D - T), "Trim");
            Box(p, "Baseboard_W", V(T, 0, T), V(T + 0.5f, 3, D - T), "Trim");
            Box(p, "Baseboard_E", V(W - T - 0.5f, 0, T), V(W - T, 3, D - T), "Trim");
            Box(p, "Cornice_S", V(T, H - 2, T), V(W - T, H, T + 1), "Trim");
            Box(p, "Cornice_E", V(W - T - 1, H - 2, T), V(W - T, H, D - T), "Trim");

            // Потолок с люком и проёмом под светильник
            var ceiling = Group(p, "Ceiling");
            SlabWithHoles(ceiling, "Ceiling", new Rect(T, T, W - 2 * T, D - 2 * T), H, AtticFloor, "Ceiling", Hatch, LampHousing);
            SlabWithHoles(ceiling, "LampDiffuser", LampHousing, H, AtticFloor, "LampGlow", LampHole);
            Box(ceiling, "Hatch_Frame_S", V(Hatch.xMin - 0.6f, H - 0.6f, Hatch.yMin - 0.6f), V(Hatch.xMax + 0.6f, H, Hatch.yMin), "Trim");
            Box(ceiling, "Hatch_Frame_E", V(Hatch.xMax, H - 0.6f, Hatch.yMin), V(Hatch.xMax + 0.6f, H, Hatch.yMax), "Trim");
            Box(ceiling, "Hatch_Frame_N", V(Hatch.xMin - 0.6f, H - 0.6f, Hatch.yMax), V(Hatch.xMax + 0.6f, H, Hatch.yMax + 0.6f), "Trim");
            Box(ceiling, "Diffuser_Rim_W", V(LampHousing.xMin - 0.8f, H - 0.8f, LampHousing.yMin), V(LampHousing.xMin, H, LampHousing.yMax), "MetalLight");
            Box(ceiling, "Diffuser_Rim_E", V(LampHousing.xMax, H - 0.8f, LampHousing.yMin), V(LampHousing.xMax + 0.8f, H, LampHousing.yMax), "MetalLight");
            Box(ceiling, "Diffuser_Shard_1", V(68, H - 3, 38), V(70, H - 0.2f, 38.6f), "Glass");
            Box(ceiling, "Diffuser_Shard_2", V(72.5f, H - 2, 43), V(74.5f, H - 0.2f, 43.6f), "Glass");
            foreach (var t in p.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        }

        static void Attic(Transform p)
        {
            Box(p, "Roof", V(0, AtticRoof, 0), V(W, AtticRoof + 1, D), "AtticWood");
            SlabWithHoles(p, "Boards", new Rect(T, T, W - 2 * T, D - 2 * T), AtticFloor, AtticFloor + 0.05f, "AtticBoards", Hatch, LampHousing);

            // Балки перекрытия вдоль Z
            foreach (var x in new[] { 29f, 44f, 59f, 89f, 104f, 119f, 134f })
                Box(p, "Joist_" + x, V(x, AtticFloor, T), V(x + 2, AtticFloor + 1.5f, D - T), "AtticWood");

            // Утеплитель, труба, вентиляционный короб, провода, мышеловка
            Box(p, "Insulation_Bale", V(36.5f, AtticFloor, 117), V(39.5f, AtticFloor + 1.5f, D - T), "Insulation");
            Box(p, "Insulation_Pile_1", V(8, AtticFloor, 30), V(26, AtticFloor + 2.5f, 60), "Insulation");
            Box(p, "Insulation_Pile_2", V(95, AtticFloor, 60), V(115, AtticFloor + 2, 90), "Insulation");
            var pipe = Cyl(p, "WaterPipe", V(0, 0, 0), 0.8f, W - 2 * T, "Copper");
            pipe.transform.rotation = Quaternion.Euler(0, 0, 90);
            pipe.transform.position = V(W * 0.5f, AtticFloor + 0.8f, 108);
            Box(p, "VentDuct", V(40, AtticFloor + CrawlGap, 95), V(100, AtticRoof, 100), "Duct");
            Box(p, "VentDuct_Seam_1", V(55, AtticFloor + CrawlGap - 0.2f, 94.8f), V(55.6f, AtticRoof, 100.2f), "MetalDark");
            Box(p, "VentDuct_Seam_2", V(85, AtticFloor + CrawlGap - 0.2f, 94.8f), V(85.6f, AtticRoof, 100.2f), "MetalDark");
            Box(p, "Cable_Run", V(T, AtticFloor, 72), V(66, AtticFloor + 0.3f, 73), "Cable");
            Box(p, "MouseTrap", V(50, AtticFloor, 112), V(56, AtticFloor + 0.6f, 115), "WoodLight");
            Box(p, "MouseTrap_Bar", V(50.5f, AtticFloor + 0.6f, 112.3f), V(55.5f, AtticFloor + 0.9f, 112.6f), "Metal");
            Sphere(p, "DustBunny_1", V(20, AtticFloor + 0.8f, 98), 0.8f, "Dust");
            Sphere(p, "DustBunny_2", V(80, AtticFloor + 0.6f, 118), 0.6f, "Dust");

            // Корпус потолочного светильника: внутри ползком (высота 1.3)
            var lamp = Group(p, "LampHousing");
            float x0 = LampHousing.xMin, x1 = LampHousing.xMax, z0 = LampHousing.yMin, z1 = LampHousing.yMax;
            float top = AtticFloor + CrawlGap;
            Box(lamp, "Wall_W", V(x0, AtticFloor, z0), V(x0 + 0.5f, top + 0.5f, z1), "MetalLight");
            Box(lamp, "Wall_E", V(x1 - 0.5f, AtticFloor, z0), V(x1, top + 0.5f, z1), "MetalLight");
            Box(lamp, "Wall_S", V(x0, AtticFloor, z0), V(x1, top + 0.5f, z0 + 0.5f), "MetalLight");
            Box(lamp, "Top", V(x0, top, z0), V(x1, top + 0.5f, z1), "MetalLight");
            Box(lamp, "Ballast", V(x0 + 2, top + 0.5f, 60), V(x1 - 2, top + 1.5f, 66), "MetalDark");
            foreach (var tx in new[] { x0 + 1.3f, x1 - 1.3f })
            {
                var tube = Cyl(lamp, "Tube", V(0, 0, 0), 0.5f, z1 - z0 - 10, "TubeGlow");
                tube.transform.rotation = Quaternion.Euler(90, 0, 0);
                tube.transform.position = V(tx, AtticFloor + 0.5f, (z0 + 8 + z1) * 0.5f);
            }
            Label(p, "Чердак: балки, утеплитель, труба, под коробом — ползком", V(40, AtticFloor + 4, 118));
            Label(p, "Корпус светильника: ползком до разбитого рассеивателя", V(71, AtticFloor + 4, 80));
        }

        static void Decor(Transform p)
        {
            // Окно, шторы, батарея
            var window = Group(p, "Window");
            Box(window, "Glass_Sky", V(30, 22, T), V(80, 52, T + 0.1f), "WindowSky");
            Box(window, "Sill", V(28, 20.5f, T), V(82, 22, T + 2.5f), "Trim");
            Box(window, "Frame_Top", V(29, 51.5f, T), V(81, 53, T + 0.8f), "Trim");
            Box(window, "Frame_L", V(29, 22, T), V(30.5f, 52, T + 0.8f), "Trim");
            Box(window, "Frame_R", V(79.5f, 22, T), V(81, 52, T + 0.8f), "Trim");
            Box(window, "Frame_Mid", V(54.3f, 22, T), V(55.7f, 52, T + 0.6f), "Trim");
            Box(window, "Frame_Cross", V(30, 36.3f, T), V(80, 37.7f, T + 0.6f), "Trim");
            Box(window, "Curtain_L", V(22, 18, T + 0.5f), V(29, 56, T + 2.5f), "Curtain");
            Box(window, "Curtain_R", V(81, 18, T + 0.5f), V(88, 56, T + 2.5f), "Curtain");
            Box(window, "CurtainRod", V(18, 57, T + 1f), V(92, 58, T + 2f), "Metal");
            var radiator = Group(p, "Radiator");
            for (int i = 0; i < 14; i++)
                Box(radiator, "Fin_" + i, V(36 + i * 2.8f, 4, T + 0.6f), V(37.8f + i * 2.8f, 17, T + 3.2f), "Radiator");

            // Часы, чертёж капсулы, перфопанель
            var clock = Cyl(p, "Clock", V(0, 0, 0), 4f, 0.6f, "ClockFace");
            clock.transform.rotation = Quaternion.Euler(90, 0, 0);
            clock.transform.position = V(110, 58, D - T - 0.3f);
            var poster = Group(p, "BlueprintPoster");
            Box(poster, "Paper", V(W - T - 0.2f, 46, 84), V(W - T, 62, 104), "Blueprint");
            for (int i = 0; i < 4; i++)
                Box(poster, "Line_" + i, V(W - T - 0.3f, 49 + i * 3, 87 + i), V(W - T - 0.2f, 49.4f + i * 3, 101 - i * 2), "BlueprintLine");
            var peg = Group(p, "Pegboard");
            Box(peg, "Board", V(W - T - 0.6f, 34, 12), V(W - T, 52, 28), "Pegboard");
            Box(peg, "Wrench", V(W - T - 1.2f, 38, 14), V(W - T - 0.6f, 50, 16), "Metal");
            Box(peg, "Hammer_Handle", V(W - T - 1.2f, 36, 19), V(W - T - 0.6f, 48, 20.5f), "WoodLight");
            Box(peg, "Hammer_Head", V(W - T - 1.6f, 47, 17), V(W - T - 0.6f, 50, 23), "MetalDark");
            Box(peg, "Pliers", V(W - T - 1.2f, 38, 23.5f), V(W - T - 0.6f, 50, 26.5f), "Plastic_Red");

            Box(p, "Rug", V(78, 0, 12), V(122, 0.12f, 50), "Rug");
            Sphere(p, "PaperBall_1", V(95, 1.6f, 30), 1.6f, "Paper");
            Sphere(p, "PaperBall_2", V(60, 1.3f, 60), 1.3f, "Paper");
            Sphere(p, "PaperBall_3", V(110, 1.4f, 70), 1.4f, "Paper");
            Cyl(p, "Nut", V(48, 0, 22), 1.2f, 0.8f, "Metal");
            Box(p, "Screw", V(55, 0, 30), V(59, 0.4f, 30.6f), "Metal");
            Box(p, "Cable_Desk", V(118, 0, 96), V(W - T, 0.4f, 97), "Cable");
        }

        // ================= Капсула и пульт =================

        static void Capsule(Transform p)
        {
            var c = V(32, 0, 40);
            const float r = 14f, top = 52f;
            Cyl(p, "Base", c, r + 1, 1.5f, "Machine");
            Cyl(p, "Floor_Glow", c + Vector3.up * 1.5f, r - 2, 0.05f, "Glow");
            Box(p, "Footprint_L", c + V(-3, 1.5f, -2), c + V(-0.5f, 1.6f, 5), "MetalDark");
            Box(p, "Footprint_R", c + V(0.5f, 1.5f, -2), c + V(3, 1.6f, 5), "MetalDark");

            // Стеклянная труба из сегментов, дверной проём на юго-восток
            var glass = Group(p, "Glass");
            const int segments = 32;
            float segW = 2f * Mathf.PI * r / segments * 1.06f;
            float doorAngle = -45f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 360f / segments;
                if (Mathf.Abs(Mathf.DeltaAngle(a, doorAngle)) < 24f) continue;
                var seg = Box(glass, "Segment_" + i, V(-segW * 0.5f, 0, -0.3f), V(segW * 0.5f, top - 1.5f, 0.3f), "CapsuleGlass");
                float rad = a * Mathf.Deg2Rad;
                seg.transform.position = c + new Vector3(Mathf.Cos(rad) * r, 1.5f + (top - 1.5f) * 0.5f, Mathf.Sin(rad) * r);
                seg.transform.rotation = Quaternion.Euler(0, 90f - a, 0);
            }
            foreach (var side in new[] { doorAngle - 25f, doorAngle + 25f })
            {
                float rad = side * Mathf.Deg2Rad;
                Cyl(p, "DoorPost", c + new Vector3(Mathf.Cos(rad) * r, 1.5f, Mathf.Sin(rad) * r), 0.8f, top - 1.5f, "Machine");
            }
            Cyl(p, "Cap", c + Vector3.up * top, r + 1, 4f, "Machine");
            Cyl(p, "Cap_Emitter", c + Vector3.up * (top - 0.6f), r - 4, 0.6f, "Glow");
            Cyl(p, "Cap_Dome", c + Vector3.up * (top + 4), r - 3, 2f, "MachineDark");

            // Кабели к пульту
            Box(p, "Cable_1", V(46, 0, 38), V(60, 0.4f, 39.2f), "Cable");
            Box(p, "Cable_2", V(46.5f, 0, 41), V(60, 0.4f, 42.2f), "Cable");
            Label(p, "СТАРТ: капсула уменьшения\n(учёный включил её и уменьшился)", c + Vector3.up * 8);
        }

        // Пульт капсулы высотой 56 м: панель с переключателем режима и кнопкой на уступе 34 м
        static void ControlConsole(Transform p)
        {
            Box(p, "Body", V(60, 0, 30), V(76, 56, 44), "Machine");
            Box(p, "Top_Grille", V(61, 56, 31), V(75, 56.3f, 43), "MetalDark");
            Box(p, "Vent_Slots", V(59.9f, 6, 33), V(60, 26, 41), "MetalDark");
            Box(p, "Ledge_Indicators_46", V(61, 45, 25), V(75, 46, 30), "MachineDark");
            for (int i = 0; i < 5; i++)
                Cyl(p, "Indicator_" + i, V(63 + i * 2.8f, 46, 29), 0.6f, 0.3f, i % 2 == 0 ? "Glow" : "Button");
            Box(p, "Ledge_Controls_34", V(61, 33, 18), V(75, 34, 30), "MachineDark");
            Box(p, "Screen", V(62, 36, 29.9f), V(74, 43, 30), "Screen");
            var lever = Group(p, "ModeSwitch");
            Box(lever, "Base", V(63, 34, 25.5f), V(66, 35, 28.5f), "MetalDark");
            Box(lever, "Lever", V(64, 35, 26.5f), V(65, 39, 27.5f), "Interact");
            Sphere(lever, "Knob", V(64.5f, 39.5f, 27), 0.9f, "Interact");
            Box(p, "Mode_Label_Shrink", V(62.5f, 34, 23.5f), V(64, 34.05f, 25), "Plastic_Blue");
            Box(p, "Mode_Label_Grow", V(65, 34, 23.5f), V(66.5f, 34.05f, 25), "Plastic_Red");
            Cyl(p, "BigButton_Base", V(72, 34, 21), 2f, 0.4f, "MetalDark");
            Cyl(p, "BigButton", V(72, 34.4f, 21), 1.5f, 0.6f, "Button");
            Label(p, "ПУЛЬТ КАПСУЛЫ: переключить режим на «увеличение» и нажать кнопку", V(68, 41, 22));
        }

        // ================= Маршрут 1: пол =================

        static void FloorRoute(Transform p)
        {
            BookStack(p, "R1_Books_1.5", V(33, 0, 10), 2, 6f, 8f);
            BookStack(p, "R2_Books_3", V(42, 0, 10), 4, 6f, 8f);
            var shoebox = Group(p, "R3_Shoebox_4.5");
            Box(shoebox, "Body", V(48, 0, 5.7f), V(57, 3.9f, 14.3f), "Shoebox");
            Box(shoebox, "Lid", V(47.8f, 3.9f, 5.5f), V(57.2f, 4.5f, 14.5f), "ShoeboxLid");
            Carton(p, "R4_Box_6", V(60, 0, 6), V(68, 6, 14));
            Carton(p, "R5_Box_7.5", V(71, 0, 6), V(79, 7.5f, 14));
            PaintCan(p, "R6_PaintCan_9", V(85, 0, 9), 3.2f, 9f, "Paint_Blue");
            PaintCan(p, "Decor_PaintCan", V(96, 0, 8), 3.2f, 6f, "Paint_Green");

            var toolbox = Group(p, "R7_Toolbox_10.5");
            Box(toolbox, "Body", V(68, 0, 15), V(78, 9.5f, 25), "Toolbox");
            Box(toolbox, "Lid", V(67.8f, 9.5f, 14.8f), V(78.2f, 10.5f, 25.2f), "ToolboxLid");
            Box(toolbox, "Latch", V(72, 7.5f, 14.7f), V(74, 9.5f, 15), "Metal");

            // Кресло лицом на запад: сиденье 12, подлокотники 13.5, спинка на востоке
            var chair = Group(p, "R8_OfficeChair");
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var leg = Box(chair, "BaseLeg_" + i, V(-0.6f, 1.2f, 0), V(0.6f, 2.2f, 6.5f), "ChairPlastic");
                leg.transform.position = V(86 + Mathf.Sin(a) * 3.25f, 1.7f, 21 + Mathf.Cos(a) * 3.25f);
                leg.transform.rotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
                Sphere(chair, "Caster_" + i, V(86 + Mathf.Sin(a) * 6.3f, 0.8f, 21 + Mathf.Cos(a) * 6.3f), 0.8f, "ChairPlastic");
            }
            Cyl(chair, "GasLift", V(86, 2, 21), 0.9f, 8.5f, "Metal");
            Box(chair, "Seat", V(80, 10.5f, 15), V(92, 12, 27), "ChairFabric");
            Box(chair, "Backrest", V(92, 14, 16), V(94, 31, 26), "ChairFabric");
            Box(chair, "Backrest_Post", V(92, 11, 20), V(93.5f, 14, 22), "ChairPlastic");
            Box(chair, "Armrest_N", V(81, 12, 24), V(91, 13.5f, 27), "ChairPlastic");
            Box(chair, "Armrest_S", V(81, 12, 15), V(91, 13.5f, 18), "ChairPlastic");

            Cyl(p, "R9_Bin", V(85, 0, 33), 5f, 14.4f, "BinMetal");
            Cyl(p, "R9_Bin_Lid", V(85, 14.4f, 33), 5.2f, 0.6f, "MetalDark");
            Label(p, "1. Книги → коробки → банка → ящик → кресло → ведро", V(60, 16, 12));
        }

        // ================= Маршрут 2: комод с ящиками-лесенкой =================

        static void ChestOfDrawers(Transform p)
        {
            Box(p, "Body", V(108, 0, 32), V(122, 15, 46), "Wood");
            Box(p, "Top", V(107.5f, 24.5f, 31.5f), V(122.5f, 25.5f, 46.5f), "WoodDark");
            Box(p, "Back", V(121, 15, 32), V(122, 24.5f, 46), "Wood");
            float[] pulls = { 18, 15, 12, 9, 6, 3 };
            for (int i = 0; i < pulls.Length; i++)
            {
                float yTop = 16.5f + 1.5f * i, x0 = 108 - pulls[i];
                var drawer = Group(p, $"Drawer_{i}_{yTop}");
                Box(drawer, "Tray", V(x0, yTop - 1.5f, 32.5f), V(121, yTop - 0.2f, 45.5f), i % 2 == 0 ? "WoodLight" : "Wood");
                Box(drawer, "Papers", V(x0 + 0.6f, yTop - 0.2f, 33.5f), V(121, yTop, 44.5f), "Paper");
                Box(drawer, "Handle", V(x0 - 0.5f, yTop - 1.1f, 37.5f), V(x0, yTop - 0.5f, 40.5f), "Brass");
            }
            Label(p, "2. Комод: ящики выдвинуты лесенкой → стол", V(105, 30, 39));
        }

        // ================= Маршрут 3: стол и полки =================

        static void Desk(Transform p)
        {
            Table(p, "Desk", 124, W - T, 10, 80, 25f);
            Box(p, "Desk_DrawerUnit", V(136, 0, 66), V(W - T, 23.5f, 80), "WoodDark");
            Box(p, "PC_Tower", V(126, 0, 50), V(134, 11, 60), "PC");
            Box(p, "Monitor_Stand", V(126.5f, 25, 21), V(128.5f, 30, 25), "PC");
            Box(p, "Monitor", V(126, 30, 14), V(128.5f, 40, 32), "PC");
            Box(p, "Monitor_Screen", V(125.9f, 30.6f, 14.6f), V(126, 39.4f, 31.4f), "Screen");
            Box(p, "Keyboard", V(129.5f, 25, 16), V(135, 25.4f, 30), "PC");
            Cyl(p, "Mug", V(132, 25, 34), 1.8f, 4f, "Mug");
            Box(p, "Pickup_Notebook", V(130, 25, 74), V(135, 25.6f, 78), "Pickup", trigger: true);

            BookStack(p, "D1_Books_26.5", V(135.5f, 25, 65), 2, 6f, 6f);
            BookStack(p, "D2_Books_28", V(135.5f, 25, 57), 4, 6f, 6f);
            BookStack(p, "D3_Books_29.5", V(135.5f, 25, 49), 6, 6f, 6f);
            BookStack(p, "D4_Books_31", V(135.5f, 25, 41), 8, 6f, 6f);

            // Нижняя полка 32.5 (z 10–80) и верхняя 41.5 (z 82–122) на восточной стене
            Box(p, "Shelf_Low_32.5", V(141, 31.5f, 10), V(W - T, 32.5f, 80), "Wood");
            Box(p, "Shelf_High_41.5", V(141, 40.5f, 82), V(W - T, 41.5f, D - T - 1), "Wood");
            foreach (var bz in new[] { 12f, 45f, 77f })
                Box(p, "Bracket_Low", V(W - T - 1.2f, 27, bz), V(W - T, 31.5f, bz + 1.2f), "MetalDark");
            foreach (var bz in new[] { 84f, 120f })
                Box(p, "Bracket_High", V(W - T - 1.2f, 36, bz), V(W - T, 40.5f, bz + 1.2f), "MetalDark");

            BookStack(p, "E1_Books_34", V(144.5f, 32.5f, 45), 2, 6f, 6f);
            BookStack(p, "E1_Books_35.5", V(144.5f, 32.5f, 53), 4, 6f, 6f);
            BookStack(p, "E1_Books_37", V(144.5f, 32.5f, 61), 6, 6f, 6f);
            BookStack(p, "E1_Books_38.5", V(144.5f, 32.5f, 69), 8, 6f, 6f);
            Carton(p, "E1_Box_40", V(141.5f, 32.5f, 74), V(147.5f, 40, 80));
            BookStack(p, "E2_Books_43", V(144.5f, 41.5f, 91), 2, 6f, 6f);
            BookStack(p, "E2_Books_44.5", V(144.5f, 41.5f, 99), 4, 6f, 6f);
            BookStack(p, "E2_Books_46", V(144.5f, 41.5f, 107), 6, 6f, 6f);
            BookStack(p, "E2_Books_47.5", V(144.5f, 41.5f, 115), 8, 6f, 6f);
            Label(p, "3. Стопки на столе → нижняя полка → верхняя полка", V(138, 36, 55));
        }

        // ================= Маршрут 4: книжный шкаф =================

        static void Bookcase(Transform p)
        {
            const float x0 = 88, x1 = 141, z0 = 108, z1 = D - T;
            float[] boards = { 0, 12, 24, 36, 48, 60 };
            foreach (var by in boards)
            {
                if (Mathf.Approximately(by, 48))
                {
                    Box(p, "Board_48_West", V(x0, 48, z0), V(118, 49, z1), "WoodDark");
                    Box(p, "Board_48_East", V(124, 48, z0), V(x1, 49, z1), "WoodDark");
                }
                else Box(p, "Board_" + by, V(x0, by, z0), V(x1, by + 1, z1), "WoodDark");
            }
            Box(p, "Top", V(x0 - 0.5f, 61, z0 - 0.5f), V(x1 + 0.5f, 62, z1), "WoodDark");
            // Боковины открыты на 4-м ярусе (49–60), чтобы пройти шкаф насквозь
            foreach (var (sx, name) in new[] { (x0, "Side_W"), (x1 - 1, "Side_E") })
            {
                Box(p, name + "_Low", V(sx, 0, z0), V(sx + 1, 48, z1), "WoodDark");
                Box(p, name + "_High", V(sx, 61, z0), V(sx + 1, 62, z1), "WoodDark");
            }
            Box(p, "Divider_Low", V(114, 1, z0), V(115, 48, z1), "WoodDark");

            // Декор: ряды книг на нижних ярусах
            for (int i = 0; i < 4; i++)
            {
                float y = boards[i] + 1;
                BookRowAlongX(p, $"Row_{i}_W", 89, 113.5f, y, z0 + 2, z1, 10f);
                BookRowAlongX(p, $"Row_{i}_E", 115.5f, 140, y, z0 + 2, z1, 10f);
            }

            // 4-й ярус: барьер, провал, лаз, стопки вверх
            BookStack(p, "C_Hurdle_50.5", V(130, 49, 115.75f), 2, 4f, 15.5f);
            BookRowAlongX(p, "C_StandingBooks", 132.5f, 139.5f, 49, 117, z1, 9f);
            Box(p, "C_Tunnel_Wall_S", V(105, 49, z0), V(111, 49 + CrawlGap, z0 + 1), "Book_Brown");
            Box(p, "C_Tunnel_Wall_N", V(105, 49, z1 - 1), V(111, 49 + CrawlGap, z1), "Book_Brown");
            Box(p, "C_Tunnel_FallenBook", V(104.5f, 49 + CrawlGap, z0), V(111.5f, 53, z1), "Book_Red");
            Box(p, "C_Tunnel_FallenBook_Pages", V(104.7f, 49 + CrawlGap + 0.3f, z0 + 0.2f), V(111.3f, 52.7f, z1 - 0.2f), "Paper");
            BookStack(p, "C_Stack_50.5", V(100, 49, 113.5f), 2, 4f, 7f);
            BookStack(p, "C_Stack_52", V(95.5f, 49, 115.5f), 4, 4f, 7f);
            BookStack(p, "C_Stack_53.5", V(91.5f, 49, 115), 6, 4f, 6f);
            BookStack(p, "C_Stack_55", V(91.5f, 49, 121), 8, 4f, 5f);
            Label(p, "4. Книжный шкаф: барьер, провал 6 м (с разбега), лаз под книгой (присед)", V(115, 57, 113));
        }

        // ================= Маршрут 5: дверь, платяной шкаф, люк =================

        static void DoorAndWardrobe(Transform p)
        {
            // Дверь с наличником на северной стене; верх наличника — узкий карниз 2 м
            var door = Group(p, "Door");
            Box(door, "Leaf", V(58, 0.4f, D - T - 1), V(86, 52.5f, D - T), "DoorWood");
            Box(door, "Panel_1", V(61, 30, D - T - 1.3f), V(83, 48, D - T - 1), "DoorWoodDark");
            Box(door, "Panel_2", V(61, 5, D - T - 1.3f), V(83, 25, D - T - 1), "DoorWoodDark");
            Box(door, "Jamb_W", V(56, 0, D - T - 2), V(58, 52.5f, D - T), "Trim");
            Box(door, "Jamb_E", V(86, 0, D - T - 2), V(88, 52.5f, D - T), "Trim");
            Box(door, "Lintel_54.5", V(56, 52.5f, D - T - 2), V(88, 54.5f, D - T), "Trim");
            Sphere(door, "Knob", V(82, 26, D - T - 1.8f), 1.2f, "Brass");

            // Платяной шкаф в северо-западном углу (верх 56)
            var wardrobe = Group(p, "Wardrobe");
            Box(wardrobe, "Body", V(T, 0, 98), V(52, 55, D - T), "Wood");
            Box(wardrobe, "Top", V(T, 55, 97.5f), V(52.5f, 56, D - T), "WoodDark");
            Box(wardrobe, "Door_L", V(3, 3, 97.6f), V(26.5f, 53, 98), "WoodLight");
            Box(wardrobe, "Door_R", V(27, 3, 97.6f), V(50.5f, 53, 98), "WoodLight");
            Box(wardrobe, "Handle_L", V(24.5f, 24, 96.9f), V(25.5f, 32, 97.6f), "Brass");
            Box(wardrobe, "Handle_R", V(28, 24, 96.9f), V(29, 32, 97.6f), "Brass");

            // Хлам на шкафу: чемодан, шляпная коробка, коробки лесенкой под люк
            var suitcase = Group(p, "W_Suitcase_57.5");
            Box(suitcase, "Body", V(36, 56, 106), V(46, 57.5f, 118), "Suitcase");
            Box(suitcase, "Handle", V(46, 56.4f, 110), V(46.8f, 57.1f, 114), "MetalDark");
            Box(suitcase, "Strap", V(39, 57.5f, 106), V(40, 57.55f, 118), "Ink");
            Cyl(p, "W_HatBox_59", V(30, 56, 112), 4f, 3f, "HatBox");
            Cyl(p, "W_HatBox_Ribbon", V(30, 57.2f, 112), 4.05f, 0.4f, "Plastic_Red");
            for (int i = 0; i < 7; i++)
            {
                float top = 60.5f + 1.5f * i, x0 = 22 - 3 * i;
                float zMin = i == 6 ? 108 : 106, zMax = i == 6 ? 116 : 116;
                Carton(p, $"W_Box_{top}", V(x0, 56, zMin), V(x0 + 3, top, zMax));
            }
            Box(p, "HatchCover", V(32, 56, 100), V(46, 56.5f, 105), "Ceiling");
            Label(p, "5. Наличник двери → шкаф → коробки лесенкой → люк на чердак", V(30, 64, 110));
        }

        static void Lighting(Transform p)
        {
            var main = PointLight(p, "DiffuserLight_Main", V(71, H - 5, 62), new Color(1f, 0.97f, 0.92f), 220f, 4000f);
            main.shadows = LightShadows.Hard;
            PointLight(p, "DiffuserLight_South", V(71, H - 5, 30), new Color(1f, 0.97f, 0.92f), 120f, 1500f);
            PointLight(p, "Fill_East", V(125, H - 8, 70), new Color(1f, 0.95f, 0.88f), 120f, 1600f);
            PointLight(p, "WindowLight", V(55, 37, 12), new Color(0.75f, 0.85f, 1f), 90f, 700f);
            PointLight(p, "CapsuleGlow", V(32, 30, 40), new Color(0.3f, 0.9f, 1f), 50f, 300f);
            PointLight(p, "Attic_Hatch", V(15, AtticFloor + 4, 115), new Color(1f, 0.85f, 0.6f), 60f, 150f);
            PointLight(p, "Attic_Lamp", V(71, AtticFloor + 4, 95), new Color(1f, 0.85f, 0.6f), 60f, 150f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.52f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.38f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.18f);
        }

        static Light PointLight(Transform p, string name, Vector3 pos, Color color, float range, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(p, false);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        // ================= Проверка маршрута ботом =================

        [MenuItem("Tools/HouseCreators/Workshop: Validate Route")]
        public static bool ValidateRoute()
        {
            var player = Object.FindFirstObjectByType<TinyPlayerController>();
            if (player == null) { Debug.LogError("[Workshop] Player not found"); return false; }
            var pcc = player.GetComponent<CharacterController>();

            var bot = new GameObject("RouteBot") { layer = 2, hideFlags = HideFlags.HideAndDontSave };
            bot.transform.position = Route[0].pos + Vector3.up * 0.05f;
            var cc = bot.AddComponent<CharacterController>();
            cc.height = pcc.height;
            cc.radius = pcc.radius;
            cc.center = pcc.center;
            cc.stepOffset = pcc.stepOffset;
            cc.slopeLimit = pcc.slopeLimit;
            cc.skinWidth = pcc.skinWidth;
            bool playerWasEnabled = pcc.enabled;
            pcc.enabled = false;
            Physics.SyncTransforms();

            const float dt = 1f / 60f;
            float g = player.gravity, jumpV = Mathf.Sqrt(2f * player.jumpHeight * -g);
            float air = player.airAcceleration;
            var vel = Vector3.zero;
            var log = new StringBuilder();
            float total = 0f;
            int jumps = 0, crawls = 0;
            bool allOk = true, crouched = false;

            for (int f = 0; f < 30; f++) { vel.y += g * dt; cc.Move(vel * dt); if (cc.isGrounded) vel.y = -2f; }

            for (int i = 1; i < Route.Length; i++)
            {
                var node = Route[i];
                float startY = bot.transform.position.y, t = 0f;
                string result = null;
                bool jumped = false;

                // Присед как у TinyPlayerController: встать можно только если над головой свободно
                if (node.crouch && !crouched) { SetHeight(cc, player.crouchHeight); crouched = true; crawls++; }
                else if (!node.crouch && crouched && CanStand(bot.transform.position, cc.radius, player.standHeight))
                {
                    SetHeight(cc, player.standHeight);
                    crouched = false;
                }
                float speed = crouched ? player.crouchSpeed : node.run ? player.runSpeed : player.walkSpeed;

                while (result == null && t < 30f)
                {
                    t += dt;
                    var pos = bot.transform.position;
                    if (crouched && !node.crouch && cc.isGrounded && CanStand(pos, cc.radius, player.standHeight))
                    {
                        SetHeight(cc, player.standHeight);
                        crouched = false;
                        speed = node.run ? player.runSpeed : player.walkSpeed;
                    }
                    var to = new Vector3(node.pos.x - pos.x, 0f, node.pos.z - pos.z);
                    float dist = to.magnitude;
                    var wish = dist > 0.2f ? to / dist * Mathf.Min(speed, dist / dt) : Vector3.zero;
                    if (cc.isGrounded)
                    {
                        // Как игрок: прыгаем у края или перед препятствием (в приседе прыгать нельзя)
                        if (node.jump && !jumped && !crouched && dist > 0.3f && ShouldJump(pos, to / dist))
                        {
                            vel.y = jumpV;
                            jumped = true;
                            jumps++;
                        }
                        vel.x = wish.x;
                        vel.z = wish.z;
                        if (vel.y < 0f) vel.y = -2f;
                    }
                    else
                    {
                        var h = Vector3.MoveTowards(new Vector3(vel.x, 0f, vel.z), wish, air * dt);
                        vel.x = h.x;
                        vel.z = h.z;
                    }
                    vel.y += g * dt;
                    cc.Move(vel * dt);
                    pos = bot.transform.position;
                    dist = new Vector2(node.pos.x - pos.x, node.pos.z - pos.z).magnitude;

                    if (cc.isGrounded && dist < 0.5f)
                        result = Mathf.Abs(pos.y - node.pos.y) < 0.6f ? "OK" : $"не та высота {pos.y:0.0} вместо {node.pos.y:0.0}";
                    else if (pos.y < Mathf.Min(startY, node.pos.y) - 3f)
                        result = $"упал до {pos.y:0.0} у ({pos.x:0.0}, {pos.z:0.0})";
                }
                result ??= "не дошёл за 30 с (застрял у " + bot.transform.position.ToString("F1") + ")";
                total += t;
                bool ok = result == "OK";
                allOk &= ok;
                string flags = (node.jump ? " [прыжок]" : "") + (node.run ? " [бег]" : "") + (node.crouch ? " [ползком]" : "");
                log.AppendLine($"{(ok ? "✔" : "✘")} {i,2}. {node.name}{flags}: {result} ({t:0.0} с)");
                if (!ok) break;
            }

            Object.DestroyImmediate(bot);
            pcc.enabled = playerWasEnabled;
            string summary = allOk
                ? $"[Workshop] Маршрут ПРОХОДИМ: {Route.Length - 1} участков, {jumps} прыжков, {crawls} лаза, ~{total / 60f:0.0} мин идеального прохождения."
                : "[Workshop] Маршрут НЕ ПРОХОДИМ.";
            Debug.Log(summary + "\n" + log);
            return allOk;
        }

        static void SetHeight(CharacterController cc, float height)
        {
            cc.height = height;
            cc.center = new Vector3(0f, height * 0.5f, 0f);
        }

        static bool CanStand(Vector3 feet, float radius, float standHeight)
        {
            float r = radius * 0.9f;
            return !Physics.CheckCapsule(feet + Vector3.up * (r + 0.05f), feet + Vector3.up * (standHeight - r), r, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        // Прыгать, если в 2 м впереди опора выше шага или прямо перед ногами обрыв.
        static bool ShouldJump(Vector3 feet, Vector3 dir)
        {
            const QueryTriggerInteraction ignore = QueryTriggerInteraction.Ignore;
            var far = feet + dir * 2f;
            if (Physics.Raycast(far + Vector3.up * 3f, Vector3.down, out var hitFar, 3.6f, ~0, ignore) && hitFar.point.y > feet.y + 0.45f)
                return true;
            var near = feet + dir * 0.6f;
            return !Physics.Raycast(near + Vector3.up * 0.3f, Vector3.down, 1.0f, ~0, ignore);
        }

        // ================= Пропсы =================

        static void BookStack(Transform p, string name, Vector3 baseCenter, int count, float sx, float sz)
        {
            var stack = Group(p, name);
            string[] colors = { "Book_Red", "Book_Blue", "Book_Green", "Book_Beige", "Book_Brown", "Book_Orange" };
            for (int i = 0; i < count; i++)
            {
                float w = sx * Rand(0.92f, 1f), d = sz * Rand(0.94f, 1f);
                var c = baseCenter + new Vector3(Rand(-0.2f, 0.2f), Book * (i + 0.5f), Rand(-0.2f, 0.2f));
                var b = Box(stack, "Book_" + i, c - new Vector3(w, Book, d) * 0.5f, c + new Vector3(w, Book, d) * 0.5f, colors[rnd.Next(colors.Length)]);
                b.transform.rotation = Quaternion.Euler(0, Rand(-4f, 4f), 0);
                var pages = Box(b.transform, "Pages", Vector3.zero, Vector3.one, "Paper");
                pages.transform.localPosition = new Vector3(0.02f, 0f, 0f);
                pages.transform.localScale = new Vector3(0.97f, 0.8f, 1.002f);
            }
        }

        // Ряд стоящих книг вдоль X (корешками к комнате)
        static void BookRowAlongX(Transform p, string name, float x0, float x1, float y, float z0, float z1, float maxH)
        {
            var row = Group(p, name);
            string[] colors = { "Book_Red", "Book_Blue", "Book_Green", "Book_Beige", "Book_Brown", "Book_Orange" };
            float x = x0;
            while (x < x1 - 1.2f)
            {
                float th = Rand(0.8f, 1.6f), h = maxH * Rand(0.7f, 1f);
                Box(row, "Book", V(x, y, z0 + (z1 - z0) * Rand(0.05f, 0.25f)), V(x + th, y + h, z1), colors[rnd.Next(colors.Length)]);
                x += th + 0.05f;
                if (rnd.NextDouble() < 0.06) x += Rand(2f, 4f);
            }
        }

        static void Carton(Transform p, string name, Vector3 min, Vector3 max)
        {
            var box = Box(p, name, min, max, "Cardboard");
            var tape = Box(box.transform, "Tape", Vector3.zero, Vector3.one, "Tape");
            tape.transform.localPosition = new Vector3(0, 0.5f, 0);
            tape.transform.localScale = new Vector3(0.18f, 0.01f, 1.004f);
            var label = Box(box.transform, "Label", Vector3.zero, Vector3.one, "Paper");
            label.transform.localPosition = new Vector3(0.2f, 0.1f, -0.501f);
            label.transform.localScale = new Vector3(0.3f, 0.25f, 0.005f);
        }

        static void PaintCan(Transform p, string name, Vector3 bottom, float r, float h, string labelMat)
        {
            var can = Group(p, name);
            Cyl(can, "Body", bottom, r, h - 0.4f, "MetalLight");
            Cyl(can, "Lid", bottom + Vector3.up * (h - 0.4f), r, 0.4f, labelMat);
            Cyl(can, "Label", bottom + Vector3.up * h * 0.25f, r + 0.05f, h * 0.45f, labelMat);
        }

        static void Table(Transform p, string name, float x0, float x1, float z0, float z1, float topY)
        {
            var t = Group(p, name);
            Box(t, "Top", V(x0, topY - 1.5f, z0), V(x1, topY, z1), "DeskWood");
            Box(t, "Edge", V(x0 - 0.3f, topY - 1.8f, z0), V(x0, topY, z1), "WoodDark");
            const float leg = 2f;
            foreach (var (lx, lz) in new[] { (x0, z0), (x1 - leg, z0), (x0, z1 - leg), (x1 - leg, z1 - leg) })
                Box(t, "Leg", V(lx, 0, lz), V(lx + leg, topY - 1.5f, lz + leg), "MetalDark");
        }

        // Плита с прямоугольными вырезами: режем по всем границам вырезов на ячейки.
        static void SlabWithHoles(Transform p, string name, Rect area, float y0, float y1, string mat, params Rect[] holes)
        {
            var xs = new SortedSet<float> { area.xMin, area.xMax };
            var zs = new SortedSet<float> { area.yMin, area.yMax };
            foreach (var h in holes)
            {
                foreach (var x in new[] { h.xMin, h.xMax }) if (x > area.xMin && x < area.xMax) xs.Add(x);
                foreach (var z in new[] { h.yMin, h.yMax }) if (z > area.yMin && z < area.yMax) zs.Add(z);
            }
            var xa = xs.ToArray();
            var za = zs.ToArray();
            for (int i = 0; i < xa.Length - 1; i++)
                for (int j = 0; j < za.Length - 1; j++)
                {
                    var center = new Vector2((xa[i] + xa[i + 1]) * 0.5f, (za[j] + za[j + 1]) * 0.5f);
                    if (holes.Any(h => h.Contains(center))) continue;
                    Box(p, $"{name}_{i}_{j}", V(xa[i], y0, za[j]), V(xa[i + 1], y1, za[j + 1]), mat);
                }
        }

        // ================= Примитивы (все с коллайдерами) =================

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static float Rand(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        static GameObject Box(Transform p, string name, Vector3 min, Vector3 max, string mat, bool trigger = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.position = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            go.GetComponent<Renderer>().sharedMaterial = mats[mat];
            go.GetComponent<Collider>().isTrigger = trigger;
            return go;
        }

        static GameObject Cyl(Transform p, string name, Vector3 bottom, float r, float h, string mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.position = bottom + Vector3.up * h * 0.5f;
            go.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mats[mat];
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
            return go;
        }

        static GameObject Sphere(Transform p, string name, Vector3 center, float r, string mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * r * 2f;
            go.GetComponent<Renderer>().sharedMaterial = mats[mat];
            return go;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static void Label(Transform p, string text, Vector3 pos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(p, false);
            go.transform.position = pos;
            var l = go.AddComponent<BlockoutLabel>();
            l.text = text;
            l.color = new Color(1f, 0.85f, 0.3f);
        }

        static void BuildPlayer(Vector3 position, float yaw)
        {
            var player = new GameObject("Player") { tag = "Player", layer = 2 };
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
            camera.fieldOfView = 70f;
            cam.AddComponent<AudioListener>();
            if (cam.GetComponent<UniversalAdditionalCameraData>() == null) cam.AddComponent<UniversalAdditionalCameraData>();
            player.AddComponent<TinyPlayerController>().cameraPivot = pivot;
        }

        // ================= Материалы =================

        static void CreateMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/WorkshopRoom")) AssetDatabase.CreateFolder("Assets", "WorkshopRoom");
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/WorkshopRoom", "Materials");
            mats.Clear();
            var floorTex = AssetDatabase.LoadAssetAtPath<Material>("Assets/nappin/HouseInteriorPack/Materials/(Mat)Floor.mat")?.GetTexture("_BaseMap");

            Mat("FloorWood", floorTex ? new Color(0.85f, 0.72f, 0.58f) : new Color(0.55f, 0.38f, 0.24f), 0f, 0.35f, null, floorTex, new Vector2(10, 8));
            Mat("Wall", new Color(0.66f, 0.73f, 0.64f));
            Mat("Ceiling", new Color(0.95f, 0.95f, 0.93f));
            Mat("Trim", new Color(0.93f, 0.92f, 0.88f), 0f, 0.4f);
            Mat("AtticWood", new Color(0.5f, 0.38f, 0.26f), 0f, 0.2f);
            Mat("AtticBoards", floorTex ? new Color(0.6f, 0.5f, 0.42f) : new Color(0.42f, 0.33f, 0.24f), 0f, 0.1f, null, floorTex, new Vector2(10, 8));
            Mat("Insulation", new Color(0.95f, 0.6f, 0.65f), 0f, 0.05f);
            Mat("Copper", new Color(0.8f, 0.45f, 0.25f), 0.9f, 0.6f);
            Mat("Duct", new Color(0.72f, 0.74f, 0.76f), 0.8f, 0.5f);
            Mat("Dust", new Color(0.55f, 0.55f, 0.55f), 0f, 0f);
            Mat("WindowSky", new Color(0.6f, 0.8f, 1f), 0f, 0.9f, new Color(0.55f, 0.75f, 1f) * 1.4f);
            Mat("Curtain", new Color(0.75f, 0.3f, 0.25f), 0f, 0.1f);
            Mat("Radiator", new Color(0.9f, 0.9f, 0.88f), 0.3f, 0.5f);
            Mat("DoorWood", new Color(0.45f, 0.3f, 0.2f), 0f, 0.35f);
            Mat("DoorWoodDark", new Color(0.38f, 0.25f, 0.16f), 0f, 0.35f);
            Mat("Brass", new Color(0.85f, 0.68f, 0.3f), 0.9f, 0.7f);
            Mat("WoodDark", new Color(0.33f, 0.22f, 0.14f), 0f, 0.35f);
            Mat("Wood", new Color(0.55f, 0.38f, 0.24f), 0f, 0.35f);
            Mat("WoodLight", new Color(0.72f, 0.56f, 0.38f), 0f, 0.35f);
            Mat("DeskWood", new Color(0.62f, 0.45f, 0.3f), 0f, 0.45f);
            Mat("ClockFace", new Color(0.97f, 0.96f, 0.9f), 0f, 0.6f);
            Mat("Ink", new Color(0.1f, 0.1f, 0.12f));
            Mat("Blueprint", new Color(0.12f, 0.3f, 0.6f));
            Mat("BlueprintLine", new Color(0.85f, 0.92f, 1f), 0f, 0.3f, new Color(0.4f, 0.5f, 0.7f) * 0.5f);
            Mat("Pegboard", new Color(0.78f, 0.66f, 0.48f));
            Mat("Metal", new Color(0.7f, 0.72f, 0.75f), 0.9f, 0.6f);
            Mat("MetalLight", new Color(0.82f, 0.83f, 0.85f), 0.85f, 0.55f);
            Mat("MetalDark", new Color(0.18f, 0.19f, 0.21f), 0.7f, 0.5f);
            Mat("BinMetal", new Color(0.55f, 0.58f, 0.6f), 0.8f, 0.45f);
            Mat("Rug", new Color(0.5f, 0.2f, 0.18f), 0f, 0.05f);
            Mat("Paper", new Color(0.96f, 0.95f, 0.9f), 0f, 0.2f);
            Mat("Cable", new Color(0.08f, 0.08f, 0.08f), 0f, 0.4f);
            Mat("Cardboard", new Color(0.72f, 0.55f, 0.35f), 0f, 0.15f);
            Mat("Tape", new Color(0.86f, 0.76f, 0.55f), 0f, 0.7f);
            Mat("Shoebox", new Color(0.9f, 0.45f, 0.15f), 0f, 0.25f);
            Mat("ShoeboxLid", new Color(0.95f, 0.95f, 0.95f), 0f, 0.25f);
            Mat("Suitcase", new Color(0.45f, 0.18f, 0.12f), 0.1f, 0.5f);
            Mat("HatBox", new Color(0.92f, 0.85f, 0.7f), 0f, 0.3f);
            Mat("Toolbox", new Color(0.78f, 0.12f, 0.1f), 0.5f, 0.55f);
            Mat("ToolboxLid", new Color(0.6f, 0.08f, 0.07f), 0.5f, 0.55f);
            Mat("Paint_Blue", new Color(0.15f, 0.4f, 0.85f), 0f, 0.6f);
            Mat("Paint_Green", new Color(0.2f, 0.65f, 0.3f), 0f, 0.6f);
            Mat("ChairFabric", new Color(0.15f, 0.2f, 0.32f), 0f, 0.15f);
            Mat("ChairPlastic", new Color(0.1f, 0.1f, 0.11f), 0.2f, 0.45f);
            Mat("Mug", new Color(0.95f, 0.85f, 0.3f), 0f, 0.7f);
            Mat("Glass", new Color(0.7f, 0.9f, 0.95f), 0.1f, 0.95f);
            Mat("PC", new Color(0.12f, 0.12f, 0.13f), 0.3f, 0.5f);
            Mat("Screen", new Color(0.1f, 0.2f, 0.35f), 0f, 0.9f, new Color(0.2f, 0.45f, 0.8f) * 1.2f);
            Mat("Plastic_Red", new Color(0.85f, 0.15f, 0.12f), 0f, 0.5f);
            Mat("Plastic_Blue", new Color(0.15f, 0.35f, 0.9f), 0f, 0.5f, new Color(0.1f, 0.3f, 0.9f) * 0.8f);
            Mat("Machine", new Color(0.62f, 0.65f, 0.7f), 0.85f, 0.6f);
            Mat("MachineDark", new Color(0.25f, 0.27f, 0.3f), 0.8f, 0.5f);
            Mat("MachineRingGlow", new Color(0.3f, 0.85f, 1f), 0.5f, 0.7f, new Color(0.2f, 0.8f, 1f) * 1.5f);
            Mat("Glow", new Color(0.2f, 0.8f, 1f), 0f, 0.8f, new Color(0.2f, 0.8f, 1f) * 3f);
            Mat("Button", new Color(0.9f, 0.1f, 0.08f), 0f, 0.7f, new Color(1f, 0.1f, 0.05f) * 1.5f);
            Mat("Interact", new Color(1f, 0.55f, 0.1f), 0.3f, 0.5f, new Color(1f, 0.5f, 0.1f) * 0.6f);
            Mat("Pickup", new Color(1f, 0.85f, 0.15f), 0f, 0.5f, new Color(1f, 0.8f, 0.1f) * 0.8f);
            Mat("LampGlow", Color.white, 0f, 0.5f, new Color(1f, 0.95f, 0.85f) * 2.5f);
            Mat("TubeGlow", Color.white, 0f, 0.5f, new Color(1f, 0.97f, 0.9f) * 3f);
            Mat("Book_Red", new Color(0.65f, 0.15f, 0.12f));
            Mat("Book_Blue", new Color(0.15f, 0.28f, 0.55f));
            Mat("Book_Green", new Color(0.18f, 0.45f, 0.25f));
            Mat("Book_Beige", new Color(0.85f, 0.78f, 0.6f));
            Mat("Book_Brown", new Color(0.42f, 0.27f, 0.16f));
            Mat("Book_Orange", new Color(0.9f, 0.5f, 0.15f));
            Mat("CapsuleGlass", new Color(0.65f, 0.9f, 1f, 0.22f), 0.1f, 0.95f, transparent: true);
            AssetDatabase.SaveAssets();
        }

        static void Mat(string name, Color color, float metallic = 0f, float smoothness = 0.3f, Color? emission = null,
            Texture tex = null, Vector2? tiling = null, bool transparent = false)
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
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m);
            mats[name] = m;
        }
    }
}
