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

        private const float BaseSurfaceOffset = 0.0005f;
        private const float LayerOffsetStep = 0.0001f;

        private MeshRenderer _renderer;
        private Material _materialInstance;
        private bool _isGhost;
        private int _layerOrder;

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

        public void SetLayerOrder(int order)
        {
            _layerOrder = order;
            if (_materialInstance != null)
                _materialInstance.renderQueue = (int)RenderQueue.Transparent + order;
        }

        public void UpdateFromPlacement(
            DecalPlacement placement,
            Transform meshRoot,
            GraphMesh authoringGraph,
            Quaternion meshVertexRotation = default,
            GraphMesh surfaceGraph = null,
            PreviewAnchorCache previewCache = null)
        {
            if (meshVertexRotation == default)
                meshVertexRotation = Quaternion.identity;

            GraphMesh displayGraph = previewCache != null && surfaceGraph != null ? surfaceGraph : authoringGraph;
            if (!DecalSurfaceQuery.TryGetDisplayFrame(
                    displayGraph,
                    placement,
                    out Vector3 localPos,
                    out Vector3 localNormal,
                    out Vector3 localTangent,
                    previewCache))
            {
                if (previewCache != null)
                    return;

                localPos = DecalSurfaceQuery.InterpolatePosition(authoringGraph, placement);
                localNormal = DecalSurfaceQuery.InterpolateNormal(authoringGraph, placement);
                localTangent = DecalSurfaceQuery.InterpolateTangent(authoringGraph, placement, localNormal);
            }

            if (meshVertexRotation != Quaternion.identity)
            {
                localPos = meshVertexRotation * localPos;
                localNormal = meshVertexRotation * localNormal;
                localTangent = meshVertexRotation * localTangent;
            }
            localPos += localNormal * (BaseSurfaceOffset + _layerOrder * LayerOffsetStep);

            transform.SetParent(meshRoot, false);
            transform.localPosition = localPos;
            transform.localRotation = Quaternion.LookRotation(localNormal, localTangent)
                * Quaternion.Euler(0f, 0f, placement.RotationUv);
            Vector2 aspectScale = GetAspectPreservingScale(placement.Texture, placement.Scale);
            transform.localScale = new Vector3(aspectScale.x, aspectScale.y, -1f);
        }

        /// <summary>
        /// Scale is the sticker width on the paper; height follows the texture aspect ratio.
        /// </summary>
        private static Vector2 GetAspectPreservingScale(Texture2D texture, float widthScale)
        {
            if (texture == null || texture.width <= 0)
                return new Vector2(widthScale, widthScale);

            float aspect = (float)texture.height / texture.width;
            return new Vector2(widthScale, widthScale * aspect);
        }

        private void EnsureQuadMesh()
        {
            if (_sharedQuadMesh == null)
            {
                _sharedQuadMesh = new Mesh { name = "DecalQuad" };
                _sharedQuadMesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                };
            }

            // U flipped to compensate for negative Z scale (needed for correct Lit shading).
            _sharedQuadMesh.uv = new[]
            {
                new Vector2(1f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
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
