using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

namespace Crease.Flying.Environment.Editor
{
    [CustomEditor(typeof(TunnelSection))]
    public class TunnelSectionEditor : UnityEditor.Editor
    {
        static readonly Regex NamePattern = new Regex(
            @"^(?<name>.+)_(?<letter>[A-Za-z])_(?<number>\d+)$",
            RegexOptions.Compiled);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Auto-Detect Pairs", GUILayout.Height(30)))
            {
                DetectPairs((TunnelSection)target);
                serializedObject.Update();
            }

            if (GUILayout.Button("Generate Prefabs", GUILayout.Height(30)))
                GeneratePrefabs((TunnelSection)target);
        }

        static void GeneratePrefabs(TunnelSection section)
        {
            if (!Validate(section, out string folder))
                return;

            int created = 0;
            for (int i = 0; i < section.Pairs.Count; i++)
            {
                if (CreateVariant(section, folder, i))
                    created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TunnelSection] Generated {created} prefab variant(s) in {folder}", section);
        }

        static void DetectPairs(TunnelSection section)
        {
            string assetPath = AssetDatabase.GetAssetPath(section);
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                EditorUtility.DisplayDialog("Auto-Detect Pairs", "Could not resolve this asset's folder.", "OK");
                return;
            }

            if (section.Pairs != null && section.Pairs.Count > 0 &&
                !EditorUtility.DisplayDialog("Auto-Detect Pairs",
                    "Replace the current pair list with meshes and materials found in this folder?",
                    "Replace", "Cancel"))
            {
                return;
            }

            var entries = new Dictionary<(char Letter, int Number), DetectedPair>();
            foreach (string file in Directory.GetFiles(folder))
            {
                string extension = Path.GetExtension(file);
                bool isMaterial = extension.Equals(".mat", StringComparison.OrdinalIgnoreCase);
                bool isMeshFile = extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".obj", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".mesh", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".asset", StringComparison.OrdinalIgnoreCase);
                if (!isMaterial && !isMeshFile)
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(file);
                Match match = NamePattern.Match(fileName);
                if (!match.Success)
                    continue;

                char letter = char.ToUpperInvariant(match.Groups["letter"].Value[0]);
                int number = int.Parse(match.Groups["number"].Value);
                (char Letter, int Number) key = (letter, number);
                if (!entries.TryGetValue(key, out DetectedPair entry))
                {
                    entry = new DetectedPair();
                    entries.Add(key, entry);
                }

