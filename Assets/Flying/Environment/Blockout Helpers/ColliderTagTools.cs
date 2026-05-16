using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Helper component to recursively add colliders to children or apply tags.
/// Use the custom inspector button to execute the operation.
/// </summary>
public class ColliderTagTools : MonoBehaviour
{
    public enum OperationMode
    {
        AddColliders,
        TagOnly
    }
    
    public enum TagTarget
    {
        ObjectsWithColliders,
        AllChildren
    }
    
    [Header("Operation Mode")]
    [Tooltip("Choose whether to add colliders or just apply tags")]
    public OperationMode mode = OperationMode.AddColliders;
    
    [Header("Collider Settings")]
    [Tooltip("Make the mesh colliders convex")]
    public bool convex = false;
    
    [Tooltip("Make the colliders triggers")]
    public bool isTrigger = false;
    
    [Header("Tag Settings")]
    [Tooltip("Apply a tag to objects")]
    public bool applyTag = false;
    
    [Tooltip("When in Tag Only mode, what to tag")]
    public TagTarget tagTarget = TagTarget.ObjectsWithColliders;
    
    [Tooltip("The tag to apply (must exist in Tags and Layers settings)")]
    public string tagToApply = "Untagged";
    
    [Header("Options")]
    [Tooltip("Skip objects that already have a collider (when adding colliders)")]
    public bool skipExistingColliders = true;
    
    [Tooltip("Only process objects with MeshRenderer components")]
    public bool requireMeshRenderer = true;
    
    [Tooltip("Remove this component after execution")]
    public bool removeAfterExecution = true;

    [Header("Obstacle Options")]
    [Tooltip("When enabled, add/configure an Obstacle component on processed objects.")]
    public bool applyObstacle = false;

    [Tooltip("Impact damage applied by the Obstacle component")]
    public float obstacleImpactDamage = 10f;

    [Tooltip("Damage type used by the Obstacle component")]
    public DamageType obstacleDamageType = DamageType.Impact;

    [Tooltip("Knockback multiplier applied by the Obstacle component")]
    public float obstacleKnockbackMultiplier = 1f;

    [Tooltip("If false, the Obstacle will not apply knockback to the hitter (damage only)")]
    public bool obstacleApplyKnockback = true;

    [Tooltip("Optional OnHit event to assign to the Obstacle component")]
    public UnityEvent<GameObject> obstacleOnHit = new UnityEvent<GameObject>();
}
