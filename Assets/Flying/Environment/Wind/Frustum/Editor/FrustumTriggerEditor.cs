using UnityEngine;
using UnityEditor;

namespace PhysicsHelpers
{
    [CustomEditor(typeof(FrustumTrigger))]
    public class FrustumTriggerEditor : Editor
    {
        private void OnSceneGUI()
        {
            FrustumTrigger t = (target as FrustumTrigger);
            if (t == null) return;

            Matrix4x4 localToWorld = t.transform.localToWorldMatrix;
            Handles.matrix = localToWorld;
            Handles.color = Color.green;

            float halfH = t.height * 0.5f;

            // Height Handles (Top and Bottom) - Moved to the edge (Z-axis) to clear the center view
            EditorGUI.BeginChangeCheck();
            Vector3 topPos = Handles.Slider(new Vector3(0, halfH, t.topRadius), Vector3.up, 0.4f, Handles.ConeHandleCap, 0.1f);
            Vector3 bottomPos = Handles.Slider(new Vector3(0, -halfH, t.bottomRadius), Vector3.down, 0.4f, Handles.ConeHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Adjust Frustum Height");
                // The slider moves in Y, so we calculate height based on the Y difference relative to center
                // We assume symmetrical expansion/contraction for simplicity in this handle
                // actually, since we are moving the caps, let's just calculate height from the new Y positions logic
                // But wait, the handle position's Y component is what we care about.
                
                float newHalfHeight = Mathf.Abs(topPos.y); 
                // Alternatively, user might be dragging the bottom one.
                // Let's deduce intention. If topPos moved, update height based on top.
                
                // Simpler approach: Calculate total height based on the distance between the logical top and bottom planes
                // effectively, we are modifying 'height' which is centered.
                
                // Since the handle is restricted to Vector3.up/down, we only get Y changes.
                // Let's assume we want to scale the height symmetrically for now as the pivot is center.
                // If the user drags the top handle up, height increases.
                
                float newHeight = t.height;
                if (GUIUtility.hotControl != 0) // If we are dragging
                {
                    // Check which handle is roughly being dragged or just calculate based on max extent
                    float topY = topPos.y;
                    float bottomY = bottomPos.y; // note: bottomPos.y should be negative
                    
                    // If we dragged top handle
                    if (topY != halfH) newHeight = topY * 2f;
                    // If we dragged bottom handle (which is negative)
                    else if (bottomY != -halfH) newHeight = -bottomY * 2f;
                }

                t.height = Mathf.Max(0, newHeight);
                t.RebuildMesh();
            }

            // Top Radius Handle
            EditorGUI.BeginChangeCheck();
            Vector3 topRadiusPos = Handles.Slider(new Vector3(t.topRadius, halfH, 0), Vector3.right, 0.15f, Handles.SphereHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Adjust Frustum Top Radius");
                t.topRadius = Mathf.Max(0, topRadiusPos.x);
                t.RebuildMesh();
            }

            // Bottom Radius Handle
            EditorGUI.BeginChangeCheck();
            Vector3 bottomRadiusPos = Handles.Slider(new Vector3(t.bottomRadius, -halfH, 0), Vector3.right, 0.15f, Handles.SphereHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Adjust Frustum Bottom Radius");
                t.bottomRadius = Mathf.Max(0, bottomRadiusPos.x);
                t.RebuildMesh();
            }
        }
    }
}
