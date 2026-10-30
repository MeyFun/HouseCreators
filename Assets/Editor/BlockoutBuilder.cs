using System.Collections.Generic;
using System.Linq;
using HouseCreators.Blockout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HouseCreators.EditorTools
{
    // Макет (greybox) уровня «Мастерская изобретателя» из примитивов.
    // Геометрия — стандартный серый материал; игровые объекты помечены цветом по легенде (UseColorCoding).
    // Масштаб как в Level_Workshop: игрок 1.8 м, обстановка ×25 (стол 25 м, потолок 70 м).
    //
    // Сюжетная цепочка:
    //  Гл.1 Мастерская — обучение, подъём на стол, кнопка → «микросхема перегорела».
    //  Гл.2 Коридор    — хаб, робот-пылесос патрулирует, двери в спальню и кухню.
    //  Гл.3 Спальня    — пульт на тумбочке → батарейка; радио на полке закрыто (нужна отвёртка).
    //  Гл.4 Кухня      — батарейка в игрушечный лифт → столешница → раковина, конфорки → отвёртка.
    //  Гл.3b Спальня   — отвёрткой открыть радио → микросхема.
    //  Гл.5 Мастерская — шорткат-линейка, установить микросхему, кнопка, 30 с на прыжок в машину.
    public static class BlockoutBuilder
    {
        const string ScenePath = "Assets/Scenes/Blockout_Level.unity";
        const string ColorDir = "Assets/Blockout/Colors";
        const bool UseColorCoding = true;

        const float H = 70f;          // высота комнаты (2.8 м)
        const float T = 1.5f;         // толщина стены
        const float DoorH = 55f;      // высота дверного проёма
        const float CrawlGap = 1.3f;  // щель под дверью/шкафом: присед (1.0) проходит, стоя (1.8) — нет

        enum Kind { Solid, Interact, Pickup, Hazard, Climb, Bounce, Moving, Checkpoint, Goal, Event }
        enum Side { South, North, West, East }

        static readonly Dictionary<Kind, Material> colors = new Dictionary<Kind, Material>();

        static readonly Color Ch1 = new Color(1f, 1f, 1f);
        static readonly Color Ch2 = new Color(0.3f, 0.9f, 1f);
        static readonly Color Ch3 = new Color(1f, 0.4f, 0.9f);
        static readonly Color Ch4 = new Color(1f, 0.85f, 0.2f);
        static readonly Color Ch5 = new Color(0.4f, 1f, 0.4f);

        [MenuItem("Tools/HouseCreators/Build Blockout (greybox)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateColors();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var level = new GameObject("Blockout").transform;

            var workshop = Room(level, "1_Workshop", new Rect(0, 0, 125, 100), (Side.North, 20, 45));
            var hallway = Room(level, "2_Hallway", new Rect(0, 100, 225, 40), (Side.South, 20, 45), (Side.South, 150, 175), (Side.North, 100, 125));
            var bedroom = Room(level, "3_Bedroom", new Rect(125, 0, 100, 100), (Side.North, 150, 175));
            var kitchen = Room(level, "4_Kitchen", new Rect(50, 140, 125, 90), (Side.South, 100, 125));

            Workshop(workshop);
            Hallway(hallway);
            Bedroom(bedroom);
            Kitchen(kitchen);
            Legend(level, new Vector3(12f, 20f, 40f));

            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;

            BuildPlayer(new Vector3(25f, 0.05f, 22f), 60f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log("[Blockout] Built " + ScenePath);
        }

        // ================= Глава 1 / 5: Мастерская =================

        static void Workshop(Transform room)
        {
            var g = Group(room, "Gameplay");

            // Старт
            Block(g, "ShrinkRay_Base", V(8, 0, 8), V(18, 3, 18));
            Block(g, "ShrinkRay_Emitter", V(11, 3, 11), V(15, 9, 15), Kind.Interact);
            Label(g, "Гл.1 СТАРТ\nЛуч уменьшения", V(13, 12, 13), Ch1);

            // Обучение: прыжок и присед
            Block(g, "Tut_JumpBook", V(32, 0, 12), V(38, 1.5f, 32));
            Label(g, "Обучение: прыжок (Space)", V(35, 4, 22), Ch1);
            Raised(g, "Tut_CrouchCabinet", V(45, 0, 6), V(65, 16, 26), CrawlGap);
            Block(g, "Pickup_Notebook", V(53, 0, 14), V(57, 0.5f, 18), Kind.Pickup);
            Label(g, "Обучение: присед (Shift)\nпод тумбой — БЛОКНОТ (меню задач, Tab)", V(55, 19, 16), Ch1);

            // Подъём на стол: книги → ящик с инструментами → провод (лазание)
            for (int i = 0; i < 7; i++)
            {
                float top = 1.5f * (i + 1), cx = 30f + 8f * i;
                Block(g, $"Step_{i + 1}_Books_{top:0.#}m", V(cx - 3, 0, 56), V(cx + 3, top, 64));
            }
            Block(g, "Step_8_Toolbox_12m", V(83, 0, 55), V(96, 12, 65));
            Block(g, "Climb_LampCable", V(97, 12, 59.5f), V(98, 25, 60.5f), Kind.Climb);
            Label(g, "Паркур: книги → коробки → ящик (+1.5 м на прыжок)", V(55, 14, 60), Ch1);
            Label(g, "Лазание по проводу (выносливость)", V(97.5f, 28, 60), Ch1);

            // Два стола с разрывом
            Table(g, "DeskB_North", 99, 123, 52, 92, 25f);
            Table(g, "DeskA_South", 99, 123, 8, 46, 25f);
            Block(g, "Checkpoint_DeskB", V(100.5f, 25, 54), V(106, 25.2f, 60), Kind.Checkpoint);

            // Стол B: клавиатура, монитор, пульт прибора
            Block(g, "Keyboard", V(101, 25, 64), V(107, 25.6f, 80));
            Block(g, "Monitor_Stand", V(116.5f, 25, 69), V(118.5f, 30, 73));
            Block(g, "Monitor", V(117, 30, 62), V(119, 39, 80));
            Block(g, "PencilCup", V(115, 25, 54), V(121, 34, 60));
            Block(g, "ControlPanel", V(104, 25, 82), V(112, 29, 90), Kind.Interact);
            Label(g, "Гл.1 ПУЛЬТ: режим «увеличение» + кнопка\n→ искры: МИКРОСХЕМА ПЕРЕГОРЕЛА\nГл.5: вставить микросхему → кнопка → таймер 30 с", V(108, 33, 86), Ch5);

            // Разрыв между столами
            Label(g, "Разрыв 6 м — прыжок с разбега (Ctrl)", V(111, 29, 49), Ch5);

            // Стол A: прибор увеличения
            var machine = Group(g, "EnlargeMachine");
            Cyl(machine, "Pad", V(111, 25, 22), 7f, 1f, Kind.Goal);
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * (0.25f + 0.5f * i);
                Cyl(machine, $"Pillar_{i}", V(111 + Mathf.Cos(a) * 6f, 26, 22 + Mathf.Sin(a) * 6f), 0.5f, 10f);
            }
            Cyl(machine, "Top", V(111, 36, 22), 7f, 1f);
            Label(g, "ФИНАЛ: прибор увеличения\nзапрыгнуть на платформу до конца таймера", V(111, 40, 22), Ch5);
            Block(g, "DeskLamp_Base", V(115, 25, 36), V(121, 26, 42));
            Block(g, "DeskLamp_Pole", V(117, 26, 38), V(119, 40, 40));

            // Под столами и декор
            Block(g, "PC_Tower", V(101, 0, 70), V(111, 11, 80));
            Cyl(g, "TrashBin", V(86, 0, 75), 5f, 12f);

            // Шорткат главы 5: линейка падает со стола и становится пандусом (без коллайдера до события)
            Ramp(g, "Event_RulerShortcut", V(55, 0, 88), V(99.5f, 25, 88), 4f, Kind.Event);
            Label(g, "Гл.5 шорткат: линейка падает → пандус на стол", V(75, 16, 88), Ch5);

            // Выход: закрытая дверь, проползти под ней
            Block(g, "Door_Workshop_Closed", V(20, CrawlGap, 98.5f), V(45, DoorH, 101.5f));
            Label(g, "Дверь закрыта: проползти в щель (присед)", V(32.5f, 5, 96), Ch2);

            Route(g, "Route_Ch1", Ch1, V(25, 1, 22), V(35, 2.5f, 22), V(55, 1, 16), V(30, 2.5f, 60), V(54, 7, 60), V(78, 12, 60),
                V(89, 13, 60), V(97.5f, 13, 60), V(97.5f, 25, 60), V(103, 26, 57), V(108, 26, 80));
            Route(g, "Route_Ch5_Final", Ch5, V(108, 26, 80), V(111, 26, 60), V(111, 27, 46), V(111, 26, 22));
            Route(g, "Route_Ch1_Exit", Ch2, V(108, 26, 80), V(70, 1, 88), V(32.5f, 0.5f, 96), V(32.5f, 0.5f, 104));
        }

        // ================= Глава 2: Коридор =================

        static void Hallway(Transform room)
        {
            var g = Group(room, "Gameplay");
            Block(g, "Rug", V(10, 0, 106), V(215, 0.3f, 134));

            Cyl(g, "RobotVacuum", V(100, 0, 120), 8f, 2.5f, Kind.Moving);
            Label(g, "Гл.2 РОБОТ-ПЫЛЕСОС: патруль по коридору\nзаметил — откидывает к началу", V(100, 8, 120), Ch2);
            Route(g, "Route_VacuumPatrol", Kind2Color(Kind.Moving), V(25, 1.5f, 120), V(200, 1.5f, 120), V(200, 1.5f, 110), V(25, 1.5f, 110), V(25, 1.5f, 120));

            Block(g, "Cover_Shoe_1", V(60, 0, 127), V(74, 7, 137));
            Block(g, "Cover_Shoe_2", V(130, 0, 103), V(144, 7, 113));
            Block(g, "Cover_Shoe_3", V(178, 0, 126), V(192, 7, 136));
            Label(g, "Укрытия: ботинки", V(67, 10, 132), Ch2);

            Block(g, "Checkpoint_Hall_Kitchen", V(108, 0.3f, 134), V(117, 0.5f, 138.5f), Kind.Checkpoint);
            Block(g, "Checkpoint_Hall_Bedroom", V(158, 0.3f, 101.5f), V(167, 0.5f, 106), Kind.Checkpoint);

            Route(g, "Route_Ch2", Ch2, V(32.5f, 0.5f, 104), V(67, 0.5f, 120), V(137, 0.5f, 118), V(162.5f, 0.5f, 104), V(162.5f, 0.5f, 96));
            Route(g, "Route_Ch2_ToKitchen", Ch4, V(162.5f, 0.5f, 104), V(140, 0.5f, 124), V(112.5f, 0.5f, 136), V(112.5f, 0.5f, 144));
        }

        // ================= Глава 3: Спальня =================

        static void Bedroom(Transform room)
        {
            var g = Group(room, "Gameplay");

            // Кровать (изголовье у восточной стороны)
            Block(g, "Bed_Frame", V(140, 0, 30), V(190, 10, 80));
            Block(g, "Bed_Mattress", V(140, 10, 30), V(190, 13, 80));
            Block(g, "Bed_Headboard", V(190, 0, 30), V(192, 28, 80));
            Block(g, "Pillow_1", V(178, 13, 34), V(188, 15.5f, 53));
            Block(g, "Pillow_2", V(178, 13, 57), V(188, 15.5f, 76));
            Ramp(g, "Blanket_Slope", V(157.5f, 0, 12), V(157.5f, 13, 30), 15f, Kind.Climb);
            Label(g, "Гл.3 Свисающее одеяло — подъём на кровать", V(157.5f, 10, 18), Ch3);
            Block(g, "Bounce_FallenPillow", V(141, 13, 44), V(149, 15, 58), Kind.Bounce);
            Label(g, "Подушка-батут → 2-я полка", V(145, 19, 51), Ch3);
            Block(g, "Checkpoint_Bed", V(160, 13, 50), V(168, 13.2f, 58), Kind.Checkpoint);

            // Тумбочка с пультом (батарейка)
            Block(g, "Nightstand", V(184, 0, 14), V(196, 13, 26));
            Block(g, "TVRemote", V(187, 13, 17), V(193, 14, 23), Kind.Interact);
            Label(g, "ПУЛЬТ: открыть крышку → БАТАРЕЙКА\n(прыжок с кровати)", V(190, 17, 20), Ch3);

            // Книжный стеллаж у западной стены
            var shelf = Group(g, "Bookshelf");
            Block(shelf, "Side_S", V(126.5f, 0, 29), V(137, 51, 30));
            Block(shelf, "Side_N", V(126.5f, 0, 90), V(137, 51, 91));
            Block(shelf, "Board_0", V(126.5f, 0, 30), V(137, 1, 90));
            Block(shelf, "Board_1_12.5m", V(126.5f, 11.5f, 30), V(137, 12.5f, 90));
            Block(shelf, "Board_2_25m", V(126.5f, 24, 30), V(137, 25, 90));
            Block(shelf, "Board_3_37.5m", V(126.5f, 36.5f, 30), V(134.5f, 37.5f, 90));
            Block(shelf, "Board_4_50m", V(126.5f, 50, 30), V(137, 51, 90));
            Block(shelf, "Shelf2_Hurdle_1", V(126.5f, 25, 46), V(137, 26.5f, 50));
            Block(shelf, "Shelf2_Hurdle_2", V(126.5f, 25, 58), V(137, 27, 62));
            Block(shelf, "Shelf2_BookWall", V(126.5f, 25, 64), V(132, 35, 72));
            Block(shelf, "Climb_BookmarkRibbon", V(135, 25, 74), V(135.8f, 40, 75.5f), Kind.Climb);
            Block(shelf, "Radio", V(127.5f, 37.5f, 79), V(134, 45, 88), Kind.Interact);
            Label(g, "РАДИО на 3-й полке: закрыто винтами\nс ОТВЁРТКОЙ (Гл.4) → МИКРОСХЕМА", V(131, 49, 83), Ch3);
            Label(g, "Полка 2: барьеры, лента-закладка вверх", V(132, 30, 55), Ch3);

            // Шкаф на ножках: секрет под ним
            Raised(g, "Wardrobe", V(206, 0, 45), V(223.5f, 50, 98.5f), CrawlGap);
            Block(g, "Pickup_Paperclip_Secret", V(213, 0, 68), V(217, 0.4f, 74), Kind.Pickup);
            Label(g, "Секрет: под шкафом СКРЕПКА (присед)", V(214, 4, 71), Ch3);

            Route(g, "Route_Ch3", Ch3, V(162.5f, 0.5f, 96), V(157.5f, 0.5f, 8), V(157.5f, 14, 34), V(164, 14, 54), V(186, 14, 31), V(190, 14, 20),
                V(186, 14, 31), V(145, 16, 51), V(132, 26, 40), V(134, 26, 74), V(135.4f, 38, 74.5f), V(131, 38.5f, 78));
        }

        // ================= Глава 4: Кухня =================

        static void Kitchen(Transform room)
        {
            var g = Group(room, "Gameplay");
            const float top = 23.5f;

            // Холодильник с магнитами-зацепами (бонус наверху)
            Block(g, "Fridge", V(51.5f, 0, 150), V(80, 45, 185));
            Block(g, "Climb_FridgeMagnets", V(80, 4, 162), V(80.6f, 45, 174), Kind.Climb);
            Block(g, "Pickup_Gear_Bonus", V(62, 45, 164), V(68, 45.5f, 170), Kind.Pickup);
            Label(g, "Бонус: магниты → верх холодильника, ШЕСТЕРЁНКА", V(80, 48, 168), Ch4);

            // Столешница вдоль северной стены: секции [левая | раковина | средняя | плита]
            Block(g, "Counter_Left", V(51.5f, 0, 205), V(95, top, 228.5f));
            Block(g, "Sink_Base", V(95, 0, 205), V(125, 14, 228.5f));
            Block(g, "Sink_RimFront", V(95, 14, 205), V(125, top, 207));
            Block(g, "Sink_RimBack", V(95, 14, 226.5f), V(125, top, 228.5f));
            Block(g, "Hazard_SinkWater", V(95, 14, 207), V(125, 21, 226.5f), Kind.Hazard);
            Block(g, "Moving_Sponge_1", V(98, 21, 211), V(103, 23, 222), Kind.Moving);
            Block(g, "Moving_Sponge_2", V(106.5f, 21, 211), V(111.5f, 23, 222), Kind.Moving);
            Block(g, "Moving_Sponge_3", V(115, 21, 211), V(120, 23, 222), Kind.Moving);
            Label(g, "Раковина: вода = падение,\nпрыжки по губкам (покачиваются)", V(110, 28, 216), Ch4);
            Block(g, "Counter_Mid", V(125, 0, 205), V(140, top, 228.5f));
            Block(g, "Bounce_Toaster", V(128, top, 210), V(137, 33, 224), Kind.Bounce);
            Block(g, "UpperCabinet", V(125, 45, 214), V(173.5f, 60, 228.5f));
            Label(g, "Бонус: тостер-катапульта → верхний шкаф", V(132, 37, 217), Ch4);
            Block(g, "Counter_Stove", V(140, 0, 205), V(173.5f, top, 228.5f));
            Cyl(g, "Burner_1", V(149, top, 212), 5f, 0.5f, Kind.Hazard);
            Cyl(g, "Burner_2_Hot", V(165, top, 212), 5f, 0.5f, Kind.Hazard);
            Cyl(g, "Burner_3_Hot", V(149, top, 222), 5f, 0.5f, Kind.Hazard);
            Cyl(g, "Burner_4", V(165, top, 222), 5f, 0.5f, Kind.Hazard);
            Block(g, "Pickup_Screwdriver", V(156, top, 206), V(158, top + 0.6f, 227), Kind.Pickup);
            Label(g, "Гл.4 ПЛИТА: конфорки раскаляются по таймеру\nмежду ними — ОТВЁРТКА", V(157, 30, 217), Ch4);

            // Игрочный лифт со слотом батарейки
            Block(g, "Elevator_Post_L", V(59, 0, 196), V(60.5f, top, 197.5f));
            Block(g, "Elevator_Post_R", V(69.5f, 0, 196), V(71, top, 197.5f));
            Block(g, "Moving_ElevatorPlatform", V(60.5f, 0, 197), V(69.5f, 1, 205), Kind.Moving);
            Block(g, "BatterySlot", V(72, 0, 199), V(75, 4, 203), Kind.Interact);
            Label(g, "ИГРУШЕЧНЫЙ ЛИФТ: вставить БАТАРЕЙКУ (из спальни)\n→ подъём на столешницу", V(66, 8, 200), Ch4);
            Block(g, "Checkpoint_Counter", V(54, top, 210), V(62, top + 0.2f, 218), Kind.Checkpoint);

            // Обеденный стол со стульями (альтернативная зона, бонус-скрепка)
            Table(g, "DiningTable", 95, 135, 160, 185, 19.5f);
            Block(g, "Chair_Seat", V(84, 0, 165), V(92, 11, 180));
            Block(g, "Chair_Back", V(84, 11, 165), V(85.5f, 26, 180));
            Block(g, "Pickup_Paperclip_Table", V(112, 19.5f, 170), V(116, 19.9f, 176), Kind.Pickup);
            Label(g, "Бонус: стул → стол, СКРЕПКА", V(114, 24, 173), Ch4);

            Route(g, "Route_Ch4", Ch4, V(112.5f, 0.5f, 144), V(90, 0.5f, 195), V(73.5f, 4.5f, 201), V(65, 1.5f, 201), V(65, 24, 201), V(58, 24, 214),
                V(94, 24, 216), V(100.5f, 23.5f, 216), V(109, 23.5f, 216), V(117.5f, 23.5f, 216), V(127, 24, 216), V(145, 24, 217), V(157, 24.5f, 217));
        }

        static void Legend(Transform parent, Vector3 pos)
        {
            var text = "ЛЕГЕНДА МАКЕТА\n" +
                       "серый — геометрия\nоранжевый — взаимодействие (E)\nжёлтый — предмет в инвентарь\n" +
                       "красный — опасность\nсиний — лазание\nфиолетовый — батут\nбирюзовый — движется\n" +
                       "зелёный — чекпоинт/цель\nголубой без коллайдера — появится по событию\n\n" +
                       "Квест: Блокнот → Батарейка (спальня) → Лифт (кухня)\n→ Отвёртка → Радио (спальня) → Микросхема → Пульт";
            Label(parent, text, pos, Color.white);
        }

        // ================= Комнаты =================

        static Transform Room(Transform parent, string name, Rect r, params (Side side, float a, float b)[] doors)
        {
            var room = Group(parent, name);
            var shell = Group(room, "Shell");
            Block(shell, "Floor", V(r.xMin, -1, r.yMin), V(r.xMax, 0, r.yMax));
            var ceiling = Block(shell, "Ceiling (off for overview)", V(r.xMin, H, r.yMin), V(r.xMax, H + 1, r.yMax));
            ceiling.SetActive(false);

            Wall(shell, "Wall_S", true, r.yMin, r.yMin + T, r.xMin, r.xMax, Doors(doors, Side.South));
            Wall(shell, "Wall_N", true, r.yMax - T, r.yMax, r.xMin, r.xMax, Doors(doors, Side.North));
            Wall(shell, "Wall_W", false, r.xMin, r.xMin + T, r.yMin + T, r.yMax - T, Doors(doors, Side.West));
            Wall(shell, "Wall_E", false, r.xMax - T, r.xMax, r.yMin + T, r.yMax - T, Doors(doors, Side.East));
            foreach (var t in shell.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            return room;
        }

        static Vector2[] Doors((Side side, float a, float b)[] doors, Side side) =>
            doors.Where(d => d.side == side).Select(d => new Vector2(d.a, d.b)).OrderBy(d => d.x).ToArray();

        // Стена между линиями c0..c1 (толщина) на отрезке from..to, с дверными проёмами и перемычками.
        static void Wall(Transform p, string name, bool alongX, float c0, float c1, float from, float to, Vector2[] openings)
        {
            float cur = from;
            int i = 0;
            foreach (var o in openings)
            {
                if (o.x > cur) WallPart(p, $"{name}_{i++}", alongX, c0, c1, cur, o.x, 0, H);
                WallPart(p, $"{name}_Lintel_{i}", alongX, c0, c1, o.x, o.y, DoorH, H);
                cur = o.y;
            }
            if (cur < to) WallPart(p, $"{name}_{i}", alongX, c0, c1, cur, to, 0, H);
        }

        static void WallPart(Transform p, string name, bool alongX, float c0, float c1, float a, float b, float y0, float y1)
        {
            if (alongX) Block(p, name, V(a, y0, c0), V(b, y1, c1));
            else Block(p, name, V(c0, y0, a), V(c1, y1, b));
        }

        // ================= Примитивы =================

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static GameObject Block(Transform p, string name, Vector3 min, Vector3 max, Kind kind = Kind.Solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.position = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            Paint(go, kind);
            return go;
        }

        // Мебель на ножках с щелью снизу (проползти в приседе).
        static void Raised(Transform p, string name, Vector3 min, Vector3 max, float gap)
        {
            var root = Group(p, name);
            Block(root, "Body", V(min.x, gap, min.z), max);
            const float leg = 1.2f;
            Block(root, "Leg_SW", V(min.x, 0, min.z), V(min.x + leg, gap, min.z + leg));
            Block(root, "Leg_SE", V(max.x - leg, 0, min.z), V(max.x, gap, min.z + leg));
            Block(root, "Leg_NW", V(min.x, 0, max.z - leg), V(min.x + leg, gap, max.z));
            Block(root, "Leg_NE", V(max.x - leg, 0, max.z - leg), V(max.x, gap, max.z));
        }

        static void Table(Transform p, string name, float x0, float x1, float z0, float z1, float topY)
        {
            var root = Group(p, name);
            Block(root, "Top", V(x0, topY - 1.5f, z0), V(x1, topY, z1));
            const float leg = 2f;
            Block(root, "Leg_1", V(x0, 0, z0), V(x0 + leg, topY - 1.5f, z0 + leg));
            Block(root, "Leg_2", V(x1 - leg, 0, z0), V(x1, topY - 1.5f, z0 + leg));
            Block(root, "Leg_3", V(x0, 0, z1 - leg), V(x0 + leg, topY - 1.5f, z1));
            Block(root, "Leg_4", V(x1 - leg, 0, z1 - leg), V(x1, topY - 1.5f, z1));
        }

        // Наклонная плита: верхняя поверхность проходит через точки from → to.
        static GameObject Ramp(Transform p, string name, Vector3 from, Vector3 to, float width, Kind kind = Kind.Solid)
        {
            const float thick = 0.5f;
            var dir = to - from;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.SetPositionAndRotation((from + to) * 0.5f - rot * Vector3.up * thick * 0.5f, rot);
            go.transform.localScale = new Vector3(width, thick, dir.magnitude);
            Paint(go, kind);
            return go;
        }

        static GameObject Cyl(Transform p, string name, Vector3 bottomCenter, float radius, float height, Kind kind = Kind.Solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.position = bottomCenter + Vector3.up * height * 0.5f;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
            Paint(go, kind);
            return go;
        }

        static void Paint(GameObject go, Kind kind)
        {
            if (kind == Kind.Solid) return;
            if (UseColorCoding) go.GetComponent<Renderer>().sharedMaterial = colors[kind];
            var col = go.GetComponent<Collider>();
            if (kind == Kind.Event) Object.DestroyImmediate(col);
            else if (kind == Kind.Pickup || kind == Kind.Hazard || kind == Kind.Checkpoint) col.isTrigger = true;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static void Label(Transform p, string text, Vector3 pos, Color color)
        {
            var go = new GameObject("Label_" + text.Split('\n')[0]);
            go.transform.SetParent(p, false);
            go.transform.position = pos;
            var label = go.AddComponent<BlockoutLabel>();
            label.text = text;
            label.color = color;
        }

        static void Route(Transform p, string name, Color color, params Vector3[] points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            var route = go.AddComponent<BlockoutRoute>();
            route.color = color;
            route.points = points;
        }

        static GameObject BuildPlayer(Vector3 position, float yaw)
        {
            var player = new GameObject("Player") { tag = "Player", layer = 2 };
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 45f;

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
            return player;
        }

        // ================= Цветовая легенда =================

        static Color Kind2Color(Kind k)
        {
            switch (k)
            {
                case Kind.Interact: return new Color(1f, 0.55f, 0.1f);
                case Kind.Pickup: return new Color(1f, 0.9f, 0.1f);
                case Kind.Hazard: return new Color(0.9f, 0.15f, 0.1f);
                case Kind.Climb: return new Color(0.2f, 0.45f, 1f);
                case Kind.Bounce: return new Color(0.7f, 0.3f, 0.9f);
                case Kind.Moving: return new Color(0.1f, 0.85f, 0.85f);
                case Kind.Checkpoint: return new Color(0.2f, 0.8f, 0.3f);
                case Kind.Goal: return new Color(0.55f, 1f, 0.2f);
                case Kind.Event: return new Color(0.6f, 0.85f, 1f);
                default: return Color.gray;
            }
        }

        static void CreateColors()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Blockout")) AssetDatabase.CreateFolder("Assets", "Blockout");
            if (!AssetDatabase.IsValidFolder(ColorDir)) AssetDatabase.CreateFolder("Assets/Blockout", "Colors");
            colors.Clear();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (Kind k in System.Enum.GetValues(typeof(Kind)))
            {
                if (k == Kind.Solid) continue;
                string path = $"{ColorDir}/Blockout_{k}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(lit);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.SetColor("_BaseColor", Kind2Color(k));
                m.SetFloat("_Smoothness", 0.2f);
                EditorUtility.SetDirty(m);
                colors[k] = m;
            }
            AssetDatabase.SaveAssets();
        }
    }
}
