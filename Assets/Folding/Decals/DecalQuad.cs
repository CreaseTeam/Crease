using Crease.Folding.PaperGraph;
using UnityEngine;
using UnityEngine.Rendering;
using GraphMesh = Crease.Folding.PaperGraph.PaperGraph;

namespace Crease.Folding.Decals
{
    public class DecalQuad : MonoBehaviour
    {
        private static Mesh _sharedQuadMesh;
        private static Shader _urpLitShader;

        private MeshRenderer _renderer;
        private Material _materialInstance;
        private bool _isGhost;

        public void Initialize(Texture2D texture = null, bool isGhost = false)
        {
            _isGhost = isGhost;
            EnsureQuadMesh();
            EnsureRenderer();
            if (texture != null)
                SetTexture(texture);
        }

        public void SetTexture(Texture2D texture)
        {
            if (texture == null || _materialInstance == null) return;
            _materialInstance.SetTexture("_BaseMap", texture);
        }

        public void UpdateFromPlacement(
            DecalPlacement placement,
            Transform meshRoot,
            GraphMesh authoringGraph,
            Quaternion meshVertexRotation = default)
        {
            if (meshVertexRotation == default)
                meshVertexRotation = Quaternion.identity;

            Vector3 localPos = DecalSurfaceQuery.InterpolatePosition(authoringGraph, placement);
            Vector3 localNormal = DecalSurfaceQuery.InterpolateNormal(authoringGraph, placement);
            Vector3 localTangent = DecalSurfaceQuery.InterpolateTangent(authoringGraph, placement, localNormal);

            if (meshVertexRotation != Quaternion.identity)
            {
                localPos = meshVertexRotation * localPos;
                localNormal = meshVertexRotation * localNormal;
                localTangent = meshVertexRotation * localTangent;
            }
            localPos += localNormal * 0.0005f;

            transform.SetParent(meshRoot, false);
            transform.localPosition = localPos;
            transform.localRotation = Quaternion.LookRotation(localNormal, localTangent)
                * Quaternion.Euler(0f, 0f, placement.RotationUv);
            transform.localScale = new Vector3(placement.Scale, placement.Scale, -1f);
        }

        private void EnsureQuadMesh()
        {
            if (_sharedQuadMesh != null) return;

            _sharedQuadMesh = new Mesh { name = "DecalQuad" };
            _sharedQuadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            _sharedQuadMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            _sharedQuadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _sharedQuadMesh.RecalculateNormals();
            _sharedQuadMesh.RecalculateBounds();
        }

        private void EnsureRenderer()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _sharedQuadMesh;

            _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();

            if (_materialInstance != null) return;

            _materialInstance = CreateTransparentMaterial();
            _renderer.sharedMaterial = _materialInstance;
        }

        private Material CreateTransparentMaterial()
        {
            if (_urpLitShader == null)
                _urpLitShader = Shader.Find("Universal Render Pipeline/Lit");

            if (_urpLitShader == null)
            {
                Debug.LogError("DecalQuad: Universal Render Pipeline/Lit shader not found.");
                return null;
            }

            Material material = new Material(_urpLitShader);
            Color tint = _isGhost ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.5f);
            material.SetFloat("_ReceiveShadows", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_BLENDMODE_ALPHA");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private void OnDestroy()
        {
            if (_materialInstance != null)
                Destroy(_materialInstance);
        }
    }
}
