/* -------------------------------------------------------------------------
 * Copyright © 2026Dave Berg. All rights reserved.
 * 
 * File: TreeGeneratorImporter.cs
 * Description: Handles the parsing and importing of Tree Generator .gltf/.glb files.
 * 
 * ------------------------------------------------------------------------- */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using GLTFast;
using GLTFast.Logging;
using Newtonsoft.Json.Linq; 
using Debug = UnityEngine.Debug;

public class TreeGeneratorImporter : EditorWindow
{
    // --- NEW: Pipeline Enum ---
    public enum RenderPipelineType
    {
        URP,
        BuiltIn,
        HD
    }

    private RenderPipelineType selectedPipeline = RenderPipelineType.BuiltIn;

    private bool extractTextures = true;
    private bool generateColliders = true;
    private bool setupLods = true;
    private bool useBillboardLod = true;
    private bool useWind = true;
    private bool useGrow = true;
    private bool useLod0OnlyForGrowth = true;
    private bool logWindDiagnostics = false;
    private bool verboseImporterLogs = false;
    
    // Organization settings
    private bool useSubfolders = true;
    private string targetImportFolder = "Assets/TreeGenerator/Trees";

    // DEBUG/OVERRIDE: per-axis multiplier applied to imported collision position + rotation (xyz only).
    [System.NonSerialized]
    private Vector3 collisionAxisMultiplier = new Vector3(1f, 1f, -1f);

    [MenuItem("TreeGenerator/Import Tree GLTF/GLB")]
    public static void ShowWindow()
    {
        GetWindow<TreeGeneratorImporter>("Tree Gen Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Tree Generator Import", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // --- NEW: Pipeline Selection UI ---
        GUILayout.Label("Render Pipeline", EditorStyles.boldLabel);
        selectedPipeline = (RenderPipelineType)EditorGUILayout.EnumPopup("Target Pipeline", selectedPipeline);
        EditorGUILayout.HelpBox($"Importer will search for shader: {GetShaderName(selectedPipeline)}", MessageType.Info);
        EditorGUILayout.Space();
        // ----------------------------------

        UnityEngine.Object selectedObj = Selection.activeObject;
        string selectedPath = selectedObj ? AssetDatabase.GetAssetPath(selectedObj) : "";
        string lowerSelectedPath = selectedPath.ToLower();
        bool isValidFile = !string.IsNullOrEmpty(selectedPath) && (lowerSelectedPath.EndsWith(".gltf") || lowerSelectedPath.EndsWith(".glb"));

        GUI.enabled = false; 
        EditorGUILayout.TextField(isValidFile ? selectedObj.name : "(Select a .gltf or .glb file in Project)");
        GUI.enabled = true;

        EditorGUILayout.Space();
        GUILayout.Label("Settings", EditorStyles.boldLabel);
        extractTextures = EditorGUILayout.Toggle("Extract Textures", extractTextures);
        generateColliders = EditorGUILayout.Toggle("Generate Colliders", generateColliders);
        setupLods = EditorGUILayout.Toggle("Setup LOD Group", setupLods);
        useBillboardLod = EditorGUILayout.Toggle("Use Billboard LOD", useBillboardLod);
        useWind = EditorGUILayout.Toggle("Use Wind", useWind);
        useGrow = EditorGUILayout.Toggle("Use Growth", useGrow);
            if (useGrow)
            {
                useLod0OnlyForGrowth = EditorGUILayout.Toggle("Growth Uses LOD0 Only", useLod0OnlyForGrowth);
            }
            logWindDiagnostics = EditorGUILayout.Toggle("Log Wind Diagnostics", logWindDiagnostics);
            verboseImporterLogs = EditorGUILayout.Toggle("Verbose Import Logs", verboseImporterLogs);

        EditorGUILayout.Space();
        GUILayout.Label("Organization", EditorStyles.boldLabel);
        useSubfolders = EditorGUILayout.Toggle("Use Subfolders", useSubfolders);
        
        if (useSubfolders)
        {
            targetImportFolder = EditorGUILayout.TextField("Import Folder", targetImportFolder);
        }

        EditorGUILayout.Space();
        GUI.enabled = isValidFile; 
        if (GUILayout.Button("Process Selected File", GUILayout.Height(30)))
        {
            _ = ImportTree(selectedPath);
        }
        GUI.enabled = true;
    }

    // Helper to get shader name based on selection
    // Updated to look for the "_Complex" variants
    private string GetShaderName(RenderPipelineType pipeline)
    {
        switch (pipeline)
        {
            case RenderPipelineType.BuiltIn: return "TreeGenerator/BuiltIn_Wind";
            case RenderPipelineType.HD:      return "TreeGenerator/HD_Wind";
            case RenderPipelineType.URP:     
            default:                         return "TreeGenerator/URP_Wind";
        }
    }

    private void VerboseLog(string message)
    {
        if (verboseImporterLogs) Debug.Log(message);
    }

    private string NormalizeAssetPath(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace("\\", "/");
    }

    private void EnsureAssetFolder(string folder)
    {
        folder = NormalizeAssetPath(folder)?.TrimEnd('/');
        if (string.IsNullOrEmpty(folder)) return;

        if (!folder.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return;
        }

        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = NormalizeAssetPath(Path.GetDirectoryName(folder));
        string name = Path.GetFileName(folder);
        EnsureAssetFolder(parent);

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private JObject LoadGltfJson(string gltfPath, out byte[] glbBinChunk)
    {
        glbBinChunk = null;

        using (FileStream fs = File.OpenRead(gltfPath))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            if (fs.Length >= 4)
            {
                uint magic = reader.ReadUInt32();
                fs.Position = 0;

                if (magic == 0x46546C67)
                {
                    reader.ReadUInt32(); // magic
                    uint version = reader.ReadUInt32();
                    uint totalLength = reader.ReadUInt32();

                    if (version != 2)
                        throw new InvalidDataException($"Unsupported GLB version {version}.");

                    string jsonContent = null;

                    while (fs.Position + 8 <= fs.Length && fs.Position < totalLength)
                    {
                        uint chunkLength = reader.ReadUInt32();
                        uint chunkType = reader.ReadUInt32();
                        byte[] chunkData = reader.ReadBytes((int)chunkLength);

                        if (chunkType == 0x4E4F534A)
                        {
                            jsonContent = Encoding.UTF8.GetString(chunkData).TrimEnd('\0', ' ', '\r', '\n', '\t');
                        }
                        else if (chunkType == 0x004E4942)
                        {
                            glbBinChunk = chunkData;
                        }
                    }

                    if (string.IsNullOrEmpty(jsonContent))
                        throw new InvalidDataException("GLB does not contain a JSON chunk.");

                    return JObject.Parse(jsonContent);
                }
            }
        }

        string textJson = File.ReadAllText(gltfPath);
        return JObject.Parse(textJson);
    }

    private async Task ImportTree(string gltfPath)
    {
        try 
        {
            Stopwatch totalTimer = Stopwatch.StartNew();
            Debug.Log($"[TreeGen] Starting import for {gltfPath} using {selectedPipeline} pipeline...");
            
            string filename = Path.GetFileNameWithoutExtension(gltfPath);
            string originalDirectory = Path.GetDirectoryName(gltfPath);

            // --- FOLDER SETUP ---
            string finalPrefabFolder, finalMeshFolder, finalMatFolder, finalTexFolder, finalGltfFolder;
            
            if (useSubfolders)
            {
                EnsureAssetFolder(targetImportFolder);

                finalPrefabFolder = Path.Combine(targetImportFolder, "Prefab");
                finalMeshFolder = Path.Combine(targetImportFolder, "Mesh");
                finalMatFolder = Path.Combine(targetImportFolder, "Material");
                finalTexFolder = Path.Combine(targetImportFolder, "Textures");
                finalGltfFolder = Path.Combine(targetImportFolder, "Gltf");

                EnsureAssetFolder(finalPrefabFolder);
                EnsureAssetFolder(finalMeshFolder);
                EnsureAssetFolder(finalMatFolder);
                EnsureAssetFolder(finalTexFolder);
                EnsureAssetFolder(finalGltfFolder);
            }
            else
            {
                string assetFolder = Path.Combine(originalDirectory, filename + "_Assets");
                EnsureAssetFolder(assetFolder);
                
                finalPrefabFolder = originalDirectory;
                finalMeshFolder = assetFolder;
                finalMatFolder = assetFolder;
                finalTexFolder = assetFolder;
                finalGltfFolder = ""; 
            }

            finalPrefabFolder = NormalizeAssetPath(finalPrefabFolder);
            finalMeshFolder = NormalizeAssetPath(finalMeshFolder);
            finalMatFolder = NormalizeAssetPath(finalMatFolder);
            finalTexFolder = NormalizeAssetPath(finalTexFolder);
            finalGltfFolder = NormalizeAssetPath(finalGltfFolder);
            Debug.Log($"[TreeGen] Timing folder setup: {totalTimer.Elapsed.TotalSeconds:F2}s");

            // 1. JSON Parsing
            Stopwatch stepTimer = Stopwatch.StartNew();
            byte[] glbBinChunk;
            JObject gltfJson = LoadGltfJson(gltfPath, out glbBinChunk);
            Debug.Log($"[TreeGen] Timing JSON/GLB metadata parse: {stepTimer.Elapsed.TotalSeconds:F2}s");

            // 2. Load GLTF
            var settings = new ImportSettings { GenerateMipMaps = true, AnisotropicFilterLevel = 3, NodeNameMethod = NameImportMethod.Original };
            var logger = new ConsoleLogger();
            var deferAgent = new UninterruptedDeferAgent(); 
            string absolutePath = Path.GetFullPath(gltfPath);
            var url = new System.Uri(absolutePath);
            byte[] gltfBytes = File.ReadAllBytes(gltfPath);

            using (var gltfImport = new GltfImport(null, deferAgent, null, logger))
            {
                stepTimer.Restart();
                Debug.Log($"[TreeGen] Starting glTFast byte load: {gltfBytes.Length} bytes from {url.AbsoluteUri}");
                bool success = await gltfImport.Load(gltfBytes, url, settings);
                if (!success) { Debug.LogError("GLTFast Load Failed"); return; }
                Debug.Log($"[TreeGen] Timing glTFast load: {stepTimer.Elapsed.TotalSeconds:F2}s");
                
                // 3. Extract Resources
                Dictionary<int, Texture2D> indexToTex = new Dictionary<int, Texture2D>();
                Dictionary<string, Texture2D> nameToTex = new Dictionary<string, Texture2D>();
                
                if (extractTextures)
                {
                    stepTimer.Restart();
                    ExtractTextures(gltfImport, gltfJson, glbBinChunk, finalTexFolder, filename, ref indexToTex, ref nameToTex);
                    Debug.Log($"[TreeGen] Timing texture extraction: {stepTimer.Elapsed.TotalSeconds:F2}s");
                }

                // 4. Create Materials (Pass the selected pipeline)
                Dictionary<string, Material> createdMaterials = new Dictionary<string, Material>();
                stepTimer.Restart();
                CreateMaterials(gltfJson, finalMatFolder, filename, indexToTex, nameToTex, ref createdMaterials);
                Debug.Log($"[TreeGen] Timing material creation: {stepTimer.Elapsed.TotalSeconds:F2}s");

                // 5. Instantiate & Fix Meshes
                GameObject rootObject = new GameObject(filename);
                var instantiator = new GameObjectInstantiator(gltfImport, rootObject.transform, logger);
                
                stepTimer.Restart();
                if (await gltfImport.InstantiateMainSceneAsync(instantiator))
                {
                    Debug.Log($"[TreeGen] Timing scene instantiate: {stepTimer.Elapsed.TotalSeconds:F2}s");
                    stepTimer.Restart();
                    SaveMeshes(rootObject, finalMeshFolder, filename);
                    Debug.Log($"[TreeGen] Timing mesh asset save: {stepTimer.Elapsed.TotalSeconds:F2}s");
                    stepTimer.Restart();
                    ApplyExtraGrowthUVs(rootObject, gltfJson, glbBinChunk);
                    Debug.Log($"[TreeGen] Timing growth/wind UV remap: {stepTimer.Elapsed.TotalSeconds:F2}s");
                    stepTimer.Restart();
                    ApplyMaterials(rootObject, createdMaterials);
                    Debug.Log($"[TreeGen] Timing material assignment: {stepTimer.Elapsed.TotalSeconds:F2}s");
                    
                    stepTimer.Restart();
                    if (setupLods) SetupLODGroup(rootObject);
                    if (generateColliders) GenerateColliders(rootObject, gltfJson);
                    Debug.Log($"[TreeGen] Timing LOD/collider setup: {stepTimer.Elapsed.TotalSeconds:F2}s");

                    string prefabName = useSubfolders ? $"Prefab_{filename}.prefab" : $"{filename}.prefab";
                    string prefabPath = NormalizeAssetPath(Path.Combine(finalPrefabFolder, prefabName));
                    
                    stepTimer.Restart();
                    GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
                    if (savedPrefab == null)
                    {
                        Debug.LogError($"[TreeGen] Prefab save failed at: {prefabPath}");
                        GameObject.DestroyImmediate(rootObject);
                        return;
                    }
                    Debug.Log($"[TreeGen] Timing prefab save: {stepTimer.Elapsed.TotalSeconds:F2}s");
                    Debug.Log($"[TreeGen] SUCCESS! Prefab created at: {prefabPath}");
                    
                    GameObject.DestroyImmediate(rootObject);

                    // 6. Move GLTF file if subfolders enabled
                    if (useSubfolders)
                    {
                        stepTimer.Restart();
                        AssetDatabase.Refresh(); 
                        string sourceGltf = gltfPath;
                        string destGltf = NormalizeAssetPath(Path.Combine(finalGltfFolder, Path.GetFileName(gltfPath)));
                        
                        string absSource = Path.GetFullPath(sourceGltf).Replace("\\", "/");
                        string absDest = Path.GetFullPath(destGltf).Replace("\\", "/");

                        if (absSource != absDest)
                        {
                            string error = AssetDatabase.MoveAsset(sourceGltf, destGltf);
                            if (!string.IsNullOrEmpty(error)) 
                            {
                                Debug.LogWarning($"[TreeGen] Could not move GLTF file: {error}. Trying IO move as fallback.");
                                if (File.Exists(destGltf)) File.Delete(destGltf);
                                File.Move(sourceGltf, destGltf);
                                if (File.Exists(sourceGltf + ".meta")) File.Delete(sourceGltf + ".meta");
                            }
                        }
                        Debug.Log($"[TreeGen] Timing source file move/refresh: {stepTimer.Elapsed.TotalSeconds:F2}s");
                    }
                    Debug.Log($"[TreeGen] Timing total import: {totalTimer.Elapsed.TotalSeconds:F2}s");
                }
                else
                {
                    Debug.LogError("[TreeGen] glTFast scene instantiate failed. No prefab was created.");
                    GameObject.DestroyImmediate(rootObject);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TreeGen] ERROR: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            AssetDatabase.Refresh();
        }
    }

    private void SaveMeshes(GameObject root, string folder, string importFilename)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filters)
        {
            if (mf.sharedMesh == null) continue;
            Mesh mesh = mf.sharedMesh;
            
            string rawMeshName = string.IsNullOrEmpty(mesh.name) ? mf.gameObject.name : mesh.name;
            foreach (char c in Path.GetInvalidFileNameChars()) rawMeshName = rawMeshName.Replace(c, '_');

            string finalMeshName = useSubfolders 
                ? $"SM_{importFilename}_{rawMeshName}" 
                : rawMeshName;

            string path = NormalizeAssetPath(Path.Combine(folder, finalMeshName + ".asset"));
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            
            if (existing == null) {
                Mesh newMesh = UnityEngine.Object.Instantiate(mesh);
                newMesh.name = finalMeshName;
                AssetDatabase.CreateAsset(newMesh, path);
                mf.sharedMesh = newMesh;
            } else {
                string existingName = existing.name;
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = existingName;
                EditorUtility.SetDirty(existing);
                mf.sharedMesh = existing;
            }
        }
    }

