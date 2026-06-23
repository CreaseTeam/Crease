using UnityEngine;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    /// <summary>
    /// TEMPORARY DEBUG TOOL — not part of the final feature.
    /// Renders each convex segment child's mesh in bright color so the tube trigger
    /// volume is visible in Game view / Play mode, where MeshColliders never render.
    /// Remove this component (and script) once real VFX makes the tube visible enough.
    /// </summary>
    [ExecuteAlways]
    public class SplineTubeDebugRenderer : MonoBehaviour
    {
        [Tooltip("Debug color for the tube material. Bright/saturated colors show up best.")]
        public Color DebugColor = new Color(0f, 0.6f, 1f, 0.4f);

        private Material _debugMaterial;

        private void OnEnable()
        {
            CreateDebugMaterial();
        }

        private void CreateDebugMaterial()
        {
            // URP project detected — use URP/Unlit for transparency support.
            // If this still renders magenta, check your project's URP asset for the exact shader path.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");

            _debugMaterial = new Material(shader);
            _debugMaterial.SetFloat("_Surface", 1);   // 0 = Opaque, 1 = Transparent (URP)
            _debugMaterial.SetFloat("_Blend", 0);     // Alpha blend mode
            _debugMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _debugMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _debugMaterial.SetInt("_ZWrite", 0);
            _debugMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _debugMaterial.renderQueue = 3000;
            _debugMaterial.color = DebugColor;
        }

        private void Update()
        {
            if (_debugMaterial == null) CreateDebugMaterial();

            // Find all SegmentTriggerRelay children — each corresponds to one convex segment.
            // Draw their meshes via Graphics.DrawMesh so no MeshRenderer/MeshFilter needed.
            SegmentTriggerRelay[] relays = GetComponentsInChildren<SegmentTriggerRelay>();

            foreach (SegmentTriggerRelay relay in relays)
            {
                MeshCollider mc = relay.GetComponent<MeshCollider>();
                if (mc == null || mc.sharedMesh == null) continue;

                Graphics.DrawMesh(
                    mc.sharedMesh,
                    Matrix4x4.identity, // verts already in world space
                    _debugMaterial,
                    gameObject.layer
                );
            }
        }

        private void OnDisable()
        {
            if (_debugMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_debugMaterial);
                else
                    DestroyImmediate(_debugMaterial);
            }
        }
    }
}