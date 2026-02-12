using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AddMeshColliders))]
public class AddMeshCollidersEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        AddMeshColliders script = (AddMeshColliders)target;
        
        if (GUILayout.Button("Add MeshColliders to Children", GUILayout.Height(30)))
        {
            AddCollidersRecursively(script);
        }
    }
    
    private void AddCollidersRecursively(AddMeshColliders script)
    {
        int addedCount = 0;
        int skippedCount = 0;
        
        // Validate tag if applying
        if (script.applyTag && !IsValidTag(script.tagToApply))
        {
            EditorUtility.DisplayDialog("Invalid Tag", 
                $"The tag '{script.tagToApply}' does not exist. Please add it in Tags and Layers settings.", 
                "OK");
            return;
        }
        
        // Start recursive search
        Transform[] allChildren = script.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform child in allChildren)
        {
            // Skip self
            if (child == script.transform)
                continue;
            
            // Check if has MeshRenderer
            MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                continue;
            
            bool hasExistingCollider = child.GetComponent<Collider>() != null;
            bool shouldAddCollider = !hasExistingCollider || !script.skipExistingColliders;
            
            // If skipping and has collider, still apply tag if requested
            if (hasExistingCollider && script.skipExistingColliders)
            {
                if (script.applyTag)
                {
                    Undo.RecordObject(child.gameObject, "Apply Tag");
                    child.gameObject.tag = script.tagToApply;
                }
                skippedCount++;
                continue;
            }
            
            // Get or add MeshFilter (needed for MeshCollider)
            MeshFilter meshFilter = child.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"Skipping {child.name}: No valid MeshFilter found", child);
                skippedCount++;
                continue;
            }
            
            // Add MeshCollider
            Undo.RecordObject(child.gameObject, "Add MeshCollider");
            MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(child.gameObject);
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = script.convex;
            meshCollider.isTrigger = script.isTrigger;
            
            // Apply tag if requested
            if (script.applyTag)
            {
                Undo.RecordObject(child.gameObject, "Apply Tag");
                child.gameObject.tag = script.tagToApply;
            }
            
            addedCount++;
        }
        
        // Show results
        string message = $"Operation complete!\n\nMeshColliders added: {addedCount}\nObjects skipped: {skippedCount}";
        EditorUtility.DisplayDialog("Add MeshColliders", message, "OK");
        
        Debug.Log($"[AddMeshColliders] Added {addedCount} MeshColliders, skipped {skippedCount} objects on {script.gameObject.name}");
        
        // Remove component if requested
        if (script.removeAfterExecution)
        {
            EditorApplication.delayCall += () =>
            {
                if (script != null)
                {
                    Undo.DestroyObjectImmediate(script);
                }
            };
        }
    }
    
    private bool IsValidTag(string tag)
    {
        try
        {
            // This will throw an exception if the tag doesn't exist
            GameObject temp = new GameObject();
            temp.tag = tag;
            DestroyImmediate(temp);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