    private void ApplyExtraGrowthUVs(GameObject root, JObject json, byte[] glbBinChunk)
    {
        if (!useGrow && !useWind) return;

        JArray nodes = (JArray)json["nodes"];
        JArray meshes = (JArray)json["meshes"];
        JArray accessors = (JArray)json["accessors"];
        JArray bufferViews = (JArray)json["bufferViews"];
        JArray buffers = (JArray)json["buffers"];
        if (nodes == null || meshes == null || accessors == null || bufferViews == null || buffers == null) return;

        Dictionary<string, int> nodeMeshByName = new Dictionary<string, int>();
        foreach (JToken node in nodes)
        {
            string nodeName = node["name"]?.ToString();
            JToken meshToken = node["mesh"];
            if (!string.IsNullOrEmpty(nodeName) && meshToken != null)
                nodeMeshByName[nodeName] = (int)meshToken;
        }

        Dictionary<int, byte[]> decodedBuffers = new Dictionary<int, byte[]>();
        byte[] GetBufferBytes(int bufferIndex)
        {
            if (decodedBuffers.TryGetValue(bufferIndex, out byte[] cached)) return cached;
            string uri = buffers[bufferIndex]?["uri"]?.ToString();
            if (string.IsNullOrEmpty(uri) && bufferIndex == 0 && glbBinChunk != null)
            {
                decodedBuffers[bufferIndex] = glbBinChunk;
                return glbBinChunk;
            }
            if (string.IsNullOrEmpty(uri) || !uri.StartsWith("data:")) return null;
            int comma = uri.IndexOf(',');
            if (comma < 0) return null;
            byte[] bytes = Convert.FromBase64String(uri.Substring(comma + 1));
            decodedBuffers[bufferIndex] = bytes;
            return bytes;
        }

        List<Vector2> ReadAccessorVec2(int accessorIndex)
        {
            List<Vector2> result = new List<Vector2>();
            if (accessorIndex < 0 || accessorIndex >= accessors.Count) return result;

            JToken accessor = accessors[accessorIndex];
            if (accessor["type"]?.ToString() != "VEC2" || (int)accessor["componentType"] != 5126)
                return result;

            JToken view = bufferViews[(int)accessor["bufferView"]];
            byte[] buffer = GetBufferBytes((int)view["buffer"]);
            if (buffer == null) return result;

            int count = (int)accessor["count"];
            int stride = view["byteStride"] != null ? (int)view["byteStride"] : 8;
            int start = (view["byteOffset"] != null ? (int)view["byteOffset"] : 0)
                      + (accessor["byteOffset"] != null ? (int)accessor["byteOffset"] : 0);

            for (int i = 0; i < count; i++)
            {
                int offset = start + i * stride;
                if (offset + 8 > buffer.Length) break;
                float x = BitConverter.ToSingle(buffer, offset);
                float y = BitConverter.ToSingle(buffer, offset + 4);
                result.Add(new Vector2(x, y));
            }
            return result;
        }

        List<Vector3> ReadAccessorVec3(int accessorIndex)
        {
            List<Vector3> result = new List<Vector3>();
            if (accessorIndex < 0 || accessorIndex >= accessors.Count) return result;

            JToken accessor = accessors[accessorIndex];
            if (accessor["type"]?.ToString() != "VEC3" || (int)accessor["componentType"] != 5126)
                return result;

            JToken view = bufferViews[(int)accessor["bufferView"]];
            byte[] buffer = GetBufferBytes((int)view["buffer"]);
            if (buffer == null) return result;

            int count = (int)accessor["count"];
            int stride = view["byteStride"] != null ? (int)view["byteStride"] : 12;
            int start = (view["byteOffset"] != null ? (int)view["byteOffset"] : 0)
                      + (accessor["byteOffset"] != null ? (int)accessor["byteOffset"] : 0);

            for (int i = 0; i < count; i++)
            {
                int offset = start + i * stride;
                if (offset + 12 > buffer.Length) break;
                float x = BitConverter.ToSingle(buffer, offset);
                float y = BitConverter.ToSingle(buffer, offset + 4);
                float z = BitConverter.ToSingle(buffer, offset + 8);
                result.Add(new Vector3(x, y, z));
            }
            return result;
        }

        List<Color> ReadAccessorColor(int accessorIndex)
        {
            List<Color> result = new List<Color>();
            if (accessorIndex < 0 || accessorIndex >= accessors.Count) return result;

            JToken accessor = accessors[accessorIndex];
            string type = accessor["type"]?.ToString();
            if ((type != "VEC3" && type != "VEC4") || (int)accessor["componentType"] != 5126)
                return result;

            JToken view = bufferViews[(int)accessor["bufferView"]];
            byte[] buffer = GetBufferBytes((int)view["buffer"]);
            if (buffer == null) return result;

            int components = type == "VEC4" ? 4 : 3;
            int count = (int)accessor["count"];
            int stride = view["byteStride"] != null ? (int)view["byteStride"] : components * 4;
            int start = (view["byteOffset"] != null ? (int)view["byteOffset"] : 0)
                      + (accessor["byteOffset"] != null ? (int)accessor["byteOffset"] : 0);

            for (int i = 0; i < count; i++)
            {
                int offset = start + i * stride;
                if (offset + components * 4 > buffer.Length) break;
                float r = BitConverter.ToSingle(buffer, offset);
                float g = BitConverter.ToSingle(buffer, offset + 4);
                float b = BitConverter.ToSingle(buffer, offset + 8);
                float a = components > 3 ? BitConverter.ToSingle(buffer, offset + 12) : 1.0f;
                result.Add(new Color(r, g, b, a));
            }
            return result;
        }

        string PositionKey(Vector3 p)
        {
            const float q = 1000.0f;
            int x = Mathf.RoundToInt(p.x * q);
            int y = Mathf.RoundToInt(p.y * q);
            int z = Mathf.RoundToInt(p.z * q);
            return $"{x}_{y}_{z}";
        }

        Vector3 PermuteSigned(Vector3 p, int permutation, int signs)
        {
            float[] src = new float[] { p.x, p.y, p.z };
            int ax0 = 0, ax1 = 1, ax2 = 2;
            switch (permutation)
            {
                case 0: ax0 = 0; ax1 = 1; ax2 = 2; break;
                case 1: ax0 = 0; ax1 = 2; ax2 = 1; break;
                case 2: ax0 = 1; ax1 = 0; ax2 = 2; break;
                case 3: ax0 = 1; ax1 = 2; ax2 = 0; break;
                case 4: ax0 = 2; ax1 = 0; ax2 = 1; break;
                case 5: ax0 = 2; ax1 = 1; ax2 = 0; break;
            }

            Vector3 v = new Vector3(src[ax0], src[ax1], src[ax2]);
            if ((signs & 1) != 0) v.x = -v.x;
            if ((signs & 2) != 0) v.y = -v.y;
            if ((signs & 4) != 0) v.z = -v.z;
            return v;
        }

        int DecodeGrowthRecordIndex(Vector2 uv3, Vector2 uv4)
        {
            if (uv3.y > 1.5f)
                return Mathf.RoundToInt(uv3.y / 6.0f);

            int recordLo = Mathf.RoundToInt(Mathf.Clamp01(uv3.y) * 255.0f);
            int recordHi = Mathf.RoundToInt(Mathf.Clamp01(uv4.y) * 255.0f);
            return recordLo + recordHi * 256;
        }

        int FindMeshIndexForFilter(MeshFilter mf)
        {
            string objectName = mf.gameObject.name;
            if (nodeMeshByName.TryGetValue(objectName, out int exact)) return exact;

            foreach (var kvp in nodeMeshByName)
            {
                if (objectName.Contains(kvp.Key) || kvp.Key.Contains(objectName))
                    return kvp.Value;
            }
            return -1;
        }

        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
        {
            Mesh unityMesh = mf.sharedMesh;
            if (unityMesh == null) continue;

            int meshIndex = FindMeshIndexForFilter(mf);
            if (meshIndex < 0 || meshIndex >= meshes.Count) continue;

            JArray primitives = (JArray)meshes[meshIndex]?["primitives"];
            if (primitives == null || primitives.Count == 0) continue;

            List<Vector2> concatenatedUv5 = new List<Vector2>();
            List<Vector2> concatenatedUv6 = new List<Vector2>();
            List<Vector2> concatenatedUv7 = new List<Vector2>();
            List<Vector2> firstUv5 = null;
            List<Vector2> firstUv6 = null;
            List<Vector2> firstUv7 = null;
            List<Vector3> allPositions = new List<Vector3>();
            List<Vector4> allGrowth = new List<Vector4>();
            List<Vector2> allOldWindUv3 = new List<Vector2>();
            List<Vector2> allUv1 = new List<Vector2>();
            List<Vector2> allUv2 = new List<Vector2>();
            List<Vector2> allUv3 = new List<Vector2>();
            List<Vector2> allUv4 = new List<Vector2>();
            List<Color> allColors = new List<Color>();
            bool hasOldWindUv3 = false;
            Dictionary<int, Vector4> growthByRecordIndex = new Dictionary<int, Vector4>();

            foreach (JToken prim in primitives)
            {
                JToken attrs = prim["attributes"];
                if (attrs?["POSITION"] == null || attrs?["TEXCOORD_5"] == null || attrs?["TEXCOORD_6"] == null) continue;

                List<Vector3> positions = ReadAccessorVec3((int)attrs["POSITION"]);
                List<Vector2> uv1 = attrs["TEXCOORD_1"] != null ? ReadAccessorVec2((int)attrs["TEXCOORD_1"]) : null;
                List<Vector2> uv2 = attrs["TEXCOORD_2"] != null ? ReadAccessorVec2((int)attrs["TEXCOORD_2"]) : null;
                List<Vector2> uv3 = attrs["TEXCOORD_3"] != null ? ReadAccessorVec2((int)attrs["TEXCOORD_3"]) : null;
                List<Vector2> uv4 = attrs["TEXCOORD_4"] != null ? ReadAccessorVec2((int)attrs["TEXCOORD_4"]) : null;
                List<Vector2> uv5 = ReadAccessorVec2((int)attrs["TEXCOORD_5"]);
                List<Vector2> uv6 = ReadAccessorVec2((int)attrs["TEXCOORD_6"]);
                List<Vector2> uv7 = attrs["TEXCOORD_7"] != null ? ReadAccessorVec2((int)attrs["TEXCOORD_7"]) : null;
                List<Color> colors = attrs["COLOR_0"] != null ? ReadAccessorColor((int)attrs["COLOR_0"]) : null;
                if (positions.Count == 0 || uv5.Count == 0 || positions.Count != uv5.Count || uv5.Count != uv6.Count) continue;

                if (firstUv5 == null)
                {
                    firstUv5 = uv5;
                    firstUv6 = uv6;
                    firstUv7 = uv7;
                }
                concatenatedUv5.AddRange(uv5);
                concatenatedUv6.AddRange(uv6);
                bool primHasWindUv7 = uv7 != null && uv7.Count == uv5.Count;
                if (primHasWindUv7)
                {
                    hasOldWindUv3 = true;
                    concatenatedUv7.AddRange(uv7);
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    allPositions.Add(positions[i]);
                    Vector4 packedGrowth = new Vector4(uv5[i].x, uv5[i].y, uv6[i].x, uv6[i].y);
                    allGrowth.Add(packedGrowth);
                    allOldWindUv3.Add(primHasWindUv7 ? uv7[i] : Vector2.zero);
                    allUv1.Add(uv1 != null && uv1.Count == uv5.Count ? uv1[i] : Vector2.zero);
                    allUv2.Add(uv2 != null && uv2.Count == uv5.Count ? uv2[i] : Vector2.zero);
                    allUv3.Add(uv3 != null && uv3.Count == uv5.Count ? uv3[i] : Vector2.zero);
                    allUv4.Add(uv4 != null && uv4.Count == uv5.Count ? uv4[i] : Vector2.zero);
                    allColors.Add(colors != null && colors.Count == uv5.Count ? colors[i] : Color.white);

                    if (uv3 != null && uv4 != null && uv3.Count == uv5.Count && uv4.Count == uv5.Count)
                    {
                        int recordIndex = DecodeGrowthRecordIndex(uv3[i], uv4[i]);
                        if (!growthByRecordIndex.ContainsKey(recordIndex))
                            growthByRecordIndex.Add(recordIndex, packedGrowth);
                    }
                }
            }

            List<Vector2> finalUv1 = null;
            List<Vector2> finalUv2 = null;
            List<Vector2> finalUv3 = null;
            List<Vector2> finalUv4 = null;
            List<Vector2> finalUv5 = null;
            List<Vector2> finalUv6 = null;
            List<Vector2> finalUv7 = null;
            List<Color> finalColors = null;
            List<Vector2> unityUv1 = new List<Vector2>();
            List<Vector2> unityUv2 = new List<Vector2>();
            List<Vector2> unityUv3 = new List<Vector2>();
            List<Vector2> unityUv4 = new List<Vector2>();
            List<Color> unityColors = new List<Color>();
            unityMesh.GetUVs(1, unityUv1);
            unityMesh.GetUVs(2, unityUv2);
            unityMesh.GetUVs(3, unityUv3);
            unityMesh.GetUVs(4, unityUv4);
            unityMesh.GetColors(unityColors);
            // UV3.y is reserved for the old working wind pivot index in the current
            // export layout, so it is not a reliable growth-record key. Keep using
            // the position map for UV5/UV6 growth data; that is the path that matches
            // the visible growth result.
            if (false && growthByRecordIndex.Count > 0 && unityUv3.Count == unityMesh.vertexCount && unityUv4.Count == unityMesh.vertexCount)
            {
                List<Vector2> mappedUv5 = new List<Vector2>(unityMesh.vertexCount);
                List<Vector2> mappedUv6 = new List<Vector2>(unityMesh.vertexCount);
                int matched = 0;

                for (int i = 0; i < unityMesh.vertexCount; i++)
                {
                    int recordIndex = DecodeGrowthRecordIndex(unityUv3[i], unityUv4[i]);
                    if (growthByRecordIndex.TryGetValue(recordIndex, out Vector4 packedGrowth))
                    {
                        matched++;
                        mappedUv5.Add(new Vector2(packedGrowth.x, packedGrowth.y));
                        mappedUv6.Add(new Vector2(packedGrowth.z, packedGrowth.w));
                    }
                    else
                    {
                        mappedUv5.Add(Vector2.zero);
                        mappedUv6.Add(Vector2.zero);
                    }
                }

                if (matched == unityMesh.vertexCount)
                {
                    finalUv5 = mappedUv5;
                    finalUv6 = mappedUv6;
                    VerboseLog($"[TreeGen] Growth UV record map: {mf.name} matched {matched}/{unityMesh.vertexCount}");
                }
                else
                {
                    Debug.LogWarning($"[TreeGen] Growth UV record map incomplete for {mf.name}: matched {matched}/{unityMesh.vertexCount}; falling back to position/accessor mapping.");
                }
            }

            Vector3[] unityPositions = unityMesh.vertices;
            if (finalUv5 == null && allPositions.Count > 0 && allPositions.Count == allGrowth.Count && unityPositions != null && unityPositions.Length == unityMesh.vertexCount)
            {
                    Dictionary<string, List<int>> bestSourceIndicesByPosition = null;
                    int bestPermutation = -1;
                    int bestSigns = -1;
                    int bestMatched = -1;
                    bool foundExactPositionMap = false;

                    for (int permutation = 0; permutation < 6; permutation++)
                    {
                        for (int signs = 0; signs < 8; signs++)
                        {
                            Dictionary<string, List<int>> candidateIndicesByPosition = new Dictionary<string, List<int>>();
                            for (int i = 0; i < allPositions.Count; i++)
                            {
                                Vector3 mappedPosition = PermuteSigned(allPositions[i], permutation, signs);
                                string key = PositionKey(mappedPosition);
                                if (!candidateIndicesByPosition.TryGetValue(key, out List<int> indicesAtPosition))
                                {
                                    indicesAtPosition = new List<int>();
                                    candidateIndicesByPosition.Add(key, indicesAtPosition);
                                }
                                indicesAtPosition.Add(i);
                            }

                            int matchedCandidate = 0;
                            for (int i = 0; i < unityPositions.Length; i++)
                            {
                                if (candidateIndicesByPosition.ContainsKey(PositionKey(unityPositions[i]))) matchedCandidate++;
                            }

                            if (matchedCandidate > bestMatched)
                            {
                                bestMatched = matchedCandidate;
                                bestPermutation = permutation;
                                bestSigns = signs;
                                bestSourceIndicesByPosition = candidateIndicesByPosition;
                                if (matchedCandidate == unityPositions.Length)
                                {
                                    foundExactPositionMap = true;
                                    break;
                                }
                            }
                        }
                        if (foundExactPositionMap) break;
                    }

                    List<Vector2> mappedUv1 = new List<Vector2>(unityMesh.vertexCount);
                    List<Vector2> mappedUv2 = new List<Vector2>(unityMesh.vertexCount);
                    List<Vector2> mappedUv3 = new List<Vector2>(unityMesh.vertexCount);
                    List<Vector2> mappedUv4 = new List<Vector2>(unityMesh.vertexCount);
                    List<Vector2> mappedUv5 = new List<Vector2>(unityMesh.vertexCount);
                    List<Vector2> mappedUv6 = new List<Vector2>(unityMesh.vertexCount);
                    List<Vector2> mappedUv7 = new List<Vector2>(unityMesh.vertexCount);
                    List<Color> mappedColors = new List<Color>(unityMesh.vertexCount);
                    int matched = 0;
                    int windMatched = 0;
                    Dictionary<string, int> sourcePositionCursors = new Dictionary<string, int>();

                    for (int i = 0; i < unityPositions.Length; i++)
                    {
                        string key = PositionKey(unityPositions[i]);
                        int sourceIndex = -1;
                        if (bestSourceIndicesByPosition != null && bestSourceIndicesByPosition.TryGetValue(key, out List<int> sourceIndices) && sourceIndices.Count > 0)
                        {
                            sourcePositionCursors.TryGetValue(key, out int cursor);
                            sourceIndex = sourceIndices[Mathf.Min(cursor, sourceIndices.Count - 1)];
                            sourcePositionCursors[key] = cursor + 1;
                        }

                        if (sourceIndex >= 0)
                        {
                            matched++;
                            Vector4 packedGrowth = allGrowth[sourceIndex];
                            mappedUv5.Add(new Vector2(packedGrowth.x, packedGrowth.y));
                            mappedUv6.Add(new Vector2(packedGrowth.z, packedGrowth.w));
                            windMatched++;
                            mappedUv1.Add(allUv1[sourceIndex]);
                            mappedUv2.Add(allUv2[sourceIndex]);
                            mappedUv3.Add(allUv3[sourceIndex]);
                            mappedUv4.Add(allUv4[sourceIndex]);
                            mappedUv7.Add(allOldWindUv3[sourceIndex]);
                            mappedColors.Add(allColors[sourceIndex]);
                        }
                        else
                        {
                            mappedUv1.Add(unityUv1.Count == unityMesh.vertexCount ? unityUv1[i] : Vector2.zero);
                            mappedUv2.Add(unityUv2.Count == unityMesh.vertexCount ? unityUv2[i] : Vector2.zero);
                            mappedUv3.Add(unityUv3.Count == unityMesh.vertexCount ? unityUv3[i] : Vector2.zero);
                            mappedUv4.Add(unityUv4.Count == unityMesh.vertexCount ? unityUv4[i] : Vector2.zero);
                            mappedUv5.Add(Vector2.zero);
                            mappedUv6.Add(Vector2.zero);
                            mappedUv7.Add(Vector2.zero);
                            mappedColors.Add(unityColors.Count == unityMesh.vertexCount ? unityColors[i] : Color.white);
                        }
                    }

                    if (matched == unityMesh.vertexCount)
                    {
                        finalUv1 = mappedUv1;
                        finalUv2 = mappedUv2;
                        finalUv3 = mappedUv3;
                        finalUv4 = mappedUv4;
                        finalUv5 = mappedUv5;
                        finalUv6 = mappedUv6;
                        if (hasOldWindUv3) finalUv7 = mappedUv7;
                        finalColors = mappedColors;
                        VerboseLog($"[TreeGen] Growth UV position map: {mf.name} matched {matched}/{unityMesh.vertexCount} wind={windMatched}/{unityMesh.vertexCount} perm={bestPermutation} signs={bestSigns}");
                    }
                    else
                    {
                        Debug.LogWarning($"[TreeGen] Growth UV position map incomplete for {mf.name}: best matched {matched}/{unityMesh.vertexCount} perm={bestPermutation} signs={bestSigns}; falling back to accessor order.");
                    }
            }

            if (finalUv5 == null && concatenatedUv5.Count == unityMesh.vertexCount)
            {
                finalUv5 = concatenatedUv5;
                finalUv6 = concatenatedUv6;
                if (concatenatedUv7.Count == unityMesh.vertexCount) finalUv7 = concatenatedUv7;
            }
            else if (finalUv5 == null && firstUv5 != null && firstUv5.Count == unityMesh.vertexCount)
            {
                finalUv5 = firstUv5;
                finalUv6 = firstUv6;
                if (firstUv7 != null && firstUv7.Count == unityMesh.vertexCount) finalUv7 = firstUv7;
            }

            if (finalUv5 == null || finalUv6 == null)
            {
                Debug.LogWarning($"[TreeGen] Growth UV copy skipped for {mf.name}: Unity vertices={unityMesh.vertexCount}, firstPrim={(firstUv5 != null ? firstUv5.Count : 0)}, concat={concatenatedUv5.Count}");
                continue;
            }

            List<Vector2> importedUv7 = new List<Vector2>();
            unityMesh.GetUVs(7, importedUv7);
            if (useWind && hasOldWindUv3 && finalUv7 != null && finalUv7.Count == unityMesh.vertexCount)
            {
                VerboseLog($"[TreeGen] Wind UV7 source: {mf.name} glTF accessor position map");
            }
            else if (useWind && hasOldWindUv3 && importedUv7.Count == unityMesh.vertexCount)
            {
                finalUv7 = importedUv7;
                Debug.LogWarning($"[TreeGen] Wind UV7 source: {mf.name} Unity imported fallback");
            }

            if (finalUv1 != null && finalUv1.Count == unityMesh.vertexCount) unityMesh.SetUVs(1, finalUv1);
            if (finalUv2 != null && finalUv2.Count == unityMesh.vertexCount) unityMesh.SetUVs(2, finalUv2);
            if (finalUv3 != null && finalUv3.Count == unityMesh.vertexCount) unityMesh.SetUVs(3, finalUv3);
            if (finalUv4 != null && finalUv4.Count == unityMesh.vertexCount) unityMesh.SetUVs(4, finalUv4);
            if (finalColors != null && finalColors.Count == unityMesh.vertexCount) unityMesh.SetColors(finalColors);
            unityMesh.SetUVs(5, finalUv5);
            unityMesh.SetUVs(6, finalUv6);
            if (useWind && finalUv7 != null && finalUv7.Count == unityMesh.vertexCount)
            {
                // Dedicated parentZ/rootZ payload for wind hierarchy inheritance.
                unityMesh.SetUVs(7, finalUv7);
                VerboseLog($"[TreeGen] Wind UV7 copy: {mf.name} parentZRootZ vertices={finalUv7.Count}");
            }
            else if (useWind)
            {
                Debug.LogWarning($"[TreeGen] Wind UV7 copy skipped for {mf.name}: Unity vertices={unityMesh.vertexCount}, concat={concatenatedUv7.Count}");
            }
            unityMesh.UploadMeshData(false);
            EditorUtility.SetDirty(unityMesh);
            VerboseLog($"[TreeGen] Growth UV copy: {mf.name} UV5/UV6 vertices={finalUv5.Count}");
            if (logWindDiagnostics) LogWindHierarchyDiagnostics(mf.name, unityMesh);
        }
    }

