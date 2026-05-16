using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ColliderTagTools))]
public class ColliderTagToolsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        ColliderTagTools script = (ColliderTagTools)target;
        
        string buttonText = script.mode == ColliderTagTools.OperationMode.AddColliders 
            ? "Add Mesh Colliders to Children" 
            : "Apply Tags to Children";
        
        if (GUILayout.Button(buttonText, GUILayout.Height(30)))
        {
            ExecuteOperation(script);
        }
    }
    
    private void ExecuteOperation(ColliderTagTools script)
    {
        if (script.mode == ColliderTagTools.OperationMode.AddColliders)
        {
            AddCollidersRecursively(script);
        }
        else
        {
            ApplyTagsRecursively(script);
        }
    }
    
    private void ApplyTagsRecursively(ColliderTagTools script)
    {
        int taggedCount = 0;
        
        // Validate tag
        if (!IsValidTag(script.tagToApply))
        {
            EditorUtility.DisplayDialog("Invalid Tag", 
                $"The tag '{script.tagToApply}' does not exist. Please add it in Tags and Layers settings.", 
                "OK");
            return;
        }
        
        Transform[] allChildren = script.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform child in allChildren)
        {
            // Skip self
            if (child == script.transform)
                continue;
            
            bool shouldTag = false;
            
            if (script.tagTarget == ColliderTagTools.TagTarget.AllChildren)
            {
                shouldTag = true;
            }
            else // ObjectsWithColliders
            {
                shouldTag = child.GetComponent<Collider>() != null;
            }
            
            if (shouldTag)
            {
                Undo.RecordObject(child.gameObject, "Apply Tag");
                child.gameObject.tag = script.tagToApply;

                // Optionally add/configure Obstacle
                if (script.applyObstacle)
                {
                    Obstacle ob = child.GetComponent<Obstacle>();
                    if (ob == null)
                    {
                        ob = Undo.AddComponent<Obstacle>(child.gameObject);
                    }
                    Undo.RecordObject(ob, "Configure Obstacle");
                    ob._impactDamage = script.obstacleImpactDamage;
                    ob._damageType = script.obstacleDamageType;
                    ob.knockbackMultiplier = script.obstacleKnockbackMultiplier;
                    ob.applyKnockback = script.obstacleApplyKnockback;
                    ob.OnHit = script.obstacleOnHit;
                }
                taggedCount++;
            }
        }
        
        // Show results
        string message = $"Operation complete!\n\nObjects tagged: {taggedCount}";
        EditorUtility.DisplayDialog("Apply Tags", message, "OK");
        
        Debug.Log($"[ColliderTagTools] Tagged {taggedCount} objects on {script.gameObject.name}");
        
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
    
    private void AddCollidersRecursively(ColliderTagTools script)
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
            
            // Check if requires MeshRenderer
            if (script.requireMeshRenderer)
            {
                MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    continue;
            }
            
            bool hasExistingCollider = child.GetComponent<Collider>() != null;
            
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
                Debug.LogWarning($"Skipping {child.name}: No valid MeshFilter found for MeshCollider", child);
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

            // Optionally add/configure Obstacle when adding colliders
            if (script.applyObstacle)
            {
                Obstacle ob = child.GetComponent<Obstacle>();
                if (ob == null)
                {
                    ob = Undo.AddComponent<Obstacle>(child.gameObject);
                }
                Undo.RecordObject(ob, "Configure Obstacle");
                ob._impactDamage = script.obstacleImpactDamage;
                ob._damageType = script.obstacleDamageType;
                ob.knockbackMultiplier = script.obstacleKnockbackMultiplier;
                ob.applyKnockback = script.obstacleApplyKnockback;
                ob.OnHit = script.obstacleOnHit;
            }
            
            addedCount++;
        }
        
        // Show results
        string colliderTypeName = "MeshCollider" + (addedCount != 1 ? "s" : "");
        string message = $"Operation complete!\n\n{colliderTypeName} added: {addedCount}\nObjects skipped: {skippedCount}";
        EditorUtility.DisplayDialog("Add MeshColliders", message, "OK");
        
        Debug.Log($"[ColliderTagTools] Added {addedCount} {colliderTypeName}, skipped {skippedCount} objects on {script.gameObject.name}");
        
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
