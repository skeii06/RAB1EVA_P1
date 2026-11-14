using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BuildBosqueScene
{
    const string ScenePath = "Assets/_RAB_Base/Scenes/SCN_LVL02_BOSQUE.unity";
    const string PlayerPath = "Assets/_RAB_Base/Prefabs/Player.prefab";
    const string PickupPath = "Assets/_RAB_Base/Prefabs/PickUp.prefab";
    const string PinePath = "Assets/_RAB_Base/Prefabs/CH_assets/plague/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_dead.prefab";
    const string GnarledTreePath = "Assets/_RAB_Base/Prefabs/CH_assets/plague/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_dead.prefab";
    const string TreePath = "Assets/TreeGenerator/Trees/Prefab/Prefab_Tree_Unity.prefab";
    const string GrassTexturePath = "Assets/_RAB_Base/Prefabs/CH_assets/Halloween_Graveyard_Kit_Godot/textures/Ground/Tex_Grass_BaseColor.tga";
    const string DirtTexturePath = "Assets/_RAB_Base/Prefabs/CH_assets/Halloween_Graveyard_Kit_Godot/textures/Ground/Tex_Dirt_BaseColor.tga";
    const string SwampGrassPath = "Assets/_RAB_Base/Prefabs/CH_assets/EmaceArt/NecroPOLY Dark Corners/Prefabs/Assets/Nature/EA_Swamp_Grass_04a_PRE.prefab";
    const string SwampRootPath = "Assets/_RAB_Base/Prefabs/CH_assets/EmaceArt/NecroPOLY Dark Corners/Prefabs/Assets/Nature/EA_Swamp_Root_04d_PRE.prefab";
    const string SwampStonePath = "Assets/_RAB_Base/Prefabs/CH_assets/EmaceArt/NecroPOLY Dark Corners/Prefabs/Assets/Nature/EA_Swamp_Stones_02a_PRE.prefab";
    const string DeadHillPath = "Assets/_RAB_Base/Prefabs/CH_assets/EmaceArt/NecroPOLY Dark Corners/Prefabs/Assets/Ground/EA_Swamp_Grass_Hill_Dead_01a_PRE.prefab";
    const string DeadShrubPath = "Assets/_RAB_Base/Prefabs/CH_assets/plague/Polytope Studio/Lowpoly_Environments/Prefabs/Shrubs/PT_Generic_Shrub_01_dead.prefab";
    const string DeadBodyPath = "Assets/_RAB_Base/Prefabs/CH_assets/DeadBody LITE/Prefabs/Hanging_Body/Standing/Deadbody_Stand (3).prefab";
    const string StingerPath = "Assets/_RAB_Base/Audio/SFX/horror-game-stinger-boom (1).mp3";
    const int MazeWidth = 55;
    const float CellSize = 5.2f;
    const float WallHeight = 2.35f;
    const float WallThickness = 0.48f;

    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    static readonly int[] Bits = { 1, 2, 4, 8 };
    static readonly int[] OppositeBits = { 4, 8, 1, 2 };

    [MenuItem("Tools/Roll of Fear/Build BOSQUE Level")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_RAB_Base/Scenes/SCN_LVL01.unity", OpenSceneMode.Single);
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "AudioManager", "PlayerCam", "Main Camera", "RespawnPoint", "Directional Light", "Global Volume", "Canvas", "EventSystem", "Points", "Player" };
        foreach (var go in scene.GetRootGameObjects())
            if (!keep.Contains(go.name)) UnityEngine.Object.DestroyImmediate(go);

        var player = GameObject.Find("Player");
        if (player == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (prefab == null) throw new Exception("Player prefab not found: " + PlayerPath);
            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
        player.name = "Player";

        const int width = MazeWidth;
        const int height = MazeWidth;
        float half = width * CellSize * 0.5f;
        var cellOrigin = new Vector3(-half + CellSize * 0.5f, 0, -half + CellSize * 0.5f);
        var startCell = new Vector2Int(width / 2, 0);
        var exitCell = new Vector2Int(width / 2, height - 1);
        Vector3 startPosition = cellOrigin;
        player.transform.position = startPosition + Vector3.up * 0.55f;
        var respawn = GameObject.Find("RespawnPoint");
        if (respawn != null) respawn.transform.position = player.transform.position;

        var cameraGo = GameObject.Find("PlayerCam");
        var cameraComponent = cameraGo != null ? cameraGo.GetComponent("CinemachineCamera") : null;
        if (cameraGo != null && cameraComponent != null)
        {
            var followTarget = new GameObject("BOSQUE | Objetivo de cámara (altura fija)");
            var follow = followTarget.AddComponent<BosqueCameraFollow>();
            follow.target = player.transform;
            follow.fixedX = 0f;
            follow.fixedY = 0.55f;
            follow.followX = true;
            follow.Snap();
            var serializedCamera = new SerializedObject(cameraComponent);
            var cameraTarget = serializedCamera.FindProperty("Target");
            var trackingTarget = cameraTarget != null ? cameraTarget.FindPropertyRelative("TrackingTarget") : null;
            if (trackingTarget != null)
            {
                trackingTarget.objectReferenceValue = followTarget.transform;
                serializedCamera.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cameraComponent);
            }
            var composer = cameraGo.GetComponent("CinemachinePositionComposer");
            if (composer != null)
            {
                var serializedComposer = new SerializedObject(composer);
                var distance = serializedComposer.FindProperty("CameraDistance");
                if (distance != null) distance.floatValue = 18f;
                serializedComposer.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(composer);
            }
            cameraGo.transform.rotation = Quaternion.Euler(55f, 0, 0);
        }

        var controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.speed = 8.2f;
            controller.jumpForce = 4.0f;
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }
        var interactor = player.GetComponent<PlayerInteractor>();
        if (interactor != null)
        {
            interactor.winPoints = 10;
            interactor.respawnFallLimit = -8;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.018f, 0.027f, 0.041f);
        RenderSettings.fogDensity = 0.0065f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.115f, 0.145f, 0.18f);
        RenderSettings.skybox = null;
        if (RenderSettings.sun != null)
        {
            RenderSettings.sun.color = new Color(0.4f, 0.52f, 0.72f);
            RenderSettings.sun.intensity = 0.2f;
        }

        var world = new GameObject("BOSQUE | Laberinto de la arboleda negra");
        var terrainMat = MakeMat("M_Bosque_Terreno", new Color(0.15f, 0.18f, 0.15f));
        var dirtMat = MakeMat("M_Bosque_Tierra_Mojada", new Color(0.32f, 0.25f, 0.19f));
        var wallMat = MakeMat("M_Bosque_Muro_Raices", new Color(0.105f, 0.085f, 0.071f));
        var stoneMat = MakeMat("M_Bosque_Piedra", new Color(0.14f, 0.17f, 0.18f));
        var leafMat = MakeMat("M_Bosque_Follaje", new Color(0.055f, 0.095f, 0.075f));
        var leafMatAlt = MakeMat("M_Bosque_Follaje_2", new Color(0.085f, 0.115f, 0.08f));
        var barkMat = MakeMat("M_Bosque_Tronco", new Color(0.13f, 0.085f, 0.052f));
        var bloodMat = MakeMat("M_Bosque_Carmesí", new Color(0.38f, 0.018f, 0.022f), true);
        var eyeMat = MakeMat("M_Bosque_Ojos_Monstruo", new Color(0.9f, 0.025f, 0.015f), true);
        var grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath);
        if (grassTexture != null) terrainMat.SetTexture("_BaseMap", grassTexture);
        var dirtTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DirtTexturePath);
        if (dirtTexture != null) dirtMat.SetTexture("_BaseMap", dirtTexture);
        terrainMat.SetTextureScale("_BaseMap", new Vector2(30, 30));
        dirtMat.SetTextureScale("_BaseMap", new Vector2(2, 2));
        EditorUtility.SetDirty(terrainMat);
        EditorUtility.SetDirty(dirtMat);

        int[] passages = GenerateMaze(width, height, startCell, 6202614);
        var mazeRoot = new GameObject("Laberinto | muros y ruta");
        mazeRoot.transform.SetParent(world.transform, false);
        CreateGround(world.transform, terrainMat, width * CellSize + 64f, Vector3.zero);
        CreateCorridorMesh(mazeRoot.transform, dirtMat, width, height, CellSize, cellOrigin);
        CreateWallMesh(mazeRoot.transform, wallMat, passages, width, height, CellSize, cellOrigin, startCell, exitCell);
        CreateStartGate(mazeRoot.transform, cellOrigin, startCell);

        var grid = mazeRoot.AddComponent<ForestMazeGrid>();
        grid.Configure(width, height, CellSize, cellOrigin, passages);
        var rng = new System.Random(6202614);

        var treePrefabs = new List<GameObject>();
        foreach (var path in new[] { PinePath, GnarledTreePath })
        {
            var tree = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (tree != null) treePrefabs.Add(tree);
        }
        var grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwampGrassPath);
        var rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwampRootPath);
        var stonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwampStonePath);
        var hillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeadHillPath);
        var shrubPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeadShrubPath);

        PlaceForestRim(world.transform, treePrefabs, grassPrefab, shrubPrefab, hillPrefab, half, rng, barkMat, leafMat, leafMatAlt, terrainMat);
        var mainPath = FindPath(passages, width, height, startCell, exitCell);
        var pathSet = new HashSet<Vector2Int>(mainPath);
        var pickupCells = ChoosePickupCells(passages, width, height, startCell, mainPath, pathSet, 9);
        PlaceInMazeDecor(world.transform, grid, passages, width, height, cellOrigin, CellSize, rng, treePrefabs, grassPrefab, rootPrefab, stonePrefab, shrubPrefab, pickupCells, pathSet, barkMat, leafMat, leafMatAlt, stoneMat);
        PlacePickups(grid, pickupCells, exitCell);
        PlaceJumpLogs(world.transform, grid, mainPath, barkMat);
        PlacePathLights(world.transform, grid, mainPath, bloodMat, rng);
        PlaceExitArch(world.transform, grid, exitCell, barkMat, bloodMat);
        PlaceScares(world.transform, grid, pickupCells, passages, width, height, cellOrigin, CellSize, rng, bloodMat);
        CreateMonster(world.transform, grid, player.transform, interactor, mainPath, passages, width, height, cellOrigin, barkMat, bloodMat, eyeMat, AssetDatabase.LoadAssetAtPath<AudioClip>(StingerPath));

        ConfigureBuildOrder(interactor);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        Debug.Log("BOSQUE built: 55x55 maze, 10 memories, guided exit and forest stalker. Hospital remains the next scene.");
    }

    static int[] GenerateMaze(int width, int height, Vector2Int start, int seed)
    {
        var rng = new System.Random(seed);
        var passages = new int[width * height];
        var visited = new bool[passages.Length];
        var stack = new Stack<Vector2Int>();
        visited[start.y * width + start.x] = true;
        stack.Push(start);
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var options = new List<int>(4);
            for (int d = 0; d < 4; d++)
            {
                var next = current + Directions[d];
                if (next.x >= 0 && next.x < width && next.y >= 0 && next.y < height && !visited[next.y * width + next.x]) options.Add(d);
            }
            if (options.Count == 0) { stack.Pop(); continue; }
            int direction = options[rng.Next(options.Count)];
            var neighbor = current + Directions[direction];
            int currentIndex = current.y * width + current.x;
            int neighborIndex = neighbor.y * width + neighbor.x;
            passages[currentIndex] |= Bits[direction];
            passages[neighborIndex] |= OppositeBits[direction];
            visited[neighborIndex] = true;
            stack.Push(neighbor);
        }
        int startIndex = start.y * width + start.x;
        var firstStep = start + Vector2Int.up;
        int nextIndex = firstStep.y * width + firstStep.x;
        if ((passages[startIndex] & 1) == 0)
        {
            passages[startIndex] |= 1;
            passages[nextIndex] |= 4;
        }
        return passages;
    }

    static List<Vector2Int> FindPath(int[] passages, int width, int height, Vector2Int start, Vector2Int goal)
    {
        int count = width * height;
        var parent = new int[count];
        Array.Fill(parent, -1);
        int startIndex = start.y * width + start.x;
        int goalIndex = goal.y * width + goal.x;
        var queue = new Queue<int>();
        parent[startIndex] = startIndex;
        queue.Enqueue(startIndex);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (current == goalIndex) break;
            var cell = new Vector2Int(current % width, current / width);
            for (int d = 0; d < 4; d++)
            {
                if ((passages[current] & Bits[d]) == 0) continue;
                var next = cell + Directions[d];
                int nextIndex = next.y * width + next.x;
                if (parent[nextIndex] != -1) continue;
                parent[nextIndex] = current;
                queue.Enqueue(nextIndex);
            }
        }
        var path = new List<Vector2Int>();
        if (parent[goalIndex] == -1) return path;
        for (int at = goalIndex; at != startIndex; at = parent[at]) path.Add(new Vector2Int(at % width, at / width));
        path.Add(start);
        path.Reverse();
        return path;
    }

    static List<Vector2Int> ChoosePickupCells(int[] passages, int width, int height, Vector2Int start, List<Vector2Int> mainPath, HashSet<Vector2Int> pathSet, int count)
    {
        var selected = new List<Vector2Int>();
        int[] fractions = { 7, 27, 49, 70 };
        foreach (int fraction in fractions)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt((mainPath.Count - 1) * fraction / 100f), 1, mainPath.Count - 2);
            var cell = mainPath[index];
            if (!selected.Contains(cell)) selected.Add(cell);
        }

        var distances = FindPathDistances(passages, width, height, start);
        var branches = new List<Vector2Int>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var cell = new Vector2Int(x, y);
            int degree = Degree(passages[y * width + x]);
            if (degree == 1 && distances[y * width + x] > 22 && !pathSet.Contains(cell)) branches.Add(cell);
        }
        branches.Sort((a, b) => distances[a.y * width + a.x].CompareTo(distances[b.y * width + b.x]));
        int needed = count - selected.Count;
        for (int i = 0; i < needed && branches.Count > 0; i++)
        {
            int at = Mathf.RoundToInt((i + 1f) * (branches.Count - 1) / (needed + 1f));
            var cell = branches[Mathf.Clamp(at, 0, branches.Count - 1)];
            if (!selected.Contains(cell)) selected.Add(cell);
        }
        for (int y = 0; selected.Count < count && y < height; y++)
        for (int x = 0; selected.Count < count && x < width; x++)
        {
            var cell = new Vector2Int(x, y);
            if (!pathSet.Contains(cell) && !selected.Contains(cell) && Degree(passages[y * width + x]) <= 2) selected.Add(cell);
        }
        return selected;
    }

    static int[] FindPathDistances(int[] passages, int width, int height, Vector2Int start)
    {
        var distance = new int[width * height];
        Array.Fill(distance, -1);
        var queue = new Queue<Vector2Int>();
        distance[start.y * width + start.x] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int currentIndex = current.y * width + current.x;
            for (int d = 0; d < 4; d++)
            {
                if ((passages[currentIndex] & Bits[d]) == 0) continue;
                var next = current + Directions[d];
                int index = next.y * width + next.x;
                if (distance[index] >= 0) continue;
                distance[index] = distance[currentIndex] + 1;
                queue.Enqueue(next);
            }
        }
        return distance;
    }

    static int Degree(int passage) => ((passage & 1) != 0 ? 1 : 0) + ((passage & 2) != 0 ? 1 : 0) + ((passage & 4) != 0 ? 1 : 0) + ((passage & 8) != 0 ? 1 : 0);

    static void CreateGround(Transform parent, Material mat, float size, Vector3 center)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Terreno | suelo de bosque muerto";
        ground.transform.SetParent(parent, false);
        ground.transform.position = new Vector3(center.x, -0.65f, center.z);
        ground.transform.localScale = new Vector3(size, 1f, size);
        ground.GetComponent<Renderer>().sharedMaterial = mat;
        ground.tag = "Ground";
    }

    static void CreateStartGate(Transform parent, Vector3 origin, Vector2Int start)
    {
        var center = new Vector3(origin.x + start.x * CellSize, 0, origin.z + start.y * CellSize);
        var gate = new GameObject("Entrada | límite invisible de cámara");
        gate.transform.SetParent(parent, false);
        gate.transform.position = center + Vector3.back * (CellSize * 0.5f) + Vector3.up * (WallHeight * 0.5f - 0.15f);
        var collider = gate.AddComponent<BoxCollider>();
        collider.size = new Vector3(CellSize, WallHeight, WallThickness);
    }

    static void CreateCorridorMesh(Transform parent, Material mat, int width, int height, float cellSize, Vector3 origin)
    {
        EnsureMeshesFolder();
        var vertices = new Vector3[width * height * 4];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[width * height * 6];
        int v = 0, t = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float cx = origin.x + x * cellSize;
            float cz = origin.z + y * cellSize;
            vertices[v] = new Vector3(cx - cellSize * 0.5f, -0.142f, cz - cellSize * 0.5f);
            vertices[v + 1] = new Vector3(cx + cellSize * 0.5f, -0.142f, cz - cellSize * 0.5f);
            vertices[v + 2] = new Vector3(cx + cellSize * 0.5f, -0.142f, cz + cellSize * 0.5f);
            vertices[v + 3] = new Vector3(cx - cellSize * 0.5f, -0.142f, cz + cellSize * 0.5f);
            uv[v] = new Vector2(0, 0); uv[v + 1] = new Vector2(2, 0); uv[v + 2] = new Vector2(2, 2); uv[v + 3] = new Vector2(0, 2);
            triangles[t++] = v; triangles[t++] = v + 2; triangles[t++] = v + 1;
            triangles[t++] = v; triangles[t++] = v + 3; triangles[t++] = v + 2;
            v += 4;
        }
        var mesh = new Mesh { name = "Mesh_Bosque_Pasillos", indexFormat = IndexFormat.UInt32 };
        mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateNormals();
        var path = "Assets/_RAB_Base/Meshes/Mesh_Bosque_Pasillos.asset";
        AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
        var floor = new GameObject("Camino | barro y hojas"); floor.transform.SetParent(parent, false);
        floor.AddComponent<MeshFilter>().sharedMesh = mesh;
        floor.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void CreateWallMesh(Transform parent, Material mat, int[] passages, int width, int height, float cellSize, Vector3 origin, Vector2Int start, Vector2Int exit)
    {
        EnsureMeshesFolder();
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(temp);
        var walls = new List<CombineInstance>(width * height * 2);
        float centerY = -0.15f + WallHeight * 0.5f;
        void AddWall(Vector3 position, Vector3 scale)
        {
            walls.Add(new CombineInstance { mesh = cubeMesh, transform = Matrix4x4.TRS(position, Quaternion.identity, scale) });
        }
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int cell = y * width + x;
            float cx = origin.x + x * cellSize;
            float cz = origin.z + y * cellSize;
            if (x == 0 && (passages[cell] & 8) == 0) AddWall(new Vector3(cx - cellSize * 0.5f, centerY, cz), new Vector3(WallThickness, WallHeight, cellSize + WallThickness));
            if (x < width - 1 && (passages[cell] & 2) == 0) AddWall(new Vector3(cx + cellSize * 0.5f, centerY, cz), new Vector3(WallThickness, WallHeight, cellSize + WallThickness));
            if (y == 0 && (passages[cell] & 4) == 0 && !(start.x == x && start.y == y)) AddWall(new Vector3(cx, centerY, cz - cellSize * 0.5f), new Vector3(cellSize + WallThickness, WallHeight, WallThickness));
            if (y < height - 1 && (passages[cell] & 1) == 0) AddWall(new Vector3(cx, centerY, cz + cellSize * 0.5f), new Vector3(cellSize + WallThickness, WallHeight, WallThickness));
            if (x == width - 1 && (passages[cell] & 2) == 0) AddWall(new Vector3(cx + cellSize * 0.5f, centerY, cz), new Vector3(WallThickness, WallHeight, cellSize + WallThickness));
            if (y == height - 1 && (passages[cell] & 1) == 0 && !(exit.x == x && exit.y == y)) AddWall(new Vector3(cx, centerY, cz + cellSize * 0.5f), new Vector3(cellSize + WallThickness, WallHeight, WallThickness));
        }
        var mesh = new Mesh { name = "Mesh_Bosque_Muros", indexFormat = IndexFormat.UInt32 };
        mesh.CombineMeshes(walls.ToArray(), true, true);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var path = "Assets/_RAB_Base/Meshes/Mesh_Bosque_Muros.asset";
        AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
        var wallObject = new GameObject("Muros | barrera continua del laberinto"); wallObject.transform.SetParent(parent, false);
        wallObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        wallObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
        wallObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        wallObject.isStatic = true;
    }

    static void PlaceForestRim(Transform parent, List<GameObject> trees, GameObject grassPrefab, GameObject shrubPrefab, GameObject hillPrefab, float half, System.Random rng, Material bark, Material leaves, Material leaves2, Material terrain)
    {
        if (trees.Count > 0)
        {
            float extent = half + 27f;
            for (int ring = 0; ring < 2; ring++)
            for (float at = -extent; at <= extent; at += 8.0f + (float)rng.NextDouble() * 3.2f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float d = half + 5f + ring * 11f + (float)rng.NextDouble() * 3.0f;
                    bool entryGap = (side < 0 && Mathf.Abs(at) < 13f) || (side > 0 && Mathf.Abs(at) < 13f);
                    if (!entryGap) SpawnTree(trees[rng.Next(trees.Count)], parent, new Vector3(at, 0, side * d), rng, bark, leaves, leaves2, 0.9f, 1.65f);
                    SpawnTree(trees[rng.Next(trees.Count)], parent, new Vector3(side * d, 0, at), rng, bark, leaves, leaves2, 0.9f, 1.65f);
                }
            }
        }
        for (int i = 0; i < 56; i++)
        {
            int side = rng.Next(4);
            float distance = half + 7f + (float)rng.NextDouble() * 18f;
            float along = -half - 20f + (float)rng.NextDouble() * (2 * half + 40f);
            var pos = side switch
            {
                0 => new Vector3(along, -0.08f, -distance),
                1 => new Vector3(along, -0.08f, distance),
                2 => new Vector3(-distance, -0.08f, along),
                _ => new Vector3(distance, -0.08f, along)
            };
            var prefab = i % 2 == 0 ? shrubPrefab : grassPrefab;
            SpawnProp(prefab, parent, i % 2 == 0 ? "Arbusto muerto" : "Hierba de pantano", pos, rng, i % 2 == 0 ? bark : terrain, 0.7f, 1.4f);
        }
        if (hillPrefab != null)
        {
            for (int i = 0; i < 24; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2;
                float distance = half + 27 + (float)rng.NextDouble() * 12;
                SpawnProp(hillPrefab, parent, "Loma de hierba muerta", new Vector3(Mathf.Cos(angle) * distance, -0.25f, Mathf.Sin(angle) * distance), rng, terrain, 1.1f, 2.0f);
            }
        }
    }

    static void PlaceInMazeDecor(Transform parent, ForestMazeGrid grid, int[] passages, int width, int height, Vector3 origin, float cellSize, System.Random rng, List<GameObject> trees, GameObject grass, GameObject roots, GameObject stones, GameObject shrubs, List<Vector2Int> pickupCells, HashSet<Vector2Int> mainPath, Material bark, Material leaves, Material leaves2, Material stone)
    {
        var pickupSet = new HashSet<Vector2Int>(pickupCells);
        var scareCells = new List<Vector2Int>();
        int treeCount = 0;
        int clutterCount = 0;
        var startCell = new Vector2Int(width / 2, 0);
        var exitCell = new Vector2Int(width / 2, height - 1);
        for (int y = 2; y < height - 2; y++)
        for (int x = 2; x < width - 2; x++)
        {
            var cell = new Vector2Int(x, y);
            int fromStart = Mathf.Abs(x - startCell.x) + Mathf.Abs(y - startCell.y);
            int fromExit = Mathf.Abs(x - exitCell.x) + Mathf.Abs(y - exitCell.y);
            if (fromStart < 6 || fromExit < 6) continue;
            int degree = Degree(passages[y * width + x]);
            if (degree == 1 && !mainPath.Contains(cell))
            {
                if (!pickupSet.Contains(cell) && scareCells.Count < 4 && (x * 7 + y * 11) % 9 == 0) scareCells.Add(cell);
                if (trees.Count > 0 && treeCount < 18 && fromStart > 8 && fromExit > 8 && (x * 3 + y * 5) % 31 == 0)
                {
                    float side = rng.Next(0, 2) == 0 ? -1.55f : 1.55f;
                    SpawnTree(trees[rng.Next(trees.Count)], parent, grid.CellCenter(cell) + Vector3.right * side, rng, bark, leaves, leaves2, 0.42f, 0.67f);
                    treeCount++;
                }
            }
            else if (degree == 2 && clutterCount < 65 && (x * 13 + y * 3) % 37 == 0)
            {
                float side = rng.Next(0, 2) == 0 ? -1.72f : 1.72f;
                SpawnProp(rng.Next(0, 2) == 0 ? grass : shrubs, parent, "Maleza del pasillo", grid.CellCenter(cell) + Vector3.right * side, rng, bark, 0.5f, 0.75f);
                clutterCount++;
            }
        }
        for (int i = 0; i < 44; i++)
        {
            int x = rng.Next(1, width - 1), y = rng.Next(1, height - 1);
            var cell = new Vector2Int(x, y);
            if (pickupSet.Contains(cell) || Mathf.Abs(x - width / 2) + y < 6 || Mathf.Abs(x - width / 2) + (height - 1 - y) < 6) continue;
            var prefab = i % 2 == 0 ? roots : stones;
            float side = rng.Next(0, 2) == 0 ? -1.7f : 1.7f;
            SpawnProp(prefab, parent, i % 2 == 0 ? "Raíces que muerden la tierra" : "Piedras con musgo", grid.CellCenter(cell) + Vector3.right * side, rng, stone, 0.45f, 0.7f);
        }
        var body = AssetDatabase.LoadAssetAtPath<GameObject>(DeadBodyPath);
        if (body != null)
        {
            foreach (var cell in scareCells)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(body);
                instance.name = "Cuerpo inmóvil | presagio";
                instance.transform.SetParent(parent);
                instance.transform.position = grid.CellCenter(cell) + new Vector3(-1.2f, 0, 0.75f);
                instance.transform.rotation = Quaternion.Euler(0, rng.Next(0, 360), rng.Next(-8, 9));
                instance.transform.localScale = Vector3.one * 1.1f;
                RemoveColliders(instance);
            }
        }
    }

    static void PlacePickups(ForestMazeGrid grid, List<Vector2Int> cells, Vector2Int exitCell)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
        if (prefab == null) throw new Exception("Pickup prefab not found: " + PickupPath);
        for (int i = 0; i < cells.Count; i++)
        {
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            item.name = "Recuerdo " + (i + 1).ToString("00") + " | " + (i < 4 ? "camino" : "desvío") ;
            item.transform.position = grid.CellCenter(cells[i]) + Vector3.up * 0.8f;
        }
        var final = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        final.name = "Recuerdo 10 | salida al hospital";
        final.transform.position = grid.CellCenter(exitCell) + Vector3.up * 0.8f + Vector3.forward * (CellSize * 0.5f - 1.25f);
    }

    static void PlaceJumpLogs(Transform parent, ForestMazeGrid grid, List<Vector2Int> mainPath, Material bark)
    {
        if (mainPath.Count < 40) return;
        int[] fractions = { 22, 53, 81 };
        for (int i = 0; i < fractions.Length; i++)
        {
            int at = Mathf.Clamp(Mathf.RoundToInt((mainPath.Count - 1) * fractions[i] / 100f), 3, mainPath.Count - 3);
            var a = grid.CellCenter(mainPath[at - 1]);
            var b = grid.CellCenter(mainPath[at + 1]);
            Vector3 direction = (b - a).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, direction).normalized;
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "Tronco caído | salto " + (i + 1);
            log.transform.SetParent(parent, false);
            log.transform.position = grid.CellCenter(mainPath[at]) + Vector3.up * 0.38f;
            log.transform.rotation = Quaternion.FromToRotation(Vector3.up, across);
            log.transform.localScale = new Vector3(0.34f, CellSize * 0.45f, 0.34f);
            log.GetComponent<Renderer>().sharedMaterial = bark;
            log.tag = "Untagged";
        }
    }

    static void PlacePathLights(Transform parent, ForestMazeGrid grid, List<Vector2Int> path, Material redMat, System.Random rng)
    {
        if (path.Count == 0) return;
        int count = Mathf.Clamp(path.Count / 32, 12, 28);
        for (int i = 0; i < count; i++)
        {
            int at = Mathf.Clamp(Mathf.RoundToInt((path.Count - 1) * (i + 0.5f) / count), 1, path.Count - 1);
            var center = grid.CellCenter(path[at]);
            float side = rng.Next(0, 2) == 0 ? -1.85f : 1.85f;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Marca roja | salida entre los árboles";
            marker.transform.SetParent(parent, false);
            marker.transform.position = center + Vector3.right * side + Vector3.up * 0.34f;
            marker.transform.localScale = new Vector3(0.12f, 0.56f, 0.12f);
            marker.GetComponent<Renderer>().sharedMaterial = redMat;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            var lightObj = new GameObject("Luz carmesí | orientación del bosque");
            lightObj.transform.SetParent(parent, false);
            lightObj.transform.position = marker.transform.position + Vector3.up * 0.65f;
            var light = lightObj.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(0.82f, 0.06f, 0.035f); light.intensity = 1.35f; light.range = 5.2f;
        }
        for (int i = 0; i < 15; i++)
        {
            int at = Mathf.Clamp(Mathf.RoundToInt((path.Count - 1) * (i + 0.25f) / 15f), 1, path.Count - 1);
            var lightObj = new GameObject("Luz fría | niebla y sendero");
            lightObj.transform.SetParent(parent, false);
            lightObj.transform.position = grid.CellCenter(path[at]) + new Vector3(0, 2.2f, 0);
            var light = lightObj.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(0.42f, 0.59f, 0.83f); light.intensity = 1.55f; light.range = 7.5f;
        }
    }

    static void PlaceExitArch(Transform parent, ForestMazeGrid grid, Vector2Int exit, Material bark, Material red)
    {
        var center = grid.CellCenter(exit);
        float edgeZ = center.z + CellSize * 0.5f - 0.5f;
        Cube(parent, "Salida | poste izquierdo", new Vector3(center.x - 1.7f, 1.35f, edgeZ), new Vector3(0.34f, 2.7f, 0.42f), bark, "Untagged");
        Cube(parent, "Salida | poste derecho", new Vector3(center.x + 1.7f, 1.35f, edgeZ), new Vector3(0.34f, 2.7f, 0.42f), bark, "Untagged");
        Cube(parent, "Salida | dintel marcado", new Vector3(center.x, 2.68f, edgeZ), new Vector3(3.7f, 0.35f, 0.42f), red, "Untagged");
    }

    static void PlaceScares(Transform parent, ForestMazeGrid grid, List<Vector2Int> pickupCells, int[] passages, int width, int height, Vector3 origin, float cellSize, System.Random rng, Material red)
    {
        var body = AssetDatabase.LoadAssetAtPath<GameObject>(DeadBodyPath);
        var stinger = AssetDatabase.LoadAssetAtPath<AudioClip>(StingerPath);
        if (body == null) return;
        var pickupSet = new HashSet<Vector2Int>(pickupCells);
        var placed = 0;
        for (int y = 3; y < height - 3 && placed < 3; y++)
        for (int x = 3; x < width - 3 && placed < 3; x++)
        {
            int degree = Degree(passages[y * width + x]);
            var cell = new Vector2Int(x, y);
            if (degree != 1 || pickupSet.Contains(cell) || (x + 3 * y) % 19 != 0) continue;
            var trigger = new GameObject("Susto " + (placed + 1) + " | aparición breve"); trigger.transform.SetParent(parent, false); trigger.transform.position = grid.CellCenter(cell);
            var collider = trigger.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = new Vector3(4.5f, 2.5f, 4.5f); collider.center = new Vector3(0, 1.1f, 0);
            var scare = trigger.AddComponent<ForestScareTrigger>(); scare.player = GameObject.Find("Player").transform; scare.stinger = stinger;
            var flashObj = new GameObject("Destello rojo del susto"); flashObj.transform.SetParent(trigger.transform, false); flashObj.transform.localPosition = new Vector3(0, 1.2f, 0);
            var light = flashObj.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1f, 0.04f, 0.015f); light.intensity = 7; light.range = 8; flashObj.SetActive(false); scare.flash = flashObj;
            var corpse = (GameObject)PrefabUtility.InstantiatePrefab(body); corpse.name = "Silueta que aparece"; corpse.transform.SetParent(trigger.transform, false); corpse.transform.localPosition = new Vector3(0, 0, 1.45f); corpse.transform.localRotation = Quaternion.Euler(0, 180, 0); corpse.transform.localScale = Vector3.one * 1.15f; RemoveColliders(corpse);
            var rends = corpse.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var mats = rends[i].sharedMaterials;
                var safe = new Material[mats.Length];
                for (int j = 0; j < safe.Length; j++) safe[j] = i % 2 == 0 ? red : MakeMat("M_Bosque_Piel_Cadáver", new Color(0.17f, 0.15f, 0.14f));
                rends[i].sharedMaterials = safe;
            }
            placed++;
        }
    }

    static void CreateMonster(Transform parent, ForestMazeGrid grid, Transform player, PlayerInteractor interactor, List<Vector2Int> path, int[] passages, int width, int height, Vector3 origin, Material bodyMat, Material clothMat, Material eyes, AudioClip stinger)
    {
        var root = new GameObject("BOSQUE | El caminante de la niebla");
        root.transform.SetParent(parent, false);
        root.transform.position = grid.CellCenter(new Vector2Int(width / 2, 0));
        root.tag = "Obstacle";
        var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        var capsule = root.AddComponent<CapsuleCollider>(); capsule.center = new Vector3(0, 1.0f, 0); capsule.height = 2.0f; capsule.radius = 0.42f; capsule.enabled = false;
        var visual = new GameObject("Figura | aparece tras el primer recuerdo"); visual.transform.SetParent(root.transform, false);
        AddPrimitive(visual.transform, PrimitiveType.Capsule, "Torso alargado", new Vector3(0, 1.06f, 0), new Vector3(0.48f, 1.38f, 0.36f), bodyMat);
        AddPrimitive(visual.transform, PrimitiveType.Sphere, "Cabeza sin rostro", new Vector3(0, 2.03f, 0.02f), new Vector3(0.48f, 0.57f, 0.44f), bodyMat);
        AddPrimitive(visual.transform, PrimitiveType.Capsule, "Brazo izquierdo", new Vector3(-0.48f, 1.28f, 0), new Vector3(0.19f, 0.82f, 0.2f), bodyMat, Quaternion.Euler(0, 0, -16));
        AddPrimitive(visual.transform, PrimitiveType.Capsule, "Brazo derecho", new Vector3(0.48f, 1.28f, 0), new Vector3(0.19f, 0.82f, 0.2f), bodyMat, Quaternion.Euler(0, 0, 16));
        AddPrimitive(visual.transform, PrimitiveType.Cylinder, "Pierna izquierda", new Vector3(-0.18f, 0.35f, 0), new Vector3(0.18f, 0.45f, 0.2f), clothMat);
        AddPrimitive(visual.transform, PrimitiveType.Cylinder, "Pierna derecha", new Vector3(0.18f, 0.35f, 0), new Vector3(0.18f, 0.45f, 0.2f), clothMat);
        AddPrimitive(visual.transform, PrimitiveType.Sphere, "Ojo izquierdo", new Vector3(-0.14f, 2.04f, 0.215f), new Vector3(0.095f, 0.065f, 0.045f), eyes);
        AddPrimitive(visual.transform, PrimitiveType.Sphere, "Ojo derecho", new Vector3(0.14f, 2.04f, 0.215f), new Vector3(0.095f, 0.065f, 0.045f), eyes);
        AddPrimitive(visual.transform, PrimitiveType.Cylinder, "Cuerno izquierdo", new Vector3(-0.16f, 2.35f, 0), new Vector3(0.075f, 0.25f, 0.075f), clothMat, Quaternion.Euler(0, 0, -22));
        AddPrimitive(visual.transform, PrimitiveType.Cylinder, "Cuerno derecho", new Vector3(0.16f, 2.35f, 0), new Vector3(0.075f, 0.25f, 0.075f), clothMat, Quaternion.Euler(0, 0, 22));
        visual.SetActive(false);
        var chase = root.AddComponent<ForestMonsterChase>();
        chase.grid = grid; chase.player = player; chase.playerInteractor = interactor; chase.bodyVisual = visual; chase.bodyCollider = capsule; chase.monsterBody = rb; chase.stinger = stinger; chase.spawnCell = new Vector2Int(width / 2, 0); chase.exitCell = new Vector2Int(width / 2, height - 1);
    }

    static GameObject AddPrimitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material mat, Quaternion? rotation = null)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; if (rotation.HasValue) go.transform.localRotation = rotation.Value; go.GetComponent<Renderer>().sharedMaterial = mat; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }

    static void SpawnTree(GameObject prefab, Transform parent, Vector3 position, System.Random rng, Material bark, Material leaves, Material leaves2, float minScale, float maxScale)
    {
        if (prefab == null) return;
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        tree.name = "Árbol muerto | límite del laberinto"; tree.transform.SetParent(parent); tree.transform.position = position;
        tree.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
        float scale = minScale + (float)rng.NextDouble() * (maxScale - minScale); tree.transform.localScale = Vector3.one * scale;
        foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
        {
            var source = renderer.sharedMaterials; var safe = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                string n = source[i] != null ? source[i].name.ToLowerInvariant() : "";
                safe[i] = n.Contains("leaf") || n.Contains("foliage") || n.Contains("billboard") || n.Contains("leaves") ? (rng.Next(0, 2) == 0 ? leaves : leaves2) : bark;
            }
            renderer.sharedMaterials = safe;
        }
        RemoveColliders(tree);
    }

    static void SpawnProp(GameObject prefab, Transform parent, string name, Vector3 position, System.Random rng, Material material, float minScale, float maxScale)
    {
        if (prefab == null) return;
        var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        prop.name = name; prop.transform.SetParent(parent); prop.transform.position = position; prop.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
        prop.transform.localScale = Vector3.one * (minScale + (float)rng.NextDouble() * (maxScale - minScale));
        SetAllMaterials(prop, material); RemoveColliders(prop);
    }

    static void RemoveColliders(GameObject root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
    }

    static Material MakeMat(string name, Color color, bool emission = false)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
        string path = "Assets/_RAB_Base/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader; mat.color = color;
        if (emission) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * 1.6f); }
        EditorUtility.SetDirty(mat); return mat;
    }

    static void SetAllMaterials(GameObject instance, Material material)
    {
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var source = renderer.sharedMaterials; var safe = new Material[source.Length]; for (int i = 0; i < safe.Length; i++) safe[i] = material; renderer.sharedMaterials = safe;
        }
    }

    static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, string tag)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent); go.transform.position = pos; go.transform.localScale = scale; go.tag = tag; go.GetComponent<Renderer>().sharedMaterial = mat; return go;
    }

    static void EnsureMeshesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_RAB_Base/Meshes"))
            AssetDatabase.CreateFolder("Assets/_RAB_Base", "Meshes");
    }

    static void ConfigureBuildOrder(PlayerInteractor interactor)
    {
        var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        const string hospitalPath = "Assets/_RAB_Base/Scenes/SCN_LVL_Alejandro.unity";
        const string victoryPath = "Assets/_RAB_Base/Scenes/SCN_Victory.unity";
        bool bosqueEnabled = true;
        int bosqueIndex = build.FindIndex(s => s.path == ScenePath);
        if (bosqueIndex >= 0) { bosqueEnabled = build[bosqueIndex].enabled; build.RemoveAt(bosqueIndex); }
        int victoryIndex = build.FindIndex(s => s.path == victoryPath);
        if (victoryIndex < 0) build.Add(new EditorBuildSettingsScene(ScenePath, bosqueEnabled)); else build.Insert(victoryIndex, new EditorBuildSettingsScene(ScenePath, bosqueEnabled));
        bool hospitalEnabled = true; int hospitalIndex = build.FindIndex(s => s.path == hospitalPath);
        if (hospitalIndex >= 0) { hospitalEnabled = build[hospitalIndex].enabled; build.RemoveAt(hospitalIndex); }
        bosqueIndex = build.FindIndex(s => s.path == ScenePath);
        build.Insert(bosqueIndex + 1, new EditorBuildSettingsScene(hospitalPath, hospitalEnabled));
        if (interactor != null) { interactor.sceneToLoad = bosqueIndex + 1; EditorUtility.SetDirty(interactor); PrefabUtility.RecordPrefabInstancePropertyModifications(interactor); }
        EditorBuildSettings.scenes = build.ToArray();
    }
}