                string detectedPath = $"{folder}/{Path.GetFileName(file)}";
                if (isMaterial)
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(detectedPath);
                    if (material != null)
                        entry.Material = material;
                    continue;
                }

                Mesh mesh = LoadMesh(detectedPath);
                if (mesh != null)
                    entry.Mesh = mesh;
            }

            var ordered = new List<KeyValuePair<(char Letter, int Number), DetectedPair>>(entries);
            ordered.Sort((a, b) =>
            {
                int letterCompare = a.Key.Letter.CompareTo(b.Key.Letter);
                return letterCompare != 0 ? letterCompare : a.Key.Number.CompareTo(b.Key.Number);
            });

            var pairs = new List<TunnelSectionPair>(ordered.Count);
            int incomplete = 0;
            foreach (KeyValuePair<(char Letter, int Number), DetectedPair> item in ordered)
            {
                if (item.Value.Mesh == null || item.Value.Material == null)
                {
                    incomplete++;
                    Debug.LogWarning(
                        $"[TunnelSection] Incomplete pair {item.Key.Letter}_{item.Key.Number} in {folder}.",
                        section);
                }

                pairs.Add(new TunnelSectionPair
                {
                    Mesh = item.Value.Mesh,
                    Material = item.Value.Material
                });
            }

            Undo.RecordObject(section, "Auto-Detect Tunnel Section Pairs");
            section.Pairs = pairs;
            EditorUtility.SetDirty(section);

            string message = $"Detected {pairs.Count} pair(s) in {folder}.";
            if (incomplete > 0)
                message += $"\n{incomplete} pair(s) are missing a mesh or material.";
            EditorUtility.DisplayDialog("Auto-Detect Pairs", message, "OK");
        }

        static Mesh LoadMesh(string assetPath)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh != null)
                return mesh;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Mesh loaded)
                    return loaded;
            }

            return null;
        }

        class DetectedPair
        {
            public Mesh Mesh;
            public Material Material;
        }

        static bool Validate(TunnelSection section, out string folder)
        {
            folder = null;

            if (section.BasePrefab == null)
            {
                EditorUtility.DisplayDialog("Generate Prefabs", "Assign a Base Prefab first.", "OK");
                return false;
            }

            if (!EditorUtility.IsPersistent(section.BasePrefab) ||
                PrefabUtility.GetPrefabAssetType(section.BasePrefab) == PrefabAssetType.NotAPrefab)
            {
                EditorUtility.DisplayDialog("Generate Prefabs", "Base Prefab must be a prefab asset.", "OK");
                return false;
            }

            if (string.IsNullOrWhiteSpace(section.BaseName))
            {
                EditorUtility.DisplayDialog("Generate Prefabs", "Assign a Base Name first.", "OK");
                return false;
            }

            if (section.GroupSize < 1)
            {
                EditorUtility.DisplayDialog("Generate Prefabs", "Group Size must be at least 1.", "OK");
                return false;
            }

            if (section.Pairs == null || section.Pairs.Count == 0)
            {
                EditorUtility.DisplayDialog("Generate Prefabs", "Add at least one mesh/material pair.", "OK");
                return false;
            }

            int groupCount = (section.Pairs.Count + section.GroupSize - 1) / section.GroupSize;
            if (groupCount > 26)
            {
                EditorUtility.DisplayDialog("Generate Prefabs",
                    "Too many groups for A–Z naming. Reduce the pair count or increase Group Size.", "OK");
                return false;
            }

            for (int i = 0; i < section.Pairs.Count; i++)
            {
                TunnelSectionPair pair = section.Pairs[i];
                if (pair.Mesh == null || pair.Material == null)
                {
                    EditorUtility.DisplayDialog("Generate Prefabs",
                        $"Pair {i} is missing a mesh or material.", "OK");
                    return false;
                }
            }

            string assetPath = AssetDatabase.GetAssetPath(section);
            folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
            {
                EditorUtility.DisplayDialog("Generate Prefabs", "Could not resolve this asset's folder.", "OK");
                return false;
            }

            return true;
        }

        static bool CreateVariant(TunnelSection section, string folder, int index)
        {
            string prefabName = section.GetPrefabName(index);
            string savePath = $"{folder}/{prefabName}.prefab";
            TunnelSectionPair pair = section.Pairs[index];

            CheckoutIfExists(savePath);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(section.BasePrefab);
            try
            {
                instance.name = prefabName;

                MeshFilter meshFilter = instance.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = instance.GetComponent<MeshRenderer>();
                MeshCollider meshCollider = instance.GetComponent<MeshCollider>();
                if (meshFilter == null || meshRenderer == null || meshCollider == null)
                {
                    Debug.LogError(
                        $"[TunnelSection] '{section.BasePrefab.name}' needs a MeshFilter, MeshRenderer, and MeshCollider on the root.",
                        section);
                    return false;
                }

                meshFilter.sharedMesh = pair.Mesh;
                meshCollider.sharedMesh = pair.Mesh;
                meshRenderer.sharedMaterial = pair.Material;

                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                PrefabUtility.RecordPrefabInstancePropertyModifications(meshFilter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(meshCollider);
                PrefabUtility.RecordPrefabInstancePropertyModifications(meshRenderer);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, savePath);
                if (saved == null)
                {
                    Debug.LogError($"[TunnelSection] Failed to save {savePath}", section);
                    return false;
                }

                AddToVersionControl(savePath);
                AddToVersionControl(savePath + ".meta");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        static void CheckoutIfExists(string path)
        {
            if (!File.Exists(path))
                return;

            AssetDatabase.MakeEditable(path);
            string metaPath = path + ".meta";
            if (File.Exists(metaPath))
                AssetDatabase.MakeEditable(metaPath);
        }

        static void AddToVersionControl(string path)
        {
            if (!Provider.enabled || !File.Exists(path))
                return;

            Asset asset = Provider.GetAssetByPath(path);
            if (asset == null)
                return;

            var assets = new AssetList { asset };
            if (Provider.AddIsValid(assets))
                Provider.Add(assets, false).Wait();
        }
    }
}
