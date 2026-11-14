using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

    [MenuItem("Tools/Roll of Fear/Build BOSQUE Level")]
    public static void Build()
    {
        var source = EditorSceneManager.OpenScene("Assets/_RAB_Base/Scenes/SCN_LVL01.unity", OpenSceneMode.Single);
        var roots = source.GetRootGameObjects();
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "AudioManager", "PlayerCam", "Main Camera", "RespawnPoint", "Directional Light", "Global Volume", "Canvas", "EventSystem", "Points", "Player" };
        foreach (var go in roots)
            if (!keep.Contains(go.name)) UnityEngine.Object.DestroyImmediate(go);

        var player = GameObject.Find("Player");
        if (player == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (prefab == null) throw new Exception("Player prefab not found: " + PlayerPath);
            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
        player.name = "Player";
        player.transform.position = new Vector3(0, 0.55f, 0);

        // Keep the familiar camera angle, but make its target travel only along the straight forest path.
        var cameraGo = GameObject.Find("PlayerCam");
        var cameraComponent = cameraGo != null ? cameraGo.GetComponent("CinemachineCamera") : null;
        if (cameraGo != null && cameraComponent != null)
        {
            var followTarget = new GameObject("BOSQUE | Objetivo de cámara (altura fija)");
            var follow = followTarget.AddComponent<BosqueCameraFollow>();
            follow.target = player.transform;
            follow.fixedX = 0f;
            follow.fixedY = 0.55f;
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
        }

        var controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.jumpForce = 4.5f;
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }

        var respawn = GameObject.Find("RespawnPoint");
        if (respawn != null) respawn.transform.position = new Vector3(0, 0.55f, 0);
        var interactor = player.GetComponent<PlayerInteractor>();
        if (interactor != null) { interactor.winPoints = 5; interactor.respawnFallLimit = -8; }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.035f, 0.055f, 0.085f);
        RenderSettings.fogDensity = 0.013f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.21f, 0.23f);
        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        if (RenderSettings.sun != null) { RenderSettings.sun.color = new Color(0.52f, 0.64f, 0.82f); RenderSettings.sun.intensity = 0.32f; }

        var world = new GameObject("BOSQUE | Sendero guiado");
        var groundMat = MakeMat("M_Bosque_Suelo", new Color(0.34f, 0.25f, 0.17f));
        var terrainMat = MakeMat("M_Bosque_Terreno", new Color(0.43f, 0.43f, 0.31f));
        var undergrowthMat = MakeMat("M_Bosque_Maleza", new Color(0.18f, 0.24f, 0.15f));
        var stoneMat = MakeMat("M_Bosque_Piedra", new Color(0.18f, 0.21f, 0.23f));
        var leafMat = MakeMat("M_Bosque_Follaje", new Color(0.095f, 0.15f, 0.12f));
        var leafMatAlt = MakeMat("M_Bosque_Follaje_2", new Color(0.14f, 0.18f, 0.13f));
        var barkMat = MakeMat("M_Bosque_Tronco", new Color(0.18f, 0.12f, 0.075f));
        var grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath);
        if (grassTexture != null)
        {
            terrainMat.SetTexture("_BaseMap", grassTexture);
            terrainMat.SetTextureScale("_BaseMap", new Vector2(14f, 14f));
            EditorUtility.SetDirty(terrainMat);
        }
        var dirtTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DirtTexturePath);
        if (dirtTexture != null)
        {
            groundMat.SetTexture("_BaseMap", dirtTexture);
            groundMat.SetTextureScale("_BaseMap", new Vector2(2.0f, 18f));
            EditorUtility.SetDirty(groundMat);
        }
        var edgeMat = MakeMat("M_Bosque_Borde", new Color(0.16f, 0.20f, 0.22f));
        var redMat = MakeMat("M_Bosque_Rojo", new Color(0.55f, 0.045f, 0.035f), true);

        const float trailEnd = 66f;
        const float trailCenter = 32f;
        Cube(world.transform, "Terreno | hierba oscura y quemada", new Vector3(0, -0.65f, trailCenter), new Vector3(54f, 1f, 82f), terrainMat, "Ground");
        Cube(world.transform, "Camino | tierra húmeda", new Vector3(0, -0.25f, trailCenter), new Vector3(7.2f, 0.5f, 68f), groundMat, "Ground");
        Cube(world.transform, "Límite izquierdo", new Vector3(-4.2f, 0.15f, trailCenter), new Vector3(0.8f, 1.5f, 68f), edgeMat, "Ground");
        Cube(world.transform, "Límite derecho", new Vector3(4.2f, 0.15f, trailCenter), new Vector3(0.8f, 1.5f, 68f), edgeMat, "Ground");

        // Layered tree lines use three already imported low-poly tree silhouettes.
        var treePrefabs = new List<GameObject>();
        foreach (var path in new[] { PinePath, GnarledTreePath, TreePath })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) treePrefabs.Add(prefab);
        }
        var grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwampGrassPath);
        var rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwampRootPath);
        var stonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwampStonePath);
        var hillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeadHillPath);
        var shrubPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeadShrubPath);
        var rng = new System.Random(62026);
        int row = 0;
        for (float z = -6f; z <= trailEnd + 2f; z += 2.8f, row++)
        {
            int sideIndex = 0;
            for (int side = -1; side <= 1; side += 2, sideIndex++)
            {
                float x = side * (7.2f + (float)rng.NextDouble() * 3.8f);
                float zz = z + (float)rng.NextDouble() * 1.8f;
                if (treePrefabs.Count > 0)
                {
                    var chosen = treePrefabs[(row + sideIndex) % treePrefabs.Count];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(chosen);
                    instance.name = "Árbol de ribera | " + chosen.name;
                    Place(instance, world.transform, new Vector3(x, 0, zz), Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0), Vector3.one * (0.78f + (float)rng.NextDouble() * 0.38f));
                    SetTreeMaterials(instance, barkMat, (row + sideIndex) % 2 == 0 ? leafMat : leafMatAlt);
                }

                // An outer, less regular row gives the forest a deeper horizon.
                if (row % 2 == 0 && treePrefabs.Count > 0)
                {
                    var outer = (GameObject)PrefabUtility.InstantiatePrefab(treePrefabs[(row + sideIndex + 1) % treePrefabs.Count]);
                    outer.name = "Árbol lejano | bosque";
                    Place(outer, world.transform, new Vector3(side * (14f + (float)rng.NextDouble() * 6f), 0, zz + (float)rng.NextDouble() * 2.8f), Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0), Vector3.one * (0.8f + (float)rng.NextDouble() * 0.45f));
                    SetTreeMaterials(outer, barkMat, (row + sideIndex) % 2 == 0 ? leafMatAlt : leafMat);
                }

                var undergrowth = (row % 3 == 0) ? shrubPrefab : grassPrefab;
                if (undergrowth != null)
                {
                    var plant = (GameObject)PrefabUtility.InstantiatePrefab(undergrowth);
                    plant.name = row % 3 == 0 ? "Arbusto seco" : "Hierba de ciénaga";
                    Place(plant, world.transform, new Vector3(side * (4.9f + (float)rng.NextDouble() * 2.0f), 0, zz + (float)rng.NextDouble() * 1.7f), Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0), Vector3.one * (0.75f + (float)rng.NextDouble() * 0.65f));
                    SetAllMaterials(plant, undergrowthMat);
                }
            }
        }

        // Stones, exposed roots and dead grass mounds break up the flat ground plane.
        for (int i = 0; i < 54; i++)
        {
            int side = rng.Next(0, 2) == 0 ? -1 : 1;
            float x = side * (5.3f + (float)rng.NextDouble() * 14.0f);
            float z = -5f + (float)rng.NextDouble() * (trailEnd + 10f);
            var detailPrefab = i % 3 == 0 ? rootPrefab : stonePrefab;
            if (detailPrefab != null)
            {
                var detail = (GameObject)PrefabUtility.InstantiatePrefab(detailPrefab);
                detail.name = i % 3 == 0 ? "Raíces expuestas" : "Piedras cubiertas de musgo";
                Place(detail, world.transform, new Vector3(x, -0.05f, z), Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0), Vector3.one * (0.55f + (float)rng.NextDouble() * 1.1f));
                SetAllMaterials(detail, i % 3 == 0 ? barkMat : stoneMat);
            }
        }
        if (hillPrefab != null)
        {
            for (int i = 0; i < 12; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                var hill = (GameObject)PrefabUtility.InstantiatePrefab(hillPrefab);
                hill.name = "Loma de hierba muerta";
                Place(hill, world.transform, new Vector3(side * (17f + (float)rng.NextDouble() * 4f), -0.2f, -3f + i / 2 * 12f + (float)rng.NextDouble() * 4f), Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0), new Vector3(2.0f + (float)rng.NextDouble() * 2f, 1.0f + (float)rng.NextDouble() * 0.9f, 2.0f + (float)rng.NextDouble() * 2f));
                SetAllMaterials(hill, terrainMat);
            }
        }

        var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
        if (pickupPrefab != null)
        {
            foreach (float z in new[] { 7f, 20f, 33f, 46f, 59f })
            {
                var pickup = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab);
                pickup.name = "Recuerdo | pickup"; pickup.transform.position = new Vector3(0, 0.9f, z);
            }
        }

        // A low, clearly visible fallen log: it is a physical jump obstacle, never a respawn trigger.
        var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        log.name = "Tronco caído | salta"; log.transform.SetParent(world.transform);
        log.transform.position = new Vector3(0, 0.45f, 17.5f);
        log.transform.rotation = Quaternion.Euler(0, 0, 90);
        log.transform.localScale = new Vector3(0.48f, 2.5f, 0.48f);
        log.GetComponent<Renderer>().sharedMaterial = barkMat;
        log.tag = "Untagged";

        // A second low branch makes the longer path feel paced without changing its guided layout.
        var branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        branch.name = "Rama caída | segundo salto"; branch.transform.SetParent(world.transform);
        branch.transform.position = new Vector3(0, 0.32f, 45.5f);
        branch.transform.rotation = Quaternion.Euler(0, 0, 90);
        branch.transform.localScale = new Vector3(0.32f, 2.35f, 0.32f);
        branch.GetComponent<Renderer>().sharedMaterial = barkMat;
        branch.tag = "Untagged";

        for (int i = 0; i < 6; i++)
        {
            float z = 8 + i * 11.2f;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Farol rojo tenue"; marker.transform.SetParent(world.transform);
            marker.transform.position = new Vector3(i % 2 == 0 ? -3.45f : 3.45f, 0.18f, z);
            marker.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
            marker.GetComponent<Renderer>().sharedMaterial = redMat;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            var lightGo = new GameObject("Resplandor rojo"); lightGo.transform.SetParent(world.transform);
            lightGo.transform.position = marker.transform.position + Vector3.up * 0.5f;
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(0.8f, 0.06f, 0.035f); light.intensity = 2.2f; light.range = 4.5f;
        }

        // Cool pools of light keep the ball and guided path readable through the fog.
        for (int i = 0; i < 8; i++)
        {
            var lightGo = new GameObject("Luz fría | sendero"); lightGo.transform.SetParent(world.transform);
            lightGo.transform.position = new Vector3(0, 2.4f, 3 + i * 8.4f);
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point;
            light.color = new Color(0.48f, 0.67f, 1.0f); light.intensity = 3.0f; light.range = 8.0f;
        }

        // Timber arch gives the guided trail a clear end while leaving its center open.
        Cube(world.transform, "Portal final | pilar izquierdo", new Vector3(-2.75f, 1.45f, 64.3f), new Vector3(0.45f, 2.9f, 0.55f), barkMat, "Ground");
        Cube(world.transform, "Portal final | pilar derecho", new Vector3(2.75f, 1.45f, 64.3f), new Vector3(0.45f, 2.9f, 0.55f), barkMat, "Ground");
        Cube(world.transform, "Portal final | dintel", new Vector3(0, 2.9f, 64.3f), new Vector3(5.95f, 0.48f, 0.58f), barkMat, "Ground");

        var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool bosqueEnabled = true;
        int existingBosqueIndex = build.FindIndex(s => s.path == ScenePath);
        if (existingBosqueIndex >= 0) { bosqueEnabled = build[existingBosqueIndex].enabled; build.RemoveAt(existingBosqueIndex); }
        const string hospitalPath = "Assets/_RAB_Base/Scenes/SCN_LVL_Alejandro.unity";
        const string victoryPath = "Assets/_RAB_Base/Scenes/SCN_Victory.unity";
        int victoryIndex = build.FindIndex(s => s.path == victoryPath);
        if (victoryIndex < 0) build.Add(new EditorBuildSettingsScene(ScenePath, bosqueEnabled));
        else build.Insert(victoryIndex, new EditorBuildSettingsScene(ScenePath, bosqueEnabled));
        bool hospitalEnabled = true;
        int hospitalIndex = build.FindIndex(s => s.path == hospitalPath);
        if (hospitalIndex >= 0)
        {
            hospitalEnabled = build[hospitalIndex].enabled;
            build.RemoveAt(hospitalIndex);
        }
        int bosqueIndex = build.FindIndex(s => s.path == ScenePath);
        build.Insert(bosqueIndex + 1, new EditorBuildSettingsScene(hospitalPath, hospitalEnabled));
        if (interactor != null)
        {
            int hospitalBuildIndex = build.FindIndex(s => s.path == hospitalPath);
            interactor.sceneToLoad = hospitalBuildIndex >= 0 ? hospitalBuildIndex : build.Count - 1;
            EditorUtility.SetDirty(interactor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(interactor);
        }
        EditorBuildSettings.scenes = build.ToArray();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        Debug.Log("BOSQUE built; completion now loads the hospital: " + hospitalPath);
    }

    static Material MakeMat(string name, Color color, bool emission = false)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        string path = "Assets/_RAB_Base/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.color = color;
        if (emission) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * 1.8f); }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void Place(GameObject instance, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        instance.transform.SetParent(parent);
        instance.transform.position = position;
        instance.transform.rotation = rotation;
        instance.transform.localScale = scale;
    }

    static void SetTreeMaterials(GameObject tree, Material bark, Material foliage)
    {
        foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
        {
            var source = renderer.sharedMaterials;
            var safe = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                string materialName = source[i] != null ? source[i].name.ToLowerInvariant() : "";
                safe[i] = materialName.Contains("leaf") || materialName.Contains("foliage") || materialName.Contains("billboard") || materialName.Contains("leaves") ? foliage : bark;
            }
            renderer.sharedMaterials = safe;
        }
    }

    static void SetAllMaterials(GameObject instance, Material material)
    {
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var source = renderer.sharedMaterials;
            var safe = new Material[source.Length];
            for (int i = 0; i < safe.Length; i++) safe[i] = material;
            renderer.sharedMaterials = safe;
        }
    }

    static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, string tag)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent);
        go.transform.position = pos; go.transform.localScale = scale; go.tag = tag;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}
