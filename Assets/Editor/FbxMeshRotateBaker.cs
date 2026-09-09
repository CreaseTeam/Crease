using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

namespace Crease.Editor
{
    public class FbxMeshRotateBakerWindow : EditorWindow
    {
        const string RotationPrefix = "Crease.MeshBakeRotation=";
        const string ScalePrefix = "Crease.MeshBakeScale=";
        static readonly Regex RotationPattern = new Regex(
            @"Crease\.MeshBakeRotation=(-?[0-9.]+),(-?[0-9.]+),(-?[0-9.]+)",
            RegexOptions.Compiled);
        static readonly Regex ScalePattern = new Regex(
            @"Crease\.MeshBakeScale=(-?[0-9.]+),(-?[0-9.]+),(-?[0-9.]+)",
            RegexOptions.Compiled);

        Object _fbx;
        Vector3 _euler;
        Vector3 _scale = Vector3.one;
        bool _applyToFolder;

        [MenuItem("Crease/Tools/FBX Mesh Rotate Baker")]
        public static void Open()
        {
            var window = GetWindow<FbxMeshRotateBakerWindow>("FBX Mesh Rotate Baker");
            window.TryAssignFromSelection();
        }

        [MenuItem("Assets/Crease/FBX Mesh Rotate Baker", true)]
        static bool ValidateOpenFromAssets()
        {
            return GetSelectedFbxPaths().Count > 0;
        }

        [MenuItem("Assets/Crease/FBX Mesh Rotate Baker")]
        static void OpenFromAssets()
        {
            Open();
        }

        void OnSelectionChange()
        {
            if (_fbx == null)
                TryAssignFromSelection();
            Repaint();
        }

        void TryAssignFromSelection()
        {
            List<string> paths = GetSelectedFbxPaths();
            if (paths.Count == 0)
                return;

            _fbx = AssetDatabase.LoadMainAssetAtPath(paths[0]);
            LoadStoredBake(paths[0]);
        }

        void LoadStoredBake(string path)
        {
            TryReadStoredBake(path, out _euler, out _scale);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("FBX Mesh Rotate Baker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bakes rotation and scale into the imported FBX mesh. " +
                "The source FBX bytes are unchanged; the transform is stored on the importer and applied only when that FBX is imported, not on scene load.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _fbx = EditorGUILayout.ObjectField("FBX", _fbx, typeof(Object), false);
            if (EditorGUI.EndChangeCheck() && TryGetFbxPath(_fbx, out string changedPath))
                LoadStoredBake(changedPath);

            if (!TryGetFbxPath(_fbx, out string fbxPath))
            {
                EditorGUILayout.HelpBox("Assign an FBX asset.", MessageType.Warning);
                return;
            }

            if (HasStoredBake(fbxPath))
            {
                TryReadStoredBake(fbxPath, out Vector3 bakedEuler, out Vector3 bakedScale);
                EditorGUILayout.LabelField("Currently baked", FormatBake(bakedEuler, bakedScale));
            }
            else
            {
                EditorGUILayout.LabelField("Currently baked", "None");
            }

            EditorGUILayout.Space(6);
            _euler = EditorGUILayout.Vector3Field("Rotation (Euler)", _euler);
            DrawAxisButtons();

            EditorGUILayout.Space(6);
            _scale = EditorGUILayout.Vector3Field("Scale", _scale);
            DrawScaleButtons();

            EditorGUILayout.Space(6);
            _applyToFolder = EditorGUILayout.ToggleLeft(
                "Apply to all FBX files in the same folder",
                _applyToFolder);

            bool isIdentity = IsIdentity(_euler, _scale);
            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(isIdentity && !HasStoredBake(fbxPath)))
            {
                if (GUILayout.Button("Bake", GUILayout.Height(28)))
                    Bake(fbxPath, _euler, _scale, _applyToFolder);
            }

            if (HasStoredBake(fbxPath) && GUILayout.Button("Clear Bake"))
                Bake(fbxPath, Vector3.zero, Vector3.one, _applyToFolder);
        }

        void DrawAxisButtons()
        {
            DrawAxisRow("X", new Vector3(90f, 0f, 0f));
            DrawAxisRow("Y", new Vector3(0f, 90f, 0f));
            DrawAxisRow("Z", new Vector3(0f, 0f, 90f));
        }

