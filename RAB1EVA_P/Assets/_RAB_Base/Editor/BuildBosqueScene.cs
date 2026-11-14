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
    const string TreePath = "Assets/TreeGenerator/Trees/Prefab/Prefab_Tree_Unity.prefab";
    const string GrassTexturePath = "Assets/_RAB_Base/Prefabs/CH_assets/Halloween_Graveyard_Kit_Godot/textures/Ground/Tex_Grass_BaseColor.tga";

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
        RenderSettings.fogDensity = 0.018f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.16f, 0.22f);
        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        if (RenderSettings.sun != null) { RenderSettings.sun.color = new Color(0.52f, 0.64f, 0.82f); RenderSettings.sun.intensity = 0.32f; }

        var world = new GameObject("BOSQUE | Sendero guiado");
        var groundMat = MakeMat("M_Bosque_Suelo", new Color(0.15f, 0.105f, 0.075f));
        var terrainMat = MakeMat("M_Bosque_Terreno", new Color(0.48f, 0.48f, 0.35f));
        var grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath);
        if (grassTexture != null)
        {
            terrainMat.SetTexture("_BaseMap", grassTexture);
            terrainMat.SetTextureScale("_BaseMap", new Vector2(14f, 14f));
            EditorUtility.SetDirty(terrainMat);
        }
        var edgeMat = MakeMat("M_Bosque_Borde", new Color(0.16f, 0.20f, 0.22f));
        var barkMat = MakeMat("M_Bosque_Tronco", new Color(0.13f, 0.095f, 0.075f));
        var foliageMat = MakeMat("M_Bosque_Follaje", new Color(0.055f, 0.105f, 0.105f));
        var redMat = MakeMat("M_Bosque_Rojo", new Color(0.55f, 0.045f, 0.035f), true);

        Cube(world.transform, "Terreno | hierba oscura y quemada", new Vector3(0, -0.65f, 20), new Vector3(42f, 1f, 60f), terrainMat, "Ground");
        Cube(world.transform, "Camino | barro oscuro", new Vector3(0, -0.25f, 20), new Vector3(7.2f, 0.5f, 48), groundMat, "Ground");
        Cube(world.transform, "Límite izquierdo", new Vector3(-4.2f, 0.15f, 20), new Vector3(0.8f, 1.5f, 48), edgeMat, "Ground");
        Cube(world.transform, "Límite derecho", new Vector3(4.2f, 0.15f, 20), new Vector3(0.8f, 1.5f, 48), edgeMat, "Ground");

        // Dense forest wall of dead pines, with the imported Unity tree as a secondary silhouette.
        var pinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PinePath);
        var treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePath);
        var rng = new System.Random(62026);
        for (int z = -4; z <= 46; z += 3)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (7.0f + (float)rng.NextDouble() * 4.0f);
                float zz = z + (float)rng.NextDouble() * 2.0f;
                var chosen = (z % 2 == 0 && pinePrefab != null) ? pinePrefab : treePrefab;
                if (chosen != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(chosen);
                    instance.name = chosen == pinePrefab ? "Pino seco | bosque" : "Árbol sombrío";
                    instance.transform.SetParent(world.transform);
                    instance.transform.position = new Vector3(x, 0, zz);
                    instance.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                    float scale = chosen == pinePrefab ? 0.72f + (float)rng.NextDouble() * 0.24f : 0.78f + (float)rng.NextDouble() * 0.25f;
                    instance.transform.localScale = Vector3.one * scale;
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        var sourceMaterials = renderer.sharedMaterials;
                        var safeMaterials = new Material[sourceMaterials.Length];
                        for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
                        {
                            string materialName = sourceMaterials[materialIndex] != null ? sourceMaterials[materialIndex].name.ToLowerInvariant() : "";
                            safeMaterials[materialIndex] = materialName.Contains("leaf") || materialName.Contains("foliage") || materialName.Contains("billboard") ? foliageMat : barkMat;
                        }
                        renderer.sharedMaterials = safeMaterials;
                    }
                }
                // Dark understory closes gaps and keeps the playable route visually legible.
                for (int k = 0; k < 2; k++)
                {
                    float bx = side * (4.7f + (float)rng.NextDouble() * 1.0f);
                    var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bush.name = "Matorral oscuro"; bush.transform.SetParent(world.transform);
                    bush.transform.position = new Vector3(bx, 0.4f, zz + k * 1.4f);
                    bush.transform.localScale = new Vector3(1.4f, 1.2f, 1.8f);
                    bush.GetComponent<Renderer>().sharedMaterial = groundMat;
                    UnityEngine.Object.DestroyImmediate(bush.GetComponent<Collider>());
                }
            }
        }

        var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
        if (pickupPrefab != null)
        {
            foreach (float z in new[] { 5f, 13f, 22f, 31f, 40f })
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

        for (int i = 0; i < 4; i++)
        {
            float z = 7 + i * 11;
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
        for (int i = 0; i < 6; i++)
        {
            var lightGo = new GameObject("Luz fría | sendero"); lightGo.transform.SetParent(world.transform);
            lightGo.transform.position = new Vector3(0, 2.4f, 3 + i * 7.2f);
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point;
            light.color = new Color(0.48f, 0.67f, 1.0f); light.intensity = 3.0f; light.range = 8.0f;
        }

        // No free exploration: the trail terminates at a visible exit gate.
        Cube(world.transform, "Salida del bosque", new Vector3(0, 1.2f, 44.5f), new Vector3(5.8f, 2.4f, 0.35f), edgeMat, "Ground");
        Cube(world.transform, "Hueco de salida", new Vector3(0, 1.1f, 44.3f), new Vector3(2.1f, 2.1f, 0.5f), groundMat, "Ground");

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

    static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, string tag)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent);
        go.transform.position = pos; go.transform.localScale = scale; go.tag = tag;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}
