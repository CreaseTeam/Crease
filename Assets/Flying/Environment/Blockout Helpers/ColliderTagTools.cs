using Crease.Flying.Environment.Obstacle;
using Crease.Flying.Player.Health;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Crease.Flying.Environment.BlockoutHelpers
{
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

        public enum TagTargetMode
        {
            ObjectsWithColliders,
            AllChildren
        }

        [Header("Operation Mode")]
        [Tooltip("Choose whether to add colliders or just apply tags")]
        [FormerlySerializedAs("mode")]
        public OperationMode Mode = OperationMode.AddColliders;

        [Header("Collider Settings")]
        [Tooltip("Make the mesh colliders convex")]
        [FormerlySerializedAs("convex")]
        public bool Convex = false;

        [Tooltip("Make the colliders triggers")]
        [FormerlySerializedAs("isTrigger")]
        public bool IsTrigger = false;

        [Header("Tag Settings")]
        [Tooltip("Apply a tag to objects")]
        [FormerlySerializedAs("applyTag")]
        public bool ApplyTag = false;

        [Tooltip("When in Tag Only mode, what to tag")]
        [FormerlySerializedAs("tagTarget")]
        public TagTargetMode TagTarget = TagTargetMode.ObjectsWithColliders;

        [Tooltip("The tag to apply (must exist in Tags and Layers settings)")]
        [FormerlySerializedAs("tagToApply")]
        public string TagToApply = "Untagged";

        [Header("Options")]
        [Tooltip("Skip objects that already have a collider (when adding colliders)")]
        [FormerlySerializedAs("skipExistingColliders")]
        public bool SkipExistingColliders = true;

        [Tooltip("Only process objects with MeshRenderer components")]
        [FormerlySerializedAs("requireMeshRenderer")]
        public bool RequireMeshRenderer = true;

        [Tooltip("Remove this component after execution")]
        [FormerlySerializedAs("removeAfterExecution")]
        public bool RemoveAfterExecution = true;

        [Header("Obstacle Options")]
        [Tooltip("When enabled, add/configure an Obstacle component on processed objects.")]
        [FormerlySerializedAs("applyObstacle")]
        public bool ApplyObstacle = false;

        [Tooltip("Impact damage applied by the Obstacle component")]
        [FormerlySerializedAs("obstacleImpactDamage")]
        public float ObstacleImpactDamage = 10f;

        [Tooltip("Damage type used by the Obstacle component")]
        [FormerlySerializedAs("obstacleDamageType")]
        public DamageType ObstacleDamageType = DamageType.Impact;

        [Tooltip("Knockback multiplier applied by the Obstacle component")]
        [FormerlySerializedAs("obstacleKnockbackMultiplier")]
        public float ObstacleKnockbackMultiplier = 1f;

        [Tooltip("If false, the Obstacle will not apply knockback to the hitter (damage only)")]
        [FormerlySerializedAs("obstacleApplyKnockback")]
        public bool ObstacleApplyKnockback = true;

        [Tooltip("Optional OnHit event to assign to the Obstacle component")]
        [FormerlySerializedAs("obstacleOnHit")]
        public UnityEvent<GameObject> OnObstacleHit = new UnityEvent<GameObject>();
    }
}
