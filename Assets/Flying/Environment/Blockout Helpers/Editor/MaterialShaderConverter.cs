using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Crease.Flying.Environment.BlockoutHelpers.Editor
{
    /// <summary>
    /// Editor utility to convert materials from Unlit/Color shader to URP shaders while preserving colors.
    /// </summary>
    public class MaterialShaderConverter : EditorWindow
    {
        private string _sourceShaderName = "Unlit/Color";
        private string _targetShaderName = "Universal Render Pipeline/Unlit";
        private string _sourceColorProperty = "_Color";
        private string _targetColorProperty = "_BaseColor";

        private bool _searchInSelection = false;
        private bool _createBackup = true;

        private List<Material> _foundMaterials = new List<Material>();
        private Vector2 _scrollPosition;

        [MenuItem("Tools/Material Shader Converter")]
        public static void ShowWindow()
        {
            GetWindow<MaterialShaderConverter>("Material Shader Converter");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Material Shader Converter", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox("This tool converts materials from one shader to another while preserving color properties.", MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Source Shader Settings", EditorStyles.boldLabel);
            _sourceShaderName = EditorGUILayout.TextField("Source Shader", _sourceShaderName);
            _sourceColorProperty = EditorGUILayout.TextField("Color Property Name", _sourceColorProperty);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Target Shader Settings", EditorStyles.boldLabel);
            _targetShaderName = EditorGUILayout.TextField("Target Shader", _targetShaderName);
            _targetColorProperty = EditorGUILayout.TextField("Color Property Name", _targetColorProperty);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _searchInSelection = EditorGUILayout.Toggle("Search in Selection Only", _searchInSelection);
            _createBackup = EditorGUILayout.Toggle("Create Backup (Undo)", _createBackup);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Find Materials", GUILayout.Height(30)))
            {
                FindMaterials();
            }

            if (_foundMaterials.Count > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField($"Found {_foundMaterials.Count} material(s):", EditorStyles.boldLabel);

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
                foreach (Material mat in _foundMaterials)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(mat, typeof(Material), false);

                    if (mat.HasProperty(_sourceColorProperty))
                    {
                        Color color = mat.GetColor(_sourceColorProperty);
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ColorField(GUIContent.none, color, GUILayout.Width(50));
                        EditorGUI.EndDisabledGroup();
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(10);

                if (GUILayout.Button($"Convert {_foundMaterials.Count} Material(s)", GUILayout.Height(35)))
                {
                    ConvertMaterials();
                }
            }
        }

        private void FindMaterials()
        {
            _foundMaterials.Clear();

            List<Material> allMaterials = new List<Material>();

            if (_searchInSelection)
            {
                foreach (GameObject obj in Selection.gameObjects)
                {
                    Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer renderer in renderers)
                    {
                        allMaterials.AddRange(renderer.sharedMaterials.Where(m => m != null));
                    }
                }
            }
            else
            {
                string[] guids = AssetDatabase.FindAssets("t:Material");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat != null)
                    {
                        allMaterials.Add(mat);
                    }
                }
            }

            Shader sourceShader = Shader.Find(_sourceShaderName);
            if (sourceShader == null)
            {
                EditorUtility.DisplayDialog("Error", $"Source shader '{_sourceShaderName}' not found!", "OK");
                return;
            }

            _foundMaterials = allMaterials
                .Where(m => m.shader == sourceShader)
                .Distinct()
                .ToList();

            Debug.Log($"Found {_foundMaterials.Count} materials using shader '{_sourceShaderName}'");
        }

        private void ConvertMaterials()
        {
            Shader targetShader = Shader.Find(_targetShaderName);
            if (targetShader == null)
            {
                EditorUtility.DisplayDialog("Error", $"Target shader '{_targetShaderName}' not found!", "OK");
                return;
            }

            int successCount = 0;
            int failCount = 0;

            foreach (Material mat in _foundMaterials)
            {
                try
                {
                    Color savedColor = Color.white;
                    bool hasColor = mat.HasProperty(_sourceColorProperty);
                    if (hasColor)
                    {
                        savedColor = mat.GetColor(_sourceColorProperty);
                    }

                    if (_createBackup)
                    {
                        Undo.RecordObject(mat, "Convert Material Shader");
                    }

                    mat.shader = targetShader;

                    if (hasColor && mat.HasProperty(_targetColorProperty))
                    {
                        mat.SetColor(_targetColorProperty, savedColor);
                    }

                    EditorUtility.SetDirty(mat);

                    successCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to convert material '{mat.name}': {e.Message}", mat);
                    failCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message = $"Conversion complete!\n\nSuccessfully converted: {successCount}\nFailed: {failCount}";
            EditorUtility.DisplayDialog("Material Shader Converter", message, "OK");

            Debug.Log($"[MaterialShaderConverter] Converted {successCount} materials, {failCount} failed");

            _foundMaterials.Clear();
        }
    }
}
