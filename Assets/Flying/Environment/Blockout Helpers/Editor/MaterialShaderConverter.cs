using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor utility to convert materials from Unlit/Color shader to URP shaders while preserving colors.
/// </summary>
public class MaterialShaderConverter : EditorWindow
{
    private string sourceShaderName = "Unlit/Color";
    private string targetShaderName = "Universal Render Pipeline/Unlit";
    private string sourceColorProperty = "_Color";
    private string targetColorProperty = "_BaseColor";
    
    private bool searchInSelection = false;
    private bool createBackup = true;
    
    private List<Material> foundMaterials = new List<Material>();
    private Vector2 scrollPosition;
    
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
        
        // Source shader settings
        EditorGUILayout.LabelField("Source Shader Settings", EditorStyles.boldLabel);
        sourceShaderName = EditorGUILayout.TextField("Source Shader", sourceShaderName);
        sourceColorProperty = EditorGUILayout.TextField("Color Property Name", sourceColorProperty);
        EditorGUILayout.Space(5);
        
        // Target shader settings
        EditorGUILayout.LabelField("Target Shader Settings", EditorStyles.boldLabel);
        targetShaderName = EditorGUILayout.TextField("Target Shader", targetShaderName);
        targetColorProperty = EditorGUILayout.TextField("Color Property Name", targetColorProperty);
        EditorGUILayout.Space(5);
        
        // Options
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        searchInSelection = EditorGUILayout.Toggle("Search in Selection Only", searchInSelection);
        createBackup = EditorGUILayout.Toggle("Create Backup (Undo)", createBackup);
        EditorGUILayout.Space(10);
        
        // Find materials button
        if (GUILayout.Button("Find Materials", GUILayout.Height(30)))
        {
            FindMaterials();
        }
        
        // Display found materials
        if (foundMaterials.Count > 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Found {foundMaterials.Count} material(s):", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            foreach (Material mat in foundMaterials)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(mat, typeof(Material), false);
                
                if (mat.HasProperty(sourceColorProperty))
                {
                    Color color = mat.GetColor(sourceColorProperty);
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ColorField(GUIContent.none, color, GUILayout.Width(50));
                    EditorGUI.EndDisabledGroup();
                }
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            
            // Convert button
            if (GUILayout.Button($"Convert {foundMaterials.Count} Material(s)", GUILayout.Height(35)))
            {
                ConvertMaterials();
            }
        }
    }
    
    private void FindMaterials()
    {
        foundMaterials.Clear();
        
        List<Material> allMaterials = new List<Material>();
        
        if (searchInSelection)
        {
            // Search in selected objects
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
            // Search all materials in project
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
        
        // Filter by source shader
        Shader sourceShader = Shader.Find(sourceShaderName);
        if (sourceShader == null)
        {
            EditorUtility.DisplayDialog("Error", $"Source shader '{sourceShaderName}' not found!", "OK");
            return;
        }
        
        foundMaterials = allMaterials
            .Where(m => m.shader == sourceShader)
            .Distinct()
            .ToList();
        
        Debug.Log($"Found {foundMaterials.Count} materials using shader '{sourceShaderName}'");
    }
    
    private void ConvertMaterials()
    {
        Shader targetShader = Shader.Find(targetShaderName);
        if (targetShader == null)
        {
            EditorUtility.DisplayDialog("Error", $"Target shader '{targetShaderName}' not found!", "OK");
            return;
        }
        
        int successCount = 0;
        int failCount = 0;
        
        foreach (Material mat in foundMaterials)
        {
            try
            {
                // Save current color if property exists
                Color savedColor = Color.white;
                bool hasColor = mat.HasProperty(sourceColorProperty);
                if (hasColor)
                {
                    savedColor = mat.GetColor(sourceColorProperty);
                }
                
                // Record for undo
                if (createBackup)
                {
                    Undo.RecordObject(mat, "Convert Material Shader");
                }
                
                // Change shader
                mat.shader = targetShader;
                
                // Apply saved color to new property if it exists
                if (hasColor && mat.HasProperty(targetColorProperty))
                {
                    mat.SetColor(targetColorProperty, savedColor);
                }
                
                // Mark as dirty
                EditorUtility.SetDirty(mat);
                
                successCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to convert material '{mat.name}': {e.Message}", mat);
                failCount++;
            }
        }
        
        // Save assets
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Show results
        string message = $"Conversion complete!\n\nSuccessfully converted: {successCount}\nFailed: {failCount}";
        EditorUtility.DisplayDialog("Material Shader Converter", message, "OK");
        
        Debug.Log($"[MaterialShaderConverter] Converted {successCount} materials, {failCount} failed");
        
        // Clear the list
        foundMaterials.Clear();
    }
}