    private void LogWindHierarchyDiagnostics(string meshName, Mesh mesh)
    {
        if (mesh == null) return;

        List<Vector2> uv1 = new List<Vector2>();
        List<Vector2> uv2 = new List<Vector2>();
        List<Vector2> uv3 = new List<Vector2>();
        List<Vector2> uv4 = new List<Vector2>();
        List<Vector2> uv7 = new List<Vector2>();
        List<Color> colors = new List<Color>();
        mesh.GetUVs(1, uv1);
        mesh.GetUVs(2, uv2);
        mesh.GetUVs(3, uv3);
        mesh.GetUVs(4, uv4);
        mesh.GetUVs(7, uv7);
        mesh.GetColors(colors);

        int count = mesh.vertexCount;
        if (uv1.Count != count || uv2.Count != count || uv3.Count != count || uv4.Count != count || uv7.Count != count || colors.Count != count)
        {
            Debug.LogWarning($"[TreeGen] Wind hierarchy diag skipped for {meshName}: uv1={uv1.Count}, uv2={uv2.Count}, uv3={uv3.Count}, uv4={uv4.Count}, uv7={uv7.Count}, colors={colors.Count}, vertices={count}");
            return;
        }

        int mode0 = 0, mode1 = 0, mode2 = 0;
        float minDist = float.PositiveInfinity;
        float maxDist = 0.0f;
        double sumDist = 0.0;
        float minAttachMask = float.PositiveInfinity;
        float maxAttachMask = 0.0f;
        double sumAttachMask = 0.0;
        int uv7Present = 0;
        int l2NearestCount = 0;
        int l2RawNearestCount = 0;
        float minNearestParentPivot = float.PositiveInfinity;
        float maxNearestParentPivot = 0.0f;
        double sumNearestParentPivot = 0.0;
        float minRawNearestParentPivot = float.PositiveInfinity;
        float maxRawNearestParentPivot = 0.0f;
        double sumRawNearestParentPivot = 0.0;
        float minRawPhaseDelta = float.PositiveInfinity;
        float maxRawPhaseDelta = 0.0f;
        double sumRawPhaseDelta = 0.0;
        float minNearestTwigToBranchVert = float.PositiveInfinity;
        float maxNearestTwigToBranchVert = 0.0f;
        double sumNearestTwigToBranchVert = 0.0;
        float minPhaseDelta = float.PositiveInfinity;
        float maxPhaseDelta = 0.0f;
        double sumPhaseDelta = 0.0;
        float minPackedParentBend = float.PositiveInfinity;
        float maxPackedParentBend = 0.0f;
        double sumPackedParentBend = 0.0;

        Vector3[] vertices = mesh.vertices;
        List<Vector3> l1Pivots = new List<Vector3>();
        List<Vector3> l1RawPivots = new List<Vector3>();
        List<Vector3> l1Vertices = new List<Vector3>();
        List<float> l1Phases = new List<float>();

        for (int i = 0; i < count; i++)
        {
            Color c = colors[i];
            int mode = Mathf.FloorToInt(c.b * 4.0f + 0.01f);
            if (mode < 1) mode0++;
            else if (mode < 2)
            {
                mode1++;
                Vector3 rawL1Pivot = new Vector3(uv1[i].x, uv2[i].x, uv1[i].y);
                l1RawPivots.Add(rawL1Pivot);
                l1Pivots.Add(Vector3.Scale(rawL1Pivot, new Vector3(-1f, 1f, 1f)));
                l1Vertices.Add(vertices[i]);
                l1Phases.Add(c.r);
            }
            else mode2++;
        }

        for (int i = 0; i < count; i++)
        {
            Color c = colors[i];
            int mode = Mathf.FloorToInt(c.b * 4.0f + 0.01f);
            if (Mathf.Abs(uv7[i].x) > 0.00001f || Mathf.Abs(uv7[i].y) > 0.00001f)
                uv7Present++;

            if (mode < 2) continue;

            Vector3 rawPivot = new Vector3(uv1[i].x, uv2[i].x, uv1[i].y);
            bool hasWindUv7 = Mathf.Abs(uv7[i].x) > 0.00001f || Mathf.Abs(uv7[i].y) > 0.00001f;
            float parentZ = hasWindUv7 ? uv7[i].x : uv4[i].y;
            Vector3 rawParent = new Vector3(uv2[i].y, uv4[i].x, parentZ);
            Vector3 pivot = Vector3.Scale(rawPivot, new Vector3(-1f, 1f, 1f));
            Vector3 parentPivot = Vector3.Scale(rawParent, new Vector3(-1f, 1f, 1f));
            float dist = Vector3.Distance(pivot, parentPivot);
            float attachMask = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.25f, 2.50f, dist));
            float packedParentBend = Mathf.Clamp01((uv3[i].y - Mathf.Floor(uv3[i].y)) * 4.0f);

