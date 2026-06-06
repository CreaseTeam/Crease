using UnityEngine;
using UnityEditor;
using Crease.Flying.Environment.BlockoutHelpers;
using ObstacleComponent = Crease.Flying.Environment.Obstacle.Obstacle;

namespace Crease.Flying.Environment.BlockoutHelpers.Editor
{
    [CustomEditor(typeof(ColliderTagTools))]
    public class ColliderTagToolsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            ColliderTagTools script = (ColliderTagTools)target;

            string buttonText = script.Mode == ColliderTagTools.OperationMode.AddColliders
                ? "Add Mesh Colliders to Children"
                : "Apply Tags to Children";

            if (GUILayout.Button(buttonText, GUILayout.Height(30)))
            {
                ExecuteOperation(script);
            }
        }

        private void ExecuteOperation(ColliderTagTools script)
        {
            if (script.Mode == ColliderTagTools.OperationMode.AddColliders)
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

            if (!IsValidTag(script.TagToApply))
            {
                EditorUtility.DisplayDialog("Invalid Tag",
                    $"The tag '{script.TagToApply}' does not exist. Please add it in Tags and Layers settings.",
                    "OK");
                return;
            }

            Transform[] allChildren = script.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in allChildren)
            {
                if (child == script.transform)
                    continue;

                bool shouldTag = script.TagTarget == ColliderTagTools.TagTargetMode.AllChildren
                    || child.GetComponent<Collider>() != null;

                if (shouldTag)
                {
                    Undo.RecordObject(child.gameObject, "Apply Tag");
                    child.gameObject.tag = script.TagToApply;

                    if (script.ApplyObstacle)
                    {
                        ObstacleComponent ob = child.GetComponent<ObstacleComponent>();
                        if (ob == null)
                        {
                            ob = Undo.AddComponent<ObstacleComponent>(child.gameObject);
                        }
                        Undo.RecordObject(ob, "Configure Obstacle");
                        ob.ImpactDamage = script.ObstacleImpactDamage;
                        ob.DamageType = script.ObstacleDamageType;
                        ob.KnockbackMultiplier = script.ObstacleKnockbackMultiplier;
                        ob.ApplyKnockback = script.ObstacleApplyKnockback;
                        ob.OnHit = script.OnObstacleHit;
                    }
                    taggedCount++;
                }
            }

            string message = $"Operation complete!\n\nObjects tagged: {taggedCount}";
            EditorUtility.DisplayDialog("Apply Tags", message, "OK");

            Debug.Log($"[ColliderTagTools] Tagged {taggedCount} objects on {script.gameObject.name}");

            if (script.RemoveAfterExecution)
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

            if (script.ApplyTag && !IsValidTag(script.TagToApply))
            {
                EditorUtility.DisplayDialog("Invalid Tag",
                    $"The tag '{script.TagToApply}' does not exist. Please add it in Tags and Layers settings.",
                    "OK");
                return;
            }

            Transform[] allChildren = script.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in allChildren)
            {
                if (child == script.transform)
                    continue;

                if (script.RequireMeshRenderer)
                {
                    MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
                    if (meshRenderer == null)
                        continue;
                }

                bool hasExistingCollider = child.GetComponent<Collider>() != null;

                if (hasExistingCollider && script.SkipExistingColliders)
                {
                    if (script.ApplyTag)
                    {
                        Undo.RecordObject(child.gameObject, "Apply Tag");
                        child.gameObject.tag = script.TagToApply;
                    }
                    skippedCount++;
                    continue;
                }

                MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    Debug.LogWarning($"Skipping {child.name}: No valid MeshFilter found for MeshCollider", child);
                    skippedCount++;
                    continue;
                }

                Undo.RecordObject(child.gameObject, "Add MeshCollider");
                MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(child.gameObject);
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = script.Convex;
                meshCollider.isTrigger = script.IsTrigger;

                if (script.ApplyTag)
                {
                    Undo.RecordObject(child.gameObject, "Apply Tag");
                    child.gameObject.tag = script.TagToApply;
                }

                if (script.ApplyObstacle)
                {
                    ObstacleComponent ob = child.GetComponent<ObstacleComponent>();
                    if (ob == null)
                    {
                        ob = Undo.AddComponent<ObstacleComponent>(child.gameObject);
                    }
                    Undo.RecordObject(ob, "Configure Obstacle");
                    ob.ImpactDamage = script.ObstacleImpactDamage;
                    ob.DamageType = script.ObstacleDamageType;
                    ob.KnockbackMultiplier = script.ObstacleKnockbackMultiplier;
                    ob.ApplyKnockback = script.ObstacleApplyKnockback;
                    ob.OnHit = script.OnObstacleHit;
                }

                addedCount++;
            }

            string colliderTypeName = "MeshCollider" + (addedCount != 1 ? "s" : "");
            string message = $"Operation complete!\n\n{colliderTypeName} added: {addedCount}\nObjects skipped: {skippedCount}";
            EditorUtility.DisplayDialog("Add MeshColliders", message, "OK");

            Debug.Log($"[ColliderTagTools] Added {addedCount} {colliderTypeName}, skipped {skippedCount} objects on {script.gameObject.name}");

            if (script.RemoveAfterExecution)
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
}
