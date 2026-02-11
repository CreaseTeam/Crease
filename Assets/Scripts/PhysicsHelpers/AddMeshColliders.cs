using UnityEngine;

/// <summary>
/// Helper component to recursively add MeshColliders to all children with MeshRenderers.
/// Use the custom inspector button to execute the operation.
/// </summary>
public class AddMeshColliders : MonoBehaviour
{
    [Header("Collider Settings")]
    [Tooltip("Make the mesh colliders convex")]
    public bool convex = false;
    
    [Tooltip("Make the mesh colliders triggers")]
    public bool isTrigger = false;
    
    [Header("Tag Settings")]
    [Tooltip("Apply a tag to all objects that get a collider added")]
    public bool applyTag = false;
    
    [Tooltip("The tag to apply (must exist in Tags and Layers settings)")]
    public string tagToApply = "Untagged";
    
    [Header("Options")]
    [Tooltip("Skip objects that already have a collider")]
    public bool skipExistingColliders = true;
    
    [Tooltip("Remove this component after execution")]
    public bool removeAfterExecution = true;
}
