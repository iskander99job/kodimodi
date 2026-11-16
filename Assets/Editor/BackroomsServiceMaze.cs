#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BackroomsServiceMaze
{
    class Tile
    {
        public GameObject prefab;
        public int w, d;
        public Quaternion rotation;
    }
    struct Plan
    {
        public int count, tile, axis, cut;
    }
    class WallRun
    {
        public Transform parent;
        public Vector2 a, b;
        public float y, height, thickness;
        public bool door, guard;
        public string feature;
        public Vector2 Direction => (b - a).normalized;
        public float Length => Vector2.Distance(a, b);
    }
    const float Epsilon = 0.0001f;
    static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    static readonly List<Tile> tiles = new List<Tile>();
    static readonly List<WallRun> walls = new List<WallRun>();
    const int FloorCount = 4;
    const int Width = 24;
    static readonly int[] Lengths = { 36, 32, 28, 24 };
    static readonly int[] Heights = { 6, 4, 5, 3 };
    static readonly float[] Bases = new float[FloorCount];
    static float currentWallHeight = 3;

    [MenuItem("Tools/Backrooms/Create Service Maze")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject root = null;
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create service maze");
        try
        {
            prefabs.Clear(); walls.Clear(); LoadTiles();
            for (int h = 1; h <= 3; h++)
                for (int w = 1; w <= 3; w++)
                    CheckPanel(Find("BR_Wall_A_" + h + "x" + w), w, h);
            CheckPanel(Find("BR_Wall_A_3x3_Door_A_Alt"), 3, 3);
            CheckPanel(Find("BR_Wall_A_3x2_Window_A_Alt"), 2, 3);
            CheckPanel(Find("BR_Wall_A_3x2_Window_C_Alt"), 2, 3);
            CheckPanel(Find("BR_Wall_A_3x3_Hole"), 3, 3);
            for (int h = 1; h <= 3; h++)
                Check(Find("Stair_" + h + "x2"), new Vector3(2, h + 0.25f, h));
            foreach (string name in new[] { "BR_Floor_3x3_Hole", "BR_Floor_3x3",
                "BR_Wall_B_Post_3m", "BR_Wall_A_Post_1m", "BR_Wall_A_Post_2m",
                "BR_Wall_A_Corner_1m_Alt", "BR_Wall_A_3x3_Debris" }) Find(name);

            float y = 0;
            for (int level = 0; level < FloorCount; level++)
            { Bases[level] = y; y += Heights[level]; }

            root = new GameObject("Backrooms_ServiceMaze");
            var levels = new Transform[FloorCount];
            for (int level = 0; level < FloorCount; level++)
            {
                levels[level] = Group("Level_" + (level + 1), root.transform);
                BuildSlab(Group("Floor_and_CeilingBelow", levels[level]), level);
                currentWallHeight = Heights[level];
                BuildPlan(Group("Walls", levels[level]), level);
                AddStairGuards(Group("StairGuards", levels[level]), level);
            }
            BuildSlab(Group("Roof", root.transform), FloorCount);
            Transform stairs = Group("Stairs", root.transform);
            for (int level = 0; level < FloorCount - 1; level++) BuildStairs(stairs, level);

            AddCover(levels);
            BuildWallGeometry();
            AddDetails(levels);
            AddMarkers(root.transform);

            Undo.RegisterCreatedObjectUndo(root, "Create service maze");
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }
        catch (Exception error)
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            Debug.LogException(error);
            EditorUtility.DisplayDialog("Backrooms", error.Message, "OK");
        }
        finally { prefabs.Clear(); tiles.Clear(); walls.Clear(); currentWallHeight = 3; }
    }

    // Different floor plans, connected by two independent enclosed stair rooms.
    static void BuildPlan(Transform parent, int level)
    {
        float y = Bases[level];
        int length = Lengths[level];
        Wall(parent, 0, 0, Width, 0, y);
        Wall(parent, 0, 0, 0, length, y);
        Wall(parent, Width, 0, Width, length, y);
        Wall(parent, 0, length, Width, length, y);

        Wall(parent, 0, 4, 4, 4, y);
        Wall(parent, 0, 14, 4, 14, y);
        Wall(parent, 4, 4, 4, 14, y, true);
        Wall(parent, 20, 4, 24, 4, y);
        Wall(parent, 20, 14, 24, 14, y);
        Wall(parent, 20, 4, 20, 14, y, true);

        // Fall shaft: accessible only from the ground and the top floor.
        Wall(parent, 6, 9, 9, 9, y, level == 0 || level == FloorCount - 1);
        Wall(parent, 6, 12, 9, 12, y);
        Wall(parent, 6, 9, 6, 12, y);
        Wall(parent, 9, 9, 9, 12, y);

        // Three-metre open passages flank the rear zone.
        Wall(parent, 3, 16, 21, 16, y);
        if (level == 0)
        {
            // A large 24 x 20 m hall. Broken sightlines, no internal doors.
            Wall(parent, 8, 21, 8, 27, y);
            Wall(parent, 16, 27, 16, 33, y);
        }
        else if (level == 1)
        {
            // Six rooms arranged in a loop. Each doorway connects a different pair.
            Wall(parent, 8, 16, 8, 24, y, true);
            Wall(parent, 8, 24, 8, 32, y, true);
            Wall(parent, 16, 16, 16, 24, y, true);
            Wall(parent, 16, 24, 16, 32, y, true);
            Wall(parent, 0, 24, 8, 24, y, true);
            Wall(parent, 8, 24, 16, 24, y);
            Wall(parent, 16, 24, 24, 24, y, true);
            // Offset partitions inside two rooms create bends, not sealed pockets.
            Wall(parent, 3, 28, 6, 28, y);
            Wall(parent, 18, 20, 21, 20, y);
        }
        else if (level == 2)
        {
            // Service archive: a 10 x 12 room, a passage and two smaller rooms.
            Wall(parent, 10, 16, 10, 28, y, true, false,
                "BR_Wall_A_3x2_Window_A_Alt");
            Wall(parent, 16, 16, 16, 22, y, true);
            Wall(parent, 16, 22, 16, 28, y, true);
            Wall(parent, 16, 22, 24, 22, y, false, false,
                "BR_Wall_A_3x3_Hole");
        }
        else
        {
            // Low maintenance floor: staggered walls form a shallow labyrinth.
            Wall(parent, 7, 16, 7, 21, y);
            Wall(parent, 12, 19, 12, 24, y);
            Wall(parent, 17, 16, 17, 21, y);
        }

        // Different routes around the front lobby on every floor.
        if (level == 0) Wall(parent, 13, 4, 13, 11, y, true);
        if (level == 1)
        {
            Wall(parent, 11, 4, 17, 4, y);
            Wall(parent, 14, 8, 20, 8, y, true);
        }
        if (level == 2) Wall(parent, 13, 5, 13, 12, y, true);
        if (level == 3) Wall(parent, 12, 5, 18, 5, y, true);
    }

    static void AddCover(Transform[] levels)
    {
        Transform p = Group("HallCover", levels[0]);
        Wall(p, 4, 30, 7, 30, Bases[0], false, true);
        Wall(p, 7, 30, 7, 32, Bases[0], false, true);
        Wall(p, 18, 21, 21, 21, Bases[0], false, true);
        p = Group("MazeCover", levels[1]);
        Wall(p, 11, 28, 14, 28, Bases[1], false, true);
        p = Group("ArchiveCover", levels[2]);
        Wall(p, 3, 19, 3, 25, Bases[2], false, true);
        Wall(p, 6, 20, 6, 26, Bases[2], false, true);
        p = Group("MaintenanceCover", levels[3]);
        Wall(p, 14, 11, 17, 11, Bases[3], false, true);
    }

    static void BuildSlab(Transform parent, int plane)
    {
        int length = plane == 0 ? Lengths[0] : Lengths[plane - 1];
        float y = plane == FloorCount ? Bases[FloorCount - 1] + Heights[FloorCount - 1] : Bases[plane];
        var holes = new List<RectInt>();
        if (plane > 0 && plane < FloorCount)
        {
            int rise = Heights[plane - 1];
            holes.Add(new RectInt(0, 5, 2, rise + 1));
            holes.Add(new RectInt(22, 12 - rise, 2, rise + 1));
            holes.Add(new RectInt(6, 9, 3, 3));
        }
        var used = new bool[Width, length];
        foreach (RectInt hole in holes)
            for (int x = hole.xMin; x < hole.xMax; x++)
                for (int z = hole.yMin; z < hole.yMax; z++) used[x, z] = true;

        for (int z = 0; z < length; z++)
            for (int x = 0; x < Width; x++)
            {
                if (used[x, z]) continue;
                int w = 0;
                while (x + w < Width && !used[x + w, z]) w++;
                int d = 1;
                while (z + d < length)
                {
                    bool clear = true;
                    for (int xx = x; xx < x + w; xx++)
                        if (used[xx, z + d]) { clear = false; break; }
                    if (!clear) break;
                    d++;
                }
                Fill(parent, x, z, w, d, y);
                for (int xx = x; xx < x + w; xx++)
                    for (int zz = z; zz < z + d; zz++) used[xx, zz] = true;
            }
        if (plane == FloorCount - 1)
            Place(Find("BR_Floor_3x3_Hole"), parent, new Vector3(7.5f, y, 10.5f),
                Quaternion.identity, true, null, Find("BR_Floor_3x3"));
    }

    static void BuildStairs(Transform parent, int level)
    {
        float y = Bases[level];
        int remainder = Heights[level] - 3;
        GameObject first = Find("Stair_3x2");
        GameObject second = Find("Stair_" + remainder + "x2");
        Quaternion forwardFirst = StairRotation(first), forwardSecond = StairRotation(second);
        Quaternion reverse = Quaternion.Euler(0, 180, 0);

        Place(first, parent, new Vector3(1, y + 3, 6.5f), forwardFirst, true);
        Fill(parent, 0, 8, 2, 1, y + 3);
        Place(second, parent, new Vector3(1, y + Heights[level], 9 + remainder * 0.5f), forwardSecond, true);
        Place(first, parent, new Vector3(23, y + 3, 11.5f), reverse * forwardFirst, true);
        Fill(parent, 22, 9, 2, 1, y + 3);
        Place(second, parent, new Vector3(23, y + Heights[level], 9 - remainder * 0.5f), reverse * forwardSecond, true);

        Wall(parent, 2, 8, 2, 9, y + 3, false, true);
        Wall(parent, 22, 9, 22, 10, y + 3, false, true);
    }

    static void AddStairGuards(Transform parent, int level)
    {
        if (level == 0) return;
        float y = Bases[level];
        int rise = Heights[level - 1];
        Wall(parent, 2, 5, 2, 6 + rise, y, false, true);
        Wall(parent, 22, 12 - rise, 22, 13, y, false, true);
        if (level == FloorCount - 1)
        {
            Wall(parent, 0, 5, 2, 5, y, false, true);
            Wall(parent, 22, 13, 24, 13, y, false, true);
        }
    }

    static void AddDetails(Transform[] levels)
    {
        // Full-height posts give the large hall a repeating Backrooms rhythm.
        Transform columns = Group("HallColumns", levels[0]);
        foreach (int x in new[] { 4, 12, 20 })
            foreach (int z in new[] { 20, 26, 32 })
                AddColumn(columns, x, z, Bases[0], Heights[0]);
        for (int level = 1; level < FloorCount; level++)
        {
            Transform group = Group("LobbyColumns", levels[level]);
            AddColumn(group, 6, 2, Bases[level], Heights[level]);
            AddColumn(group, 18, 2, Bases[level], Heights[level]);
        }
        Place(Find("BR_Wall_A_3x3_Debris"), Group("ArchiveDebris", levels[2]),
            new Vector3(21, Bases[2], 24), Quaternion.Euler(90, 0, 0));
        GameObject corner = Find("BR_Wall_A_Corner_1m_Alt");
        Bounds b = BoundsIn(corner, corner.transform);
        GameObject wall = Find("BR_Wall_A_1x1");
        Vector3 size = BoundsIn(wall, wall.transform).size;
        float offset = size.z * 0.02f;
        Place(corner, Group("CornerFinish", levels[0]), new Vector3(
            7 - offset + b.center.x, Bases[0] + size.y - b.size.y,
            30 + offset + b.center.z), Quaternion.identity);
    }

    static void AddColumn(Transform parent, int x, int z, float y, int height)
    {
        while (height > 0)
        {
            int h = Mathf.Min(3, height);
            string name = h == 3 ? "BR_Wall_B_Post_3m" : "BR_Wall_A_Post_" + h + "m";
            Place(Find(name), parent, new Vector3(x, y, z), Quaternion.identity);
            y += h; height -= h;
        }
    }

    static void AddMarkers(Transform root)
    {
        Transform group = Group("SuggestedSpawns", root);
        Vector3[] positions = { new Vector3(3, Bases[0], 34), new Vector3(21, Bases[0], 18),
            new Vector3(4, Bases[1], 30), new Vector3(20, Bases[1], 30),
            new Vector3(8, Bases[2], 26), new Vector3(21, Bases[3], 22) };
        for (int i = 0; i < positions.Length; i++)
            Group("Spawn_" + (i + 1), group).localPosition = positions[i] + Vector3.up * 0.1f;
        Group("DropFromLevel4", root).localPosition = new Vector3(7.5f, Bases[3] + 0.1f, 10.5f);
    }

    static void Wall(Transform parent, int x0, int z0, int x1, int z1,
        float y, bool door = false, bool guard = false, string feature = null)
    {
        GameObject sample = Find(guard ? "BR_Wall_A_1x1" : "BR_Wall_A_3x1");
        Vector3 size = BoundsIn(sample, sample.transform).size;
        walls.Add(new WallRun { parent = parent, a = new Vector2(x0, z0),
            b = new Vector2(x1, z1), y = y, height = guard ? size.y : currentWallHeight,
            thickness = size.z, door = door, guard = guard, feature = feature });
    }

    static void PlaceFloorHole(GameObject prefab, Transform parent, int x, int z, float y)
    {
        Place(prefab, parent, new Vector3(x + 1, y, z + 1), Quaternion.identity,
            true, null, Find("BR_Floor_2x2"));
    }

    static bool ContainsPoint(WallRun run, Vector2 p)
    {
        Vector2 delta = p - run.a, direction = run.Direction;
        float cross = delta.x * direction.y - delta.y * direction.x;
        float distance = Vector2.Dot(delta, direction);
        return Mathf.Abs(cross) < Epsilon && distance >= -Epsilon &&
            distance <= run.Length + Epsilon;
    }

    static bool CoversHeight(WallRun other, WallRun run)
    {
        return other.y <= run.y + Epsilon &&
            other.y + other.height >= run.y + run.height - Epsilon;
    }

    static bool Continues(WallRun run, bool end)
    {
        Vector2 p = end ? run.b : run.a;
        Vector2 outward = end ? run.Direction : -run.Direction;
        foreach (WallRun other in walls)
        {
            if (other == run || !CoversHeight(other, run) || !ContainsPoint(other, p)) continue;
            if (Mathf.Abs(Vector2.Dot(run.Direction, other.Direction)) < 0.999f) continue;
            if (Vector2.Dot(other.a - p, outward) > Epsilon ||
                Vector2.Dot(other.b - p, outward) > Epsilon) return true;
        }
        return false;
    }

    static float JointPadding(WallRun run, bool end)
    {
        if (StraightOverlap(run, end)) return 0;
        Vector2 p = end ? run.b : run.a;
        float padding = 0;
        foreach (WallRun other in walls)
        {
            if (other == run || !CoversHeight(other, run) || !ContainsPoint(other, p)) continue;
            if (Mathf.Abs(Vector2.Dot(run.Direction, other.Direction)) > Epsilon) continue;
            padding = Mathf.Max(padding, other.thickness * 0.5f);
        }
        return padding;
    }

    static bool StraightOverlap(WallRun run, bool end)
    {
        Vector2 p = end ? run.b : run.a;
        Vector2 outward = end ? run.Direction : -run.Direction;
        foreach (WallRun other in walls)
        {
            if (other == run || !ContainsPoint(other, p)) continue;
            if (Mathf.Min(other.y + other.height, run.y + run.height) -
                Mathf.Max(other.y, run.y) <= Epsilon) continue;
            if (Mathf.Abs(Vector2.Dot(run.Direction, other.Direction)) < 0.999f) continue;
            if (Vector2.Dot(other.a - p, outward) > Epsilon ||
                Vector2.Dot(other.b - p, outward) > Epsilon) return true;
        }
        return false;
    }

    static void BuildWallGeometry()
    {
        foreach (WallRun run in walls)
        {
            float startPad = JointPadding(run, false), endPad = JointPadding(run, true);
            int length = Mathf.RoundToInt(run.Length);
            int doorStart = run.door ? (length - 3) / 2 : -1;
            int featureWidth = run.feature == null ? 0 : Mathf.RoundToInt(
                BoundsIn(Find(run.feature), Find(run.feature).transform).size.x);
            int featureStart = run.feature == null ? -1 : length - featureWidth;
            if (run.door && featureStart >= 0 && featureStart < doorStart + 3)
                throw new InvalidOperationException("Дверь и декоративный модуль пересекаются.");
            int cursor = 0;
            Vector3 direction = new Vector3(run.Direction.x, 0, run.Direction.y);
            Quaternion rotation = Quaternion.Euler(0, Mathf.Abs(run.Direction.x) > 0.5f ? 0 : 90, 0);
            while (cursor < length)
            {
                bool opening = run.door && cursor == doorStart;
                bool special = featureStart >= 0 && cursor == featureStart;
                int available = length - cursor;
                if (run.door && cursor < doorStart) available = Mathf.Min(available, doorStart - cursor);
                if (featureStart >= 0 && cursor < featureStart) available = Mathf.Min(available, featureStart - cursor);
                int size = opening ? 3 : special ? featureWidth
                    : available >= 3 ? 3 : available >= 2 ? 2 : 1;
                float from = cursor == 0 ? -startPad : cursor;
                float to = cursor + size == length ? length + endPad : cursor + size;
                Vector3 center = new Vector3(run.a.x, run.y, run.a.y) + direction * ((from + to) * 0.5f);
                int remaining = Mathf.RoundToInt(run.height);
                float layerY = run.y;
                bool bottom = true;
                while (remaining > 0)
                {
                    int h = Mathf.Min(3, remaining);
                    string name = bottom && special ? run.feature
                        : bottom && opening ? "BR_Wall_A_3x3_Door_A_Alt"
                        : "BR_Wall_A_" + h + "x" + size;
                    GameObject prefab = Find(name);
                    Vector3 sourceSize = BoundsIn(prefab, prefab.transform).size;
                    center.y = layerY;
                    Place(prefab, run.parent, center, rotation, false,
                        new Vector3((to - from) / sourceSize.x, h / sourceSize.y,
                            bottom && special ? 1 : run.thickness / sourceSize.z),
                        bottom && special ? Find("BR_Wall_A_3x" + featureWidth) : null);
                    layerY += h; remaining -= h; bottom = false;
                }
                cursor += size;
            }
            if (run.guard) AddTop(run, startPad, endPad);
            if (startPad == 0 && !Continues(run, false)) AddEnd(run, false);
            if (endPad == 0 && !Continues(run, true)) AddEnd(run, true);
        }
    }

    static void AddTop(WallRun run, float startPad, float endPad)
    {
        var intervals = new List<Vector2> { new Vector2(-startPad, run.Length + endPad) };
        // At a corner the horizontal cap covers the overlap square.
        // Cut it out of the vertical cap to avoid coplanar overlapping faces.
        if (Mathf.Abs(run.Direction.y) > 0.5f)
            foreach (WallRun other in walls)
            {
                if (!other.guard || Mathf.Abs(other.Direction.x) < 0.5f ||
                    Mathf.Abs(other.y + other.height - run.y - run.height) > Epsilon) continue;
                Vector2 crossing = new Vector2(run.a.x, other.a.y);
                if (!ContainsPoint(other, crossing)) continue;
                float center = Vector2.Dot(crossing - run.a, run.Direction);
                float from = center - other.thickness * 0.5f, to = center + other.thickness * 0.5f;
                var remaining = new List<Vector2>();
                foreach (Vector2 interval in intervals)
                {
                    if (to <= interval.x || from >= interval.y) { remaining.Add(interval); continue; }
                    if (from > interval.x) remaining.Add(new Vector2(interval.x, from));
                    if (to < interval.y) remaining.Add(new Vector2(to, interval.y));
                }
                intervals = remaining;
            }
        foreach (Vector2 interval in intervals)
            if (interval.y - interval.x > Epsilon) AddTopPiece(run, interval.x, interval.y);
    }

    static void AddTopPiece(WallRun run, float from, float to)
    {
        GameObject prefab = Find("BR_Wall_A_1x1");
        Vector3 size = BoundsIn(prefab, prefab.transform).size;
        float depth = run.thickness * 0.02f;
        Vector2 middle = run.a + run.Direction * ((from + to) * 0.5f);
        Quaternion rotation = Quaternion.Euler(0, Mathf.Abs(run.Direction.x) > 0.5f ? 0 : 90, 0)
            * Quaternion.Euler(90, 0, 0);
        Place(prefab, run.parent, new Vector3(middle.x, run.y + run.height, middle.y),
            rotation, false, new Vector3((to - from) / size.x,
                run.thickness / size.y, depth / size.z)).name = "GuardTop";
    }

    static void AddEnd(WallRun run, bool end)
    {
        GameObject prefab = Find("BR_Wall_A_1x1");
        Vector3 size = BoundsIn(prefab, prefab.transform).size;
        float depth = run.thickness * 0.02f;
        Vector2 p = end ? run.b : run.a;
        Vector2 outward = end ? run.Direction : -run.Direction;
        Vector3 normal = new Vector3(outward.x, 0, outward.y);
        Vector3 center = new Vector3(p.x, run.y, p.y) - normal * (depth * 0.5f);
        Place(prefab, run.parent, center, Quaternion.LookRotation(normal, Vector3.up), false,
            new Vector3(run.thickness / size.x, run.height / size.y, depth / size.z)).name = "WallEnd";
    }

    static void LoadTiles()
    {
        tiles.Clear();
        string folder = Path.GetDirectoryName(
            AssetDatabase.GetAssetPath(Find("BR_Floor_1x1"))).Replace('\\', '/');
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (!Regex.IsMatch(prefab.name, @"^BR_Floor_[0-9_]+x[0-9_]+$")) continue;
            if (prefab.transform.localScale != Vector3.one ||
                Quaternion.Angle(prefab.transform.localRotation, Quaternion.identity) > 0.01f)
                continue;
            Vector3 size = BoundsIn(prefab, prefab.transform).size;
            int w = Mathf.RoundToInt(size.x * 2), d = Mathf.RoundToInt(size.z * 2);
            if (w < 1 || d < 1 || Mathf.Abs(size.y - 0.25f) > 0.01f ||
                Mathf.Abs(size.x - w * 0.5f) > 0.01f ||
                Mathf.Abs(size.z - d * 0.5f) > 0.01f) continue;
            tiles.Add(new Tile { prefab = prefab, w = w, d = d, rotation = Quaternion.identity });
            if (w != d)
                tiles.Add(new Tile { prefab = prefab, w = d, d = w,
                    rotation = Quaternion.Euler(0, 90, 0) });
        }
        tiles.Sort((a, b) => string.CompareOrdinal(a.prefab.name, b.prefab.name));
        if (tiles.Count == 0) throw new InvalidOperationException("Не найдены плиты пола.");
    }

    static void Fill(Transform parent, int x, int z, int width, int depth,
        float y, bool alignTop = true)
    {
        int W = width * 2, D = depth * 2;
        const int Impossible = 100000;
        var plans = new Plan[W + 1, D + 1];
        for (int w = 1; w <= W; w++)
            for (int d = 1; d <= D; d++)
            {
                var best = new Plan { count = Impossible, tile = -1 };
                for (int i = 0; i < tiles.Count; i++)
                    if (tiles[i].w == w && tiles[i].d == d)
                    { best = new Plan { count = 1, tile = i }; break; }
                for (int cut = 1; cut < w && best.count > 1; cut++)
                {
                    int count = plans[cut, d].count + plans[w - cut, d].count;
                    if (count < best.count)
                        best = new Plan { count = count, tile = -1, axis = 1, cut = cut };
                }
                for (int cut = 1; cut < d && best.count > 1; cut++)
                {
                    int count = plans[w, cut].count + plans[w, d - cut].count;
                    if (count < best.count)
                        best = new Plan { count = count, tile = -1, axis = 2, cut = cut };
                }
                plans[w, d] = best;
            }
        if (plans[W, D].count >= Impossible)
            throw new InvalidOperationException("Не удалось подобрать плиты.");
        Emit(parent, x * 2, z * 2, W, D, y, alignTop, plans);
    }

    static void Emit(Transform parent, int x, int z, int w, int d,
        float y, bool alignTop, Plan[,] plans)
    {
        Plan plan = plans[w, d];
        if (plan.tile >= 0)
        {
            Tile tile = tiles[plan.tile];
            Place(tile.prefab, parent,
                new Vector3((x + w * 0.5f) * 0.5f, y, (z + d * 0.5f) * 0.5f),
                tile.rotation, alignTop);
        }
        else if (plan.axis == 1)
        {
            Emit(parent, x, z, plan.cut, d, y, alignTop, plans);
            Emit(parent, x + plan.cut, z, w - plan.cut, d, y, alignTop, plans);
        }
        else
        {
            Emit(parent, x, z, w, plan.cut, y, alignTop, plans);
            Emit(parent, x, z + plan.cut, w, d - plan.cut, y, alignTop, plans);
        }
    }

    static GameObject Find(string name)
    {
        if (prefabs.TryGetValue(name, out GameObject result)) return result;
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != name) continue;
            if (result != null) throw new InvalidOperationException("Дубликат префаба: " + name);
            result = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        if (result == null) throw new InvalidOperationException("Не найден префаб: " + name);
        if (result.transform.localScale != Vector3.one ||
            Quaternion.Angle(result.transform.localRotation, Quaternion.identity) > 0.01f)
            throw new InvalidOperationException("Изменён масштаб или поворот: " + name);
        prefabs.Add(name, result);
        return result;
    }

    static Bounds BoundsIn(GameObject obj, Transform space)
    {
        Bounds bounds = new Bounds();
        bool found = false;
        foreach (var filter in obj.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                throw new InvalidOperationException("Нет модели: " + obj.name);
            Bounds b = filter.sharedMesh.bounds;
            Matrix4x4 matrix = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1,
                        (i & 4) == 0 ? -1 : 1)));
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
        }
        if (!found) throw new InvalidOperationException("Нет геометрии: " + obj.name);
        return bounds;
    }

    static void Check(GameObject prefab, Vector3 expected)
    {
        Vector3 actual = BoundsIn(prefab, prefab.transform).size;
        if ((actual - expected).sqrMagnitude > 0.001f)
            throw new InvalidOperationException("Неожиданные размеры " + prefab.name + ": " + actual);
    }

    static void CheckPanel(GameObject prefab, float width, float height)
    {
        Vector3 size = BoundsIn(prefab, prefab.transform).size;
        if (Mathf.Abs(size.x - width) > 0.02f || Mathf.Abs(size.y - height) > 0.02f || size.z <= 0)
            throw new InvalidOperationException("Неожиданные размеры: " + prefab.name);
    }

    static GameObject Place(GameObject prefab, Transform parent, Vector3 target,
        Quaternion rotation, bool alignTop = false, Vector3? scale = null,
        GameObject reference = null)
    {
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = rotation;
        obj.transform.localScale = scale ?? Vector3.one;
        Bounds b = BoundsIn(obj, parent);
        if (reference != null)
            b = TransformBounds(BoundsIn(reference, reference.transform),
                Matrix4x4.TRS(Vector3.zero, rotation, obj.transform.localScale));
        obj.transform.localPosition = target - new Vector3(b.center.x,
            alignTop ? b.max.y : b.min.y, b.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
        foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) | StaticEditorFlags.ContributeGI);
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.stitchLightmapSeams = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
        }
        return obj;
    }

    static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
    {
        Bounds result = new Bounds(matrix.MultiplyPoint3x4(source.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            result.Encapsulate(matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1,
                    (i & 4) == 0 ? -1 : 1))));
        return result;
    }

    static Quaternion StairRotation(GameObject prefab)
    {
        Bounds b = BoundsIn(prefab, prefab.transform);
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            Matrix4x4 matrix = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            using (var data = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
            using (var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
            {
                data[0].GetVertices(vertices);
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 p = matrix.MultiplyPoint3x4(vertex);
                    if (p.y >= b.max.y - 0.01f) { sum += p; count++; }
                }
            }
        }
        if (count == 0) throw new InvalidOperationException("Не удалось определить верх лестницы.");
        Vector3 direction = sum / count - b.center;
        if (new Vector2(direction.x, direction.z).magnitude < 0.1f)
            throw new InvalidOperationException("Не удалось определить направление лестницы.");
        float yaw = Mathf.Round(-Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg / 90) * 90;
        return Quaternion.Euler(0, yaw, 0);
    }

    static Transform Group(string name, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }
}
#endif