        void DrawAxisRow(string axis, Vector3 step)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(axis, GUILayout.Width(16));
            if (GUILayout.Button("+90"))
                _euler += step;
            if (GUILayout.Button("-90"))
                _euler -= step;
            EditorGUILayout.EndHorizontal();
        }

        void DrawScaleButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Scale"))
                _scale = Vector3.one;
            if (GUILayout.Button("Uniform from X"))
                _scale = new Vector3(_scale.x, _scale.x, _scale.x);
            EditorGUILayout.EndHorizontal();
        }

        static void Bake(string fbxPath, Vector3 euler, Vector3 scale, bool applyToFolder)
        {
            if (HasZeroScale(scale))
            {
                EditorUtility.DisplayDialog(
                    "FBX Mesh Rotate Baker",
                    "Scale cannot have a zero component.",
                    "OK");
                return;
            }

            List<string> paths = applyToFolder
                ? GetFbxPathsInFolder(Path.GetDirectoryName(fbxPath))
                : new List<string> { fbxPath };

            if (paths.Count == 0)
                return;

            if (IsIdentity(euler, scale) &&
                !EditorUtility.DisplayDialog(
                    "Clear Mesh Bake",
                    applyToFolder
                        ? $"Clear baked rotation and scale from {paths.Count} FBX file(s) in this folder?"
                        : "Clear the baked rotation and scale from this FBX?",
                    "Clear",
                    "Cancel"))
            {
                return;
            }

            int updated = 0;
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    EditorUtility.DisplayProgressBar(
                        "FBX Mesh Rotate Baker",
                        path,
                        (float)i / paths.Count);

                    if (WriteBake(path, euler, scale))
                        updated++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                    AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            string action = IsIdentity(euler, scale)
                ? "Cleared"
                : $"Baked {FormatBake(euler, scale)} into";
            Debug.Log($"[FBX Mesh Rotate Baker] {action} {updated} FBX file(s).");
        }

        static bool WriteBake(string path, Vector3 euler, Vector3 scale)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return false;

            CheckoutIfExists(path + ".meta");
            importer.userData = SetUserData(importer.userData, euler, scale);
            AssetDatabase.WriteImportSettingsIfDirty(path);
            return true;
        }

        internal static bool TryGetBake(string assetPath, out Quaternion rotation, out Vector3 scale)
        {
            rotation = Quaternion.identity;
            scale = Vector3.one;
            if (!TryReadStoredBake(assetPath, out Vector3 euler, out scale))
                return false;

            rotation = Quaternion.Euler(euler);
            return !IsIdentity(euler, scale);
        }

        internal static void BakeMesh(Mesh mesh, Quaternion rotation, Vector3 scale)
        {
            if (rotation == Quaternion.identity && scale == Vector3.one)
                return;

            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, rotation, scale);

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            mesh.vertices = vertices;

            Vector3[] normals = mesh.normals;
            if (normals != null && normals.Length == vertices.Length)
            {
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                for (int i = 0; i < normals.Length; i++)
                    normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                mesh.normals = normals;
            }

            Vector4[] tangents = mesh.tangents;
            float determinant = scale.x * scale.y * scale.z;
            if (tangents != null && tangents.Length == vertices.Length)
            {
                for (int i = 0; i < tangents.Length; i++)
                {
                    Vector3 tangent = matrix.MultiplyVector(
                        new Vector3(tangents[i].x, tangents[i].y, tangents[i].z));
                    if (tangent.sqrMagnitude > 0f)
                        tangent.Normalize();
                    float w = determinant < 0f ? -tangents[i].w : tangents[i].w;
                    tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, w);
                }
                mesh.tangents = tangents;
            }

            if (determinant < 0f)
                FlipWinding(mesh);

            mesh.RecalculateBounds();
        }

        static void FlipWinding(Mesh mesh)
        {
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] triangles = mesh.GetTriangles(subMesh);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int swap = triangles[i + 1];
                    triangles[i + 1] = triangles[i + 2];
                    triangles[i + 2] = swap;
                }

                mesh.SetTriangles(triangles, subMesh);
            }
        }

        static bool TryReadStoredBake(string assetPath, out Vector3 euler, out Vector3 scale)
        {
            euler = Vector3.zero;
            scale = Vector3.one;
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
                return false;

            bool hasRotation = TryParseVector3(RotationPattern, importer.userData, out euler);
            bool hasScale = TryParseVector3(ScalePattern, importer.userData, out scale);
            if (!hasScale)
                scale = Vector3.one;
            return hasRotation || hasScale;
        }

        static bool HasStoredBake(string assetPath)
        {
            return TryReadStoredBake(assetPath, out Vector3 euler, out Vector3 scale) &&
                   !IsIdentity(euler, scale);
        }

        static bool TryParseVector3(Regex pattern, string userData, out Vector3 value)
        {
            value = Vector3.zero;
            if (string.IsNullOrEmpty(userData))
                return false;

            Match match = pattern.Match(userData);
            if (!match.Success)
                return false;

            value = new Vector3(
                float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
            return true;
        }

        static string SetUserData(string userData, Vector3 euler, Vector3 scale)
        {
            userData = ClearToken(userData, RotationPattern);
            userData = ClearToken(userData, ScalePattern);

            if (euler != Vector3.zero)
                userData = AppendToken(userData, RotationPrefix + FormatVector3(euler));
            if (scale != Vector3.one)
                userData = AppendToken(userData, ScalePrefix + FormatVector3(scale));
            return userData;
        }

        static string ClearToken(string userData, Regex pattern)
        {
            if (string.IsNullOrEmpty(userData))
                return string.Empty;
            return pattern.Replace(userData, string.Empty).Trim();
        }

        static string AppendToken(string userData, string token)
        {
            if (string.IsNullOrEmpty(userData))
                return token;
            return userData + " " + token;
        }

        static bool IsIdentity(Vector3 euler, Vector3 scale)
        {
            return euler == Vector3.zero && scale == Vector3.one;
        }

        static bool HasZeroScale(Vector3 scale)
        {
            return Mathf.Approximately(scale.x, 0f) ||
                   Mathf.Approximately(scale.y, 0f) ||
                   Mathf.Approximately(scale.z, 0f);
        }

        static string FormatBake(Vector3 euler, Vector3 scale)
        {
            if (euler != Vector3.zero && scale != Vector3.one)
                return $"rot {FormatVector3(euler)} scale {FormatVector3(scale)}";
            if (scale != Vector3.one)
                return $"scale {FormatVector3(scale)}";
            return $"rot {FormatVector3(euler)}";
        }

        static string FormatVector3(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.###},{1:0.###},{2:0.###}",
                value.x,
                value.y,
                value.z);
        }

        static bool TryGetFbxPath(Object asset, out string path)
        {
            path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            return IsFbxPath(path);
        }

        static bool IsFbxPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        }

        static List<string> GetSelectedFbxPaths()
        {
            var paths = new List<string>();
            Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selected[i]);
                if (IsFbxPath(path) && !paths.Contains(path))
                    paths.Add(path);
            }

            return paths;
        }

        static List<string> GetFbxPathsInFolder(string folder)
        {
            var paths = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return paths;

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder.Replace('\\', '/') });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsFbxPath(path) || paths.Contains(path))
                    continue;
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != folder.Replace('\\', '/'))
                    continue;
                paths.Add(path);
            }

            return paths;
        }

        static void CheckoutIfExists(string path)
        {
            if (!File.Exists(path))
                return;

            AssetDatabase.MakeEditable(path);
            if (!Provider.enabled)
                return;

            Asset asset = Provider.GetAssetByPath(path);
            if (asset == null)
                return;

            var assets = new AssetList { asset };
            if (Provider.CheckoutIsValid(assets))
                Provider.Checkout(assets, CheckoutMode.Both).Wait();
        }
    }

    class FbxMeshRotateBakerPostprocessor : AssetPostprocessor
    {
        void OnPostprocessModel(GameObject root)
        {
            if (!FbxMeshRotateBakerWindow.TryGetBake(assetPath, out Quaternion rotation, out Vector3 scale))
                return;

            var seen = new HashSet<Mesh>();
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
                BakeIfNew(filters[i].sharedMesh, rotation, scale, seen);

            SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
                BakeIfNew(skinned[i].sharedMesh, rotation, scale, seen);
        }

        static void BakeIfNew(Mesh mesh, Quaternion rotation, Vector3 scale, HashSet<Mesh> seen)
        {
            if (mesh == null || !seen.Add(mesh))
                return;

            FbxMeshRotateBakerWindow.BakeMesh(mesh, rotation, scale);
        }
    }
}