            minDist = Mathf.Min(minDist, dist);
            maxDist = Mathf.Max(maxDist, dist);
            sumDist += dist;
            minAttachMask = Mathf.Min(minAttachMask, attachMask);
            maxAttachMask = Mathf.Max(maxAttachMask, attachMask);
            sumAttachMask += attachMask;
            minPackedParentBend = Mathf.Min(minPackedParentBend, packedParentBend);
            maxPackedParentBend = Mathf.Max(maxPackedParentBend, packedParentBend);
            sumPackedParentBend += packedParentBend;

            if (l1Pivots.Count > 0)
            {
                float nearestRawParentPivot = float.PositiveInfinity;
                float nearestParentPivot = float.PositiveInfinity;
                float nearestRawPhase = 0.0f;
                float nearestPhase = 0.0f;
                for (int j = 0; j < l1Pivots.Count; j++)
                {
                    float rawCandidate = Vector3.Distance(rawParent, l1RawPivots[j]);
                    if (rawCandidate < nearestRawParentPivot)
                    {
                        nearestRawParentPivot = rawCandidate;
                        nearestRawPhase = l1Phases[j];
                    }

                    float candidate = Vector3.Distance(parentPivot, l1Pivots[j]);
                    if (candidate < nearestParentPivot)
                    {
                        nearestParentPivot = candidate;
                        nearestPhase = l1Phases[j];
                    }
                }

                float nearestTwigToBranchVert = float.PositiveInfinity;
                for (int j = 0; j < l1Vertices.Count; j++)
                {
                    nearestTwigToBranchVert = Mathf.Min(nearestTwigToBranchVert, Vector3.Distance(pivot, l1Vertices[j]));
                }

                float rawPhaseDelta = Mathf.Abs(Mathf.DeltaAngle(c.g * 360.0f, nearestRawPhase * 360.0f)) / 360.0f;
                float phaseDelta = Mathf.Abs(Mathf.DeltaAngle(c.g * 360.0f, nearestPhase * 360.0f)) / 360.0f;
                minRawNearestParentPivot = Mathf.Min(minRawNearestParentPivot, nearestRawParentPivot);
                maxRawNearestParentPivot = Mathf.Max(maxRawNearestParentPivot, nearestRawParentPivot);
                sumRawNearestParentPivot += nearestRawParentPivot;
                minRawPhaseDelta = Mathf.Min(minRawPhaseDelta, rawPhaseDelta);
                maxRawPhaseDelta = Mathf.Max(maxRawPhaseDelta, rawPhaseDelta);
                sumRawPhaseDelta += rawPhaseDelta;
                l2RawNearestCount++;

                minNearestParentPivot = Mathf.Min(minNearestParentPivot, nearestParentPivot);
                maxNearestParentPivot = Mathf.Max(maxNearestParentPivot, nearestParentPivot);
                sumNearestParentPivot += nearestParentPivot;
                minNearestTwigToBranchVert = Mathf.Min(minNearestTwigToBranchVert, nearestTwigToBranchVert);
                maxNearestTwigToBranchVert = Mathf.Max(maxNearestTwigToBranchVert, nearestTwigToBranchVert);
                sumNearestTwigToBranchVert += nearestTwigToBranchVert;
                minPhaseDelta = Mathf.Min(minPhaseDelta, phaseDelta);
                maxPhaseDelta = Mathf.Max(maxPhaseDelta, phaseDelta);
                sumPhaseDelta += phaseDelta;
                l2NearestCount++;
            }
        }

        if (mode2 > 0)
        {
            Debug.Log($"[TreeGen] Wind hierarchy diag {meshName}: mode0={mode0}, mode1={mode1}, mode2={mode2}, uv7Present={uv7Present}/{count}, L2 parentBranchDist min/avg/max={minDist:F3}/{(sumDist / mode2):F3}/{maxDist:F3}, parentAttachMask@2.5 min/avg/max={minAttachMask:F3}/{(sumAttachMask / mode2):F3}/{maxAttachMask:F3}, packedParentBend min/avg/max={minPackedParentBend:F3}/{(sumPackedParentBend / mode2):F3}/{maxPackedParentBend:F3}");
            if (l2NearestCount > 0)
            {
                Debug.Log($"[TreeGen] Wind L2 attach diag {meshName}: parentPivot->nearestL1Pivot min/avg/max={minNearestParentPivot:F4}/{(sumNearestParentPivot / l2NearestCount):F4}/{maxNearestParentPivot:F4}, twigPivot->nearestL1Vertex min/avg/max={minNearestTwigToBranchVert:F4}/{(sumNearestTwigToBranchVert / l2NearestCount):F4}/{maxNearestTwigToBranchVert:F4}, parentPhase->nearestL1Phase delta min/avg/max={minPhaseDelta:F4}/{(sumPhaseDelta / l2NearestCount):F4}/{maxPhaseDelta:F4}");
            }
            if (l2RawNearestCount > 0)
            {
                Debug.Log($"[TreeGen] Wind L2 raw attach diag {meshName}: rawParent->rawL1Pivot min/avg/max={minRawNearestParentPivot:F4}/{(sumRawNearestParentPivot / l2RawNearestCount):F4}/{maxRawNearestParentPivot:F4}, rawParentPhase->rawL1Phase delta min/avg/max={minRawPhaseDelta:F4}/{(sumRawPhaseDelta / l2RawNearestCount):F4}/{maxRawPhaseDelta:F4}");
            }
        }
        else
        {
            Debug.Log($"[TreeGen] Wind hierarchy diag {meshName}: mode0={mode0}, mode1={mode1}, mode2={mode2}, uv7Present={uv7Present}/{count}");
        }
    }

    private void ExtractTextures(GltfImport importer, JObject json, byte[] glbBinChunk, string folder, string importFilename,
                                 ref Dictionary<int, Texture2D> indexMap, 
                                 ref Dictionary<string, Texture2D> nameMap)
    {
        JArray images = (JArray)json["images"];
        JArray bufferViews = (JArray)json["bufferViews"];

        bool WriteBytesIfChanged(string assetPath, byte[] bytes)
        {
            if (File.Exists(assetPath))
            {
                byte[] existing = File.ReadAllBytes(assetPath);
                if (existing.Length == bytes.Length && existing.SequenceEqual(bytes))
                    return false;
            }

            File.WriteAllBytes(assetPath, bytes);
            return true;
        }

        byte[] GetImageBytes(JToken img)
        {
            string uri = img["uri"]?.ToString();
            if (!string.IsNullOrEmpty(uri) && uri.StartsWith("data:"))
            {
                int comma = uri.IndexOf(',');
                if (comma < 0) return null;
                return System.Convert.FromBase64String(uri.Substring(comma + 1));
            }

            JToken bufferViewToken = img["bufferView"];
            if (bufferViewToken == null || bufferViews == null || glbBinChunk == null) return null;

            int viewIndex = (int)bufferViewToken;
            if (viewIndex < 0 || viewIndex >= bufferViews.Count) return null;

            JToken view = bufferViews[viewIndex];
            int bufferIndex = view["buffer"] != null ? (int)view["buffer"] : 0;
            if (bufferIndex != 0) return null;

            int byteOffset = view["byteOffset"] != null ? (int)view["byteOffset"] : 0;
            int byteLength = view["byteLength"] != null ? (int)view["byteLength"] : 0;
            if (byteOffset < 0 || byteLength <= 0 || byteOffset + byteLength > glbBinChunk.Length) return null;

            byte[] bytes = new byte[byteLength];
            Buffer.BlockCopy(glbBinChunk, byteOffset, bytes, 0, byteLength);
            return bytes;
        }

        void ConfigurePivotTextureImporter(string assetPath)
        {
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return;

            bool changed = false;
            if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; changed = true; }
            if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (ti.sRGBTexture != false) { ti.sRGBTexture = false; changed = true; }
            if (ti.mipmapEnabled != false) { ti.mipmapEnabled = false; changed = true; }
            if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; changed = true; }
            if (ti.maxTextureSize != 8192) { ti.maxTextureSize = 8192; changed = true; }
            if (changed) ti.SaveAndReimport();
        }

        // Pivot/growth textures are data textures. Do not pass them through glTFast's
        // Texture2D + Graphics.Blit + EncodeToPNG path, because Unity can resample or
        // reinterpret NPOT data textures and make the shader sample a constant corner.
        if (images != null)
        {
            for (int imgIndex = 0; imgIndex < images.Count; imgIndex++)
            {
                JToken img = images[imgIndex];
                string imageName = img["name"]?.ToString() ?? $"Texture_{imgIndex}";
                if (!imageName.ToLower().Contains("pivot")) continue;

                byte[] imageBytes = GetImageBytes(img);
                if (imageBytes == null || imageBytes.Length == 0) continue;

                string texName = imageName;
                if (Path.HasExtension(texName)) texName = Path.GetFileNameWithoutExtension(texName);
                foreach (char c in Path.GetInvalidFileNameChars()) texName = texName.Replace(c, '_');

                string finalTexName = useSubfolders
                    ? $"T_{importFilename}_{texName}"
                    : texName;

                string savePath = NormalizeAssetPath(Path.Combine(folder, finalTexName + ".png"));
                bool textureChanged = WriteBytesIfChanged(savePath, imageBytes);

                if (textureChanged || AssetDatabase.LoadAssetAtPath<Texture2D>(savePath) == null)
                    AssetDatabase.ImportAsset(savePath);
                ConfigurePivotTextureImporter(savePath);

                Texture2D loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
                if (loadedTex != null)
                {
                    indexMap[imgIndex] = loadedTex;
                    if (!nameMap.ContainsKey(texName)) nameMap[texName] = loadedTex;
                    string simpleName = Path.GetFileNameWithoutExtension(texName);
                    if (!nameMap.ContainsKey(simpleName)) nameMap[simpleName] = loadedTex;
                    VerboseLog($"[TreeGen] Extracted pivot data texture directly: {texName}");
                }
            }
        }

        // --- FIX START: Identify Normal Maps by Usage ---
        // We scan the materials to see which images are actually assigned to the "normalTexture" slot.
        HashSet<int> normalImageIndices = new HashSet<int>();
        JArray jsonMats = (JArray)json["materials"];
        JArray jsonTexs = (JArray)json["textures"];

        if (jsonMats != null && jsonTexs != null)
        {
            foreach (var mat in jsonMats)
            {
                if (mat["normalTexture"] != null)
                {
                    int texIdx = (int)mat["normalTexture"]["index"];
                    if (texIdx >= 0 && texIdx < jsonTexs.Count)
                    {
                        // Get the source image index from the texture definition
                        if (jsonTexs[texIdx]["source"] != null)
                        {
                            int sourceImgIdx = (int)jsonTexs[texIdx]["source"];
                            normalImageIndices.Add(sourceImgIdx);
                        }
                    }
                }
            }
        }
        // --- FIX END ---

        for (int i = 0; i < importer.TextureCount; i++)
        {
            Texture2D memoryTex = importer.GetTexture(i);
            if (memoryTex == null) continue;

            string texName = memoryTex.name;
            if (images != null && i < images.Count && images[i]["name"] != null)
                texName = images[i]["name"].ToString();
            
            if (string.IsNullOrEmpty(texName)) texName = $"Texture_{i}";
            
            // (Previous fix: Remove double extension)
            if (Path.HasExtension(texName)) 
            {
                texName = Path.GetFileNameWithoutExtension(texName);
            }
            
            foreach (char c in Path.GetInvalidFileNameChars()) texName = texName.Replace(c, '_');

            string finalTexName = useSubfolders 
                ? $"T_{importFilename}_{texName}" 
                : texName;

            string savePath = NormalizeAssetPath(Path.Combine(folder, finalTexName + ".png"));
            
            bool isPivot = texName.ToLower().Contains("pivot");
            // --- Skip pivot texture extraction if wind is disabled ---
            if (isPivot && !useWind && !useGrow) continue;
            if (isPivot && nameMap.ContainsKey(texName)) continue;
            
            // --- FIX: Robust Normal Detection ---
            // Check 1: Is this image index used in a Normal slot? (from pre-scan)
            // Check 2: Does the name contain "normal", "norm", or end in "_n"?
            bool nameHasNormalTag = texName.ToLower().Contains("normal") || 
                                    texName.ToLower().Contains("_norm") || 
                                    texName.ToLower().EndsWith("_n");

            bool isNormal = normalImageIndices.Contains(i) || nameHasNormalTag;
            // ------------------------------------

            RenderTexture tmp = RenderTexture.GetTemporary(memoryTex.width, memoryTex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(memoryTex, tmp);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = tmp;

            TextureFormat fmt = isPivot ? TextureFormat.RGBAFloat : TextureFormat.RGBA32;
            Texture2D readable = new Texture2D(memoryTex.width, memoryTex.height, fmt, false);
            readable.ReadPixels(new Rect(0,0, tmp.width, tmp.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tmp);

            byte[] pngBytes = readable.EncodeToPNG();
            if (Application.isPlaying) UnityEngine.Object.Destroy(readable); else UnityEngine.Object.DestroyImmediate(readable);

            bool textureChanged = WriteBytesIfChanged(savePath, pngBytes);
            if (textureChanged || AssetDatabase.LoadAssetAtPath<Texture2D>(savePath) == null)
                AssetDatabase.ImportAsset(savePath);

            TextureImporter ti = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (ti != null)
            {
                bool settingsChanged = false;
                if (isPivot) {
                    if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; settingsChanged = true; }
                    if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; settingsChanged = true; }
                    if (ti.sRGBTexture != false) { ti.sRGBTexture = false; settingsChanged = true; }
                    if (ti.mipmapEnabled != false) { ti.mipmapEnabled = false; settingsChanged = true; }
                    if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; settingsChanged = true; }
                    if (ti.maxTextureSize != 8192) { ti.maxTextureSize = 8192; settingsChanged = true; }
                } else if (isNormal) {
                    // --- FIX: Apply Normal Map Settings ---
                    if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; settingsChanged = true; }
                }
                if (settingsChanged) ti.SaveAndReimport();
            }

            Texture2D loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
            indexMap[i] = loadedTex;
            
            if (!nameMap.ContainsKey(texName)) nameMap[texName] = loadedTex;
            string simpleName = Path.GetFileNameWithoutExtension(texName);
            if (!nameMap.ContainsKey(simpleName)) nameMap[simpleName] = loadedTex;
        }
    }

    private void CreateMaterials(JObject json, string folder, string importFilename,
                                 Dictionary<int, Texture2D> indexTextures, 
                                 Dictionary<string, Texture2D> nameTextures, 
                                 ref Dictionary<string, Material> materials)
    {
        JArray jsonMats = (JArray)json["materials"];
        JArray jsonTextures = (JArray)json["textures"];
        if (jsonMats == null) return;

        JToken globalExtras = json["asset"]?["extras"];
        JToken globalPivotData = globalExtras?["pivotData"];
        
        if (globalPivotData != null) {
            VerboseLog($"[TreeGen] Found GLOBAL pivot data in asset extras.");
        }

        // --- NEW: Robust Shader Search (looks in all subfolders) ---
        Shader targetShader = FindTreeShader(GetShaderName(selectedPipeline));

        for (int mIndex = 0; mIndex < jsonMats.Count; mIndex++)
        {
            JToken matData = jsonMats[mIndex];
            string rawMatName = matData["name"]?.ToString() ?? $"Material_{mIndex}";
            foreach (char c in Path.GetInvalidFileNameChars()) rawMatName = rawMatName.Replace(c, '_');
            
            string finalMatName = useSubfolders 
                ? $"M_{importFilename}_{rawMatName}" 
                : rawMatName;

            string matPath = NormalizeAssetPath(Path.Combine(folder, finalMatName + ".mat"));
            Material newMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool materialAlreadyExists = newMat != null;
            if (newMat == null)
            {
                newMat = new Material(targetShader) { name = finalMatName };
            }
            else
            {
                newMat.shader = targetShader;
                newMat.name = finalMatName;
                newMat.shaderKeywords = new string[0];
                newMat.renderQueue = -1;
            }
            newMat.SetFloat("_Billboard", 0.0f);
            newMat.SetFloat("_UseHeightBlend", 0.0f);
            newMat.SetFloat("_UseTopLayer", 0.0f);
            
            // Standard Maps
            if (matData["pbrMetallicRoughness"] != null)
            {
                JToken pbr = matData["pbrMetallicRoughness"];
                if (pbr["baseColorTexture"] != null)
                {
                    int texIndex = (int)pbr["baseColorTexture"]["index"];
                    int imgIndex = (int)jsonTextures[texIndex]["source"];
                    if (indexTextures.TryGetValue(imgIndex, out Texture2D t)) newMat.SetTexture("_BaseMap", t);
                }
                if (pbr["baseColorFactor"] != null) newMat.SetColor("_BaseColor", ParseColor(pbr["baseColorFactor"]));
            }
            if (matData["normalTexture"] != null)
            {
                int texIndex = (int)matData["normalTexture"]["index"];
                int imgIndex = (int)jsonTextures[texIndex]["source"];
                if (indexTextures.TryGetValue(imgIndex, out Texture2D t)) newMat.SetTexture("_BumpMap", t);
            }

            // --- EXTRAS PROCESSING ---
            JToken extras = matData["extras"];
            
            JToken GetExtra(string key, string fallbackKey = null) {
                if (extras != null && extras[key] != null) return extras[key];
                if (fallbackKey != null && extras != null && extras[fallbackKey] != null) return extras[fallbackKey];
                if (globalPivotData != null && globalPivotData[key] != null) return globalPivotData[key];
                return null;
            }

            void AssignTex(string shaderProp, string searchKey) {
                JToken val = GetExtra(searchKey);
                if (val == null) return;
                string search = val.ToString();
                if (nameTextures.TryGetValue(search, out Texture2D t)) { newMat.SetTexture(shaderProp, t); return; }
                foreach (var kvp in nameTextures) {
                    if (kvp.Key.Contains(search) || search.Contains(kvp.Key)) {
                        newMat.SetTexture(shaderProp, kvp.Value); return;
                    }
                }
            }

            void AssignFloat(string shaderProp, string jsonKey) {
                JToken val = GetExtra(jsonKey);
                if (val != null) newMat.SetFloat(shaderProp, (float)val);
            }
            
            void AssignVector(string shaderProp, string jsonKey) {
                JToken val = GetExtra(jsonKey);
                if (val != null) {
                    Vector4 v = ParseVector4(val);
                    newMat.SetVector(shaderProp, v);
                }
            }

            // 1. Basic Props
            if (extras != null) {
                if (extras["TargetColor"] != null) newMat.SetColor("_BaseColor", ParseColor(extras["TargetColor"]));
                AssignTex("_BaseMap", "DiffuseMap");
                AssignFloat("_BaseMult", "BaseMult");
                AssignFloat("_UTiling", "UTiling");
                AssignFloat("_VTiling", "VTiling");
                AssignFloat("_Twist", "Twist");
                
                if (extras["AlphaCutout"] != null && (bool)extras["AlphaCutout"]) {
                    newMat.SetFloat("_Cutoff", 0.5f); 
                    newMat.SetFloat("_AlphaCutoffEnable", 1.0f); 
                    newMat.EnableKeyword("_ALPHATEST_ON"); 
                    newMat.renderQueue = 2450;
                }
                if (extras["TwoSided"] != null && (bool)extras["TwoSided"]) {
                    newMat.SetFloat("_TwoSided", 1.0f); newMat.EnableKeyword("_TWOSIDED"); newMat.SetFloat("_Cull", 0);
                }
            }
            // --- NEW: BILLBOARD MATERIAL SETUP ---
                if (extras["IsBillboard"] != null && (bool)extras["IsBillboard"]) {
                    newMat.SetFloat("_Billboard", 1.0f);
                    newMat.EnableKeyword("_BILLBOARD");
                    newMat.SetFloat("_Cutoff", 0.5f); 
                    newMat.SetFloat("_AlphaCutoffEnable", 1.0f); 
                    newMat.EnableKeyword("_ALPHATEST_ON"); 
                    newMat.renderQueue = 2450;
                }

            // 2. Wind / Pivot Data
            if (useWind) {
                newMat.SetFloat("_UseWind", 1.0f);
                newMat.EnableKeyword("_WIND_ON");
                AssignTex("_PivotMap", "PivotMap"); 
            } else {
                newMat.SetFloat("_UseWind", 0.0f);
                newMat.DisableKeyword("_WIND_ON");
            }
            if (useGrow) {
                newMat.SetFloat("_UseGrow", 1.0f);
                newMat.SetFloat("_GrowPhase", 1.0f);
                newMat.SetFloat("_GrowMaskCutoff", 0.02f);
                newMat.SetFloat("_GrowRadiusWindow", 0.10f);
                newMat.SetFloat("_GrowTipLead", 0.0f);
                newMat.EnableKeyword("_GROW_ON");
                AssignTex("_PivotMap", "PivotMap");
            } else {
                newMat.SetFloat("_UseGrow", 0.0f);
                newMat.SetFloat("_GrowPhase", 1.0f);
                newMat.SetFloat("_GrowMaskCutoff", 0.02f);
                newMat.SetFloat("_GrowRadiusWindow", 0.10f);
                newMat.SetFloat("_GrowTipLead", 0.0f);
                newMat.DisableKeyword("_GROW_ON");
            }
            AssignFloat("_PivotCount", "PivotCount");
            AssignVector("_PivotMin", "PivotMin");
            AssignVector("_PivotRange", "PivotRange");

            // Wind Defaults & Overrides
            newMat.SetFloat("_Speed_Trunk", 1.0f); newMat.SetFloat("_Power_Trunk", 0.5f);
            newMat.SetFloat("_Speed_L1", 1.5f); newMat.SetFloat("_Power_L1", 0.2f);
            newMat.SetFloat("_Speed_L2", 3.0f); newMat.SetFloat("_Power_L2", 0.1f);
            newMat.SetFloat("_WindBaseLockStart", 0.25f);
            newMat.SetFloat("_WindBaseLockEnd", 2.50f);
            newMat.SetFloat("_WindL2AttachDebug", 0.0f);
            AssignFloat("_Speed_Trunk", "Speed_Trunk"); AssignFloat("_Power_Trunk", "Power_Trunk");
            
            // 3. Top Layer
            if (extras != null && extras["IsTopLayer"] != null && (bool)extras["IsTopLayer"]) {
                newMat.EnableKeyword("_TOPLAYER"); 
                newMat.SetFloat("_UseTopLayer", 1.0f);
                if (extras["TopColor"] != null) newMat.SetColor("_TopColor", ParseColor(extras["TopColor"]));
                AssignTex("_TopMap", "TopTexture");
                AssignTex("_TopBumpMap", "TopNormalMap");
                AssignFloat("_TopBaseMult", "TopBaseMult");                
                JToken noiseVal = GetExtra("TopNoiseScale");
                if (noiseVal != null) newMat.SetFloat("_TopNoiseScale", (float)noiseVal * 0.4f);
                else AssignFloat("_TopNoiseScale", "TopNoiseScale");             
                             
                JToken blendVal = GetExtra("TopBlendMax");
                if (blendVal != null) newMat.SetFloat("_TopBlendMax", (float)blendVal * 0.2f);
                else AssignFloat("_TopBlendMax", "TopBlendMax"); 

                AssignFloat("_TopOpacity", "TopOpacity");
                AssignFloat("_TopRepeatU", "TopRepeatU");
                AssignFloat("_TopRepeatV", "TopRepeatV");
            }
            
            // 4. Height Blend Layer
            if (extras != null && extras["IsHeightBlend"] != null && (bool)extras["IsHeightBlend"]) {
                newMat.EnableKeyword("_HEIGHTBLEND"); 
                newMat.SetFloat("_UseHeightBlend", 1.0f);
                if (extras["HeightColor"] != null) newMat.SetColor("_HeightColor", ParseColor(extras["HeightColor"]));
                AssignTex("_HeightMap", "HeightTexture");
                AssignTex("_HeightBumpMap", "HeightNormal");
                AssignFloat("_HeightHeight", "HeightHeight");
                AssignFloat("_HeightFade", "HeightFade");
                AssignFloat("_HeightBaseMult", "HeightBaseMult");
                AssignFloat("_HeightRepeatU", "HeightRepeatU");
                AssignFloat("_HeightRepeatV", "HeightRepeatV");
            }

            Texture pivotTex = newMat.GetTexture("_PivotMap");
            Texture2D pivotTex2D = pivotTex as Texture2D;
            string pivotInfo = pivotTex2D != null
                ? $"{pivotTex2D.name} {pivotTex2D.width}x{pivotTex2D.height}"
                : (pivotTex != null ? pivotTex.name : "NULL");
            VerboseLog($"[TreeGen] Material {finalMatName}: shader={newMat.shader.name}, grow={newMat.GetFloat("_UseGrow")}, wind={newMat.GetFloat("_UseWind")}, pivot={pivotInfo}, pivotCount={newMat.GetFloat("_PivotCount")}, keywords={string.Join(",", newMat.shaderKeywords)}");

            if (!materialAlreadyExists)
            {
                AssetDatabase.CreateAsset(newMat, matPath);
            }
            else
            {
                EditorUtility.SetDirty(newMat);
            }
            materials[rawMatName] = newMat;
        }
    }

    private void ApplyMaterials(GameObject root, Dictionary<string, Material> materials)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            Material[] currentMats = r.sharedMaterials;
            Material[] newMats = new Material[currentMats.Length];
            bool changed = false;

            for (int i = 0; i < currentMats.Length; i++)
            {
                Material original = currentMats[i];
                if (original == null) { newMats[i] = null; continue; }

                string searchName = original.name;
                foreach (char c in Path.GetInvalidFileNameChars()) searchName = searchName.Replace(c, '_');

                if (materials.TryGetValue(searchName, out Material match)) {
                    newMats[i] = match; changed = true;
                } else {
                    foreach (var kvp in materials) {
                        if (searchName.Contains(kvp.Key) || kvp.Key.Contains(searchName)) {
                            newMats[i] = kvp.Value; changed = true; break;
                        }
                    }
                    if (newMats[i] == null) newMats[i] = original;
                }
            }
            if (changed) r.sharedMaterials = newMats;
        }
    }

    private void SetupLODGroup(GameObject root) {
        Dictionary<string, List<Renderer>> groups = new Dictionary<string, List<Renderer>>();
        foreach (var r in root.GetComponentsInChildren<Renderer>()) {
            if (r.name.Contains("_LOD")) {
                string baseName = r.name.Substring(0, r.name.LastIndexOf("_LOD"));
                if (!groups.ContainsKey(baseName)) groups[baseName] = new List<Renderer>();
                groups[baseName].Add(r);
            }
        }
        foreach (var kvp in groups) {
            var list = kvp.Value.OrderBy(x => x.name).ToList();

            if (useGrow && useLod0OnlyForGrowth)
            {
                Renderer lod0 = list.FirstOrDefault(r => r.name.Contains("_LOD0"));
                if (lod0 == null && list.Count > 0) lod0 = list[0];

                foreach (Renderer r in list)
                {
                    if (r != lod0) r.enabled = false;
                }

                if (lod0 == null) continue;

                GameObject p0 = lod0.transform.parent.gameObject;
                LODGroup g0 = p0.GetComponent<LODGroup>();
                if (!g0) g0 = p0.AddComponent<LODGroup>();
                g0.fadeMode = LODFadeMode.None;
                g0.animateCrossFading = false;
                g0.SetLODs(new LOD[] { new LOD(0.01f, new Renderer[] { lod0 }) });
                g0.RecalculateBounds();
                VerboseLog($"[TreeGen] Growth LOD safety: using only {lod0.name}. Lower LOD renderers are disabled because coarse LOD triangles stretch during growth.");
                continue;
            }
            
            // --- NEW: BILLBOARD FILTERING & CLEANUP ---
            if (!useBillboardLod) {
                var billboards = list.Where(r => r.name.Contains("Billboard")).ToList();
                foreach(var b in billboards) {
                    UnityEngine.Object.DestroyImmediate(b.gameObject);
                    list.Remove(b);
                }
            }
            
            if (list.Count == 0) continue;
            GameObject p = list[0].transform.parent.gameObject;
            LODGroup g = p.GetComponent<LODGroup>();
            if (!g) g = p.AddComponent<LODGroup>();
            
            LOD[] lods = new LOD[list.Count];
            for (int i = 0; i < list.Count; i++) {
                bool isBillboard = list[i].name.Contains("Billboard");
                float screenHeight = 0.01f;
                
                // Fine-tune LOD transition distances
                if (isBillboard) {
                    screenHeight = 0.01f; // Billboard stays visible until very far away
                } else if (i == list.Count - 1) {
                    // If this is the last actual mesh BEFORE the billboard, transition at 10%
                    screenHeight = (useBillboardLod && list.Any(r => r.name.Contains("Billboard"))) ? 0.1f : 0.01f;
                } else {
                    screenHeight = 0.6f / (i + 1);
                }
                
                lods[i] = new LOD(screenHeight, new Renderer[]{list[i]});
                
                // --- FIX: Stretch the cross-fade transition band (0.0 to 1.0) ---
                // Setting this to 0.5f forces the fade to last across 50% of the LOD's depth range
                lods[i].fadeTransitionWidth = 0.5f; 
            }
            
            // --- FIX: Enable smooth dithered crossfading between LODs ---
            g.fadeMode = LODFadeMode.CrossFade;
            g.animateCrossFading = false;
            
            g.SetLODs(lods); g.RecalculateBounds();
        }
    }

    private void GenerateColliders(GameObject root, JObject json) {
        JArray nodes = (JArray)json["nodes"];
        if (nodes == null) return;
        JToken colData = nodes.FirstOrDefault(n => n["extras"]?["ParametricCollision"] != null);
        if (colData == null) return;

        GameObject colRoot = new GameObject("Colliders");
        colRoot.transform.SetParent(root.transform, false);

        foreach (JToken col in (JArray)colData["extras"]["ParametricCollision"]) {
            GameObject c = new GameObject(col["type"]?.ToString());
            c.transform.SetParent(colRoot.transform, false);

            Vector3 p = ParseVector3(col["pos"]);
            Quaternion r = ParseQuaternion(col["rot"]);

            Vector3 pos = new Vector3(-p.x, p.y, p.z);
            Quaternion rot = new Quaternion(r.x, -r.y, -r.z, r.w);

            pos = new Vector3(
                pos.x * collisionAxisMultiplier.x,
                pos.y * collisionAxisMultiplier.y,
                pos.z * collisionAxisMultiplier.z
            );

            rot = new Quaternion(
                rot.x * collisionAxisMultiplier.x,
                rot.y * collisionAxisMultiplier.y,
                rot.z * collisionAxisMultiplier.z,
                rot.w 
            );

            c.transform.localPosition = pos;
            c.transform.localRotation = rot;

            CapsuleCollider cc = c.AddComponent<CapsuleCollider>();
            cc.radius = (float)col["radius"];
            cc.height = (float)col["length"] + cc.radius*2;
            cc.direction = 1;
        }
    }

    private Color ParseColor(JToken t) { float[] v = t.ToObject<float[]>(); return new Color(v[0],v[1],v[2],1); }
    private Vector3 ParseVector3(JToken t) { float[] v = t.ToObject<float[]>(); return new Vector3(v[0],v[1],v[2]); }
    private Vector4 ParseVector4(JToken t) { float[] v = t.ToObject<float[]>(); return new Vector4(v[0],v[1],v[2],0); }
    private Quaternion ParseQuaternion(JToken t) { float[] v = t.ToObject<float[]>(); return new Quaternion(v[0],v[1],v[2],v[3]); }

    // --- NEW: Robust Search Function ---
    private Shader FindTreeShader(string shaderName)
    {
        // 1. Try standard lookup (Fastest, works if shader is already loaded)
        Shader s = Shader.Find(shaderName);
        if (s != null) return s;

        // 2. Deep Search in Project (Slower, but finds files in any subfolder)
        // We strip the path to get just the name part (e.g. "URP_Wind_Complex")
        string simpleName = shaderName.Substring(shaderName.LastIndexOf('/') + 1);
        
        // FindAssets searches all project folders for "t:Shader Name"
        // Note: FindAssets doesn't take extension, but taking just name is generally safer
        string[] guids = AssetDatabase.FindAssets($"t:Shader {simpleName}");
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Shader candidate = AssetDatabase.LoadAssetAtPath<Shader>(path);
            
            // Check if the internal name matches exactly what we need
            if (candidate != null && candidate.name == shaderName)
            {
                return candidate;
            }
        }

        // 3. Fallback if absolutely nothing is found
        Debug.LogWarning($"[TreeGen] Shader '{shaderName}' not found in project (checked subfolders). Using Standard/Lit fallback.");
        
        if (selectedPipeline == RenderPipelineType.URP) return Shader.Find("Universal Render Pipeline/Lit");
        if (selectedPipeline == RenderPipelineType.HD) return Shader.Find("HDRP/Lit");
        return Shader.Find("Standard");
    }
}
