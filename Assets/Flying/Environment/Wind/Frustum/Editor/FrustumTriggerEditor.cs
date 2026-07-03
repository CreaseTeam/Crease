using UnityEngine;
using UnityEditor;
using Crease.Flying.Environment.Wind.Frustum;

namespace Crease.Flying.Environment.Wind.Frustum.Editor
{
    [CustomEditor(typeof(FrustumTrigger))]
    [CanEditMultipleObjects]
    public class FrustumTriggerEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            FrustumTrigger t = (FrustumTrigger)target;
            if (t == null) return;

            Matrix4x4 localToWorld = t.transform.localToWorldMatrix;
            Handles.matrix = localToWorld;
            Handles.color = Color.green;

            float halfH = t.Height * 0.5f;

            EditorGUI.BeginChangeCheck();
            Vector3 topPos = Handles.Slider(new Vector3(0, halfH, t.TopRadius), Vector3.up, 0.4f, Handles.ConeHandleCap, 0.1f);
            Vector3 bottomPos = Handles.Slider(new Vector3(0, -halfH, t.BottomRadius), Vector3.down, 0.4f, Handles.ConeHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Adjust Frustum Height");

                float newHeight = t.Height;
                if (GUIUtility.hotControl != 0)
                {
                    float topY = topPos.y;
                    float bottomY = bottomPos.y;

                    if (topY != halfH) newHeight = topY * 2f;
                    else if (bottomY != -halfH) newHeight = -bottomY * 2f;
                }

                t.Height = Mathf.Max(0, newHeight);
                t.RebuildMesh();
            }

            EditorGUI.BeginChangeCheck();
            Vector3 topRadiusPos = Handles.Slider(new Vector3(t.TopRadius, halfH, 0), Vector3.right, 0.15f, Handles.SphereHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Adjust Frustum Top Radius");
                t.TopRadius = Mathf.Max(0, topRadiusPos.x);
                t.RebuildMesh();
            }

            EditorGUI.BeginChangeCheck();
            Vector3 bottomRadiusPos = Handles.Slider(new Vector3(t.BottomRadius, -halfH, 0), Vector3.right, 0.15f, Handles.SphereHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Adjust Frustum Bottom Radius");
                t.BottomRadius = Mathf.Max(0, bottomRadiusPos.x);
                t.RebuildMesh();
            }
        }
    }
}
