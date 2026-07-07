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
        private Mesh _clippedMesh;
        private bool _isGhost;
        private int _layerOrder;

        public void Initialize(Texture2D texture = null, bool isGhost = false, Material materialTemplate = null)
        {
            _isGhost = isGhost;
            EnsureQuadMesh();
            EnsureRenderer(materialTemplate);
            if (texture != null)
                SetTexture(texture);
        }

        public void SetTexture(Texture2D texture)
        {
            if (texture == null || _materialInstance == null) return;
            _materialInstance.SetTexture("_BaseMap", texture);
        }

        public void SetBaseColor(Color color)
        {
            if (_materialInstance == null) return;
            _materialInstance.SetColor("_BaseColor", color);
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
            PreviewAnchorCache previewCache = null,
            float baseSurfaceOffset = 0.0005f,
            float layerOffsetStep = 0.0001f,
            bool useGraphToMeshLocalTransform = false,
            Matrix4x4 graphToMeshLocal = default)
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
                if (previewCache != null
                    && surfaceGraph != null
                    && DecalSurfaceQuery.TryInterpolateFromPlacementAnchors(surfaceGraph, placement, out localPos, out localNormal, out localTangent))
                {
                    // Resolved via placement anchors on the preview graph.
                }
                else if (previewCache != null)
                {
                    if (placement.LocalNormal.sqrMagnitude > 0.0001f)
                    {
                        localPos = placement.LocalPoint;
                        localNormal = placement.LocalNormal;
                        localTangent = DecalSurfaceQuery.InterpolateTangent(authoringGraph, placement, localNormal);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    localPos = DecalSurfaceQuery.InterpolatePosition(authoringGraph, placement);
                    localNormal = DecalSurfaceQuery.InterpolateNormal(authoringGraph, placement);
                    localTangent = DecalSurfaceQuery.InterpolateTangent(authoringGraph, placement, localNormal);
                }
            }

            if (useGraphToMeshLocalTransform)
            {
                localPos = graphToMeshLocal.MultiplyPoint3x4(localPos);
                localNormal = graphToMeshLocal.MultiplyVector(localNormal);
                localTangent = graphToMeshLocal.MultiplyVector(localTangent);
                if (localNormal.sqrMagnitude > 0.0001f)
                    localNormal.Normalize();
                if (localTangent.sqrMagnitude > 0.0001f)
                    localTangent.Normalize();
            }
            else if (meshVertexRotation != Quaternion.identity)
            {
                localPos = meshVertexRotation * localPos;
                localNormal = meshVertexRotation * localNormal;
                localTangent = meshVertexRotation * localTangent;
            }
            localPos += localNormal * (baseSurfaceOffset + _layerOrder * layerOffsetStep);

            if (localNormal.sqrMagnitude < 0.0001f)
                localNormal = Vector3.up;
            if (localTangent.sqrMagnitude < 0.0001f)
                localTangent = Vector3.Cross(localNormal, Vector3.forward);
            if (localTangent.sqrMagnitude < 0.0001f)
                localTangent = Vector3.right;

            transform.SetParent(meshRoot, false);
            transform.localPosition = localPos;
            Quaternion placementRotation = BuildPlacementRotation(placement, localNormal, localTangent);
            transform.localRotation = placementRotation;
            Vector2 quadScale = GetQuadScale(placement);
            transform.localScale = new Vector3(quadScale.x, quadScale.y, -1f);
            Vector3 axisU = placementRotation * Vector3.right;
            Vector3 axisV = placementRotation * Vector3.up;
            ApplyMeshForPlacement(
                placement,
                displayGraph,
                localPos,
                localNormal,
                axisU,
                axisV,
                quadScale,
                meshVertexRotation,
                useGraphToMeshLocalTransform,
                graphToMeshLocal);
        }

        private void ApplyMeshForPlacement(
            DecalPlacement placement,
            GraphMesh displayGraph,
            Vector3 localPos,
            Vector3 localNormal,
            Vector3 axisU,
            Vector3 axisV,
            Vector2 quadScale,
            Quaternion meshVertexRotation,
            bool useGraphToDisplayLocalTransform = false,
            Matrix4x4 graphToDisplayLocal = default)
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null)
                return;

            if (!placement.CullOverhang)
            {
                filter.sharedMesh = _sharedQuadMesh;
                if (_renderer != null)
                    _renderer.enabled = true;
                return;
            }

            if (_clippedMesh == null)
                _clippedMesh = new Mesh { name = "ClippedDecalQuad" };

            if (DecalPaperClipUtility.TryBuildClippedMesh(
                    displayGraph,
                    placement,
                    localPos,
                    localNormal,
                    axisU,
                    axisV,
                    quadScale,
                    out Mesh clippedMesh,
                    _clippedMesh,
                    meshVertexRotation,
                    useGraphToDisplayLocalTransform,
                    graphToDisplayLocal))
            {
                filter.sharedMesh = clippedMesh;
                if (_renderer != null)
                    _renderer.enabled = true;
                return;
            }

            filter.sharedMesh = _sharedQuadMesh;
            if (_renderer != null)
                _renderer.enabled = false;
        }

        private static Quaternion BuildPlacementRotation(
            DecalPlacement placement,
            Vector3 localNormal,
            Vector3 localTangent)
        {
            if (placement.UseAxisAlignment)
            {
                Vector3 alignOnSurface = Vector3.ProjectOnPlane(placement.AlignAxisLocal, localNormal);
                if (alignOnSurface.sqrMagnitude > 0.0001f)
                    return Quaternion.LookRotation(localNormal, alignOnSurface.normalized);
            }

            return Quaternion.LookRotation(localNormal, localTangent)
                * Quaternion.Euler(0f, 0f, placement.RotationUv);
        }

        private static Vector2 GetQuadScale(DecalPlacement placement)
        {
            if (placement.HeightScale > 0f)
                return new Vector2(placement.Scale, placement.HeightScale);

            return GetAspectPreservingScale(placement.Texture, placement.Scale);
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

        private void EnsureRenderer(Material materialTemplate = null)
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _sharedQuadMesh;

            _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();

            if (_materialInstance != null) return;

            _materialInstance = materialTemplate != null
                ? new Material(materialTemplate)
                : new Material(GetUrpLitShader());

            if (materialTemplate == null)
                Debug.LogWarning("DecalQuad: No DecalMaterial assigned on PaperDecalManager. Assign a configured transparent material in the inspector.");

            if (_isGhost)
            {
                Color baseColor = _materialInstance.GetColor("_BaseColor");
                baseColor.a = 0.5f;
                _materialInstance.SetColor("_BaseColor", baseColor);
            }

            _materialInstance.renderQueue = (int)RenderQueue.Transparent + _layerOrder;
            _renderer.sharedMaterial = _materialInstance;
        }

        private static Shader GetUrpLitShader()
        {
            if (_urpLitShader == null)
                _urpLitShader = Shader.Find("Universal Render Pipeline/Lit");

            if (_urpLitShader == null)
                Debug.LogError("DecalQuad: Universal Render Pipeline/Lit shader not found.");

            return _urpLitShader;
        }

        private void OnDestroy()
        {
            if (_materialInstance != null)
                Destroy(_materialInstance);
            if (_clippedMesh != null)
                Destroy(_clippedMesh);
        }
    }
}
