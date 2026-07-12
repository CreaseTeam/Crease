using System.Collections.Generic;
using Crease.Folding.PaperGraph;
using UnityEngine;
using UnityEngine.Serialization;
using GraphMesh = Crease.Folding.PaperGraph.PaperGraph;

namespace Crease.Folding.Decals
{
    public class PaperDecalManager : MonoBehaviour
    {
        [FormerlySerializedAs("controller")]
        public PaperGraphController Controller;

        [FormerlySerializedAs("foldingCamera")]
        public Camera FoldingCamera;

        [Header("Render Textures")]
        public DecalTextureRenderer TextureRenderer;

        private GraphMesh _authoringGraph;
        private DecalSurfaceQuery _surfaceQuery;
        private readonly List<DecalPlacement> _placements = new List<DecalPlacement>();
        private readonly List<DecalPlacement> _guidePlacements = new List<DecalPlacement>();
        private DecalPlacement _ghostPlacement;
        private bool _ghostVisible;

        private static Texture2D _guideTickTexture;

        public IReadOnlyList<DecalPlacement> Placements => _placements;

        private void Awake()
        {
            if (Controller == null)
                Controller = GetComponent<PaperGraphController>();
            if (Controller != null)
            {
                if (Controller.DecalManager == null)
                    Controller.DecalManager = this;
                _authoringGraph = Controller.GetComponent<GraphMesh>();
            }

            if (TextureRenderer == null)
                TextureRenderer = GetComponentInChildren<DecalTextureRenderer>(true);
        }

        public void PreparePlacement(bool syncPreviewFromAuthoring = true)
        {
            if (Controller == null || _authoringGraph == null || Controller.PreviewGraph == null)
                return;

            if (syncPreviewFromAuthoring)
                Controller.ClearPreview();

            OnMeshUpdated();
            RebuildTextures();
        }

        /// <summary>
        /// Refreshes sticker picking against the current preview mesh and re-binds decal RTs
        /// after paper topology or pose changes.
        /// </summary>
        public void OnMeshUpdated()
        {
            RebuildSurfaceQuery();
            ApplyDecalMapsToAllRenderers();
        }

        private void RebuildSurfaceQuery()
        {
            if (Controller == null || _authoringGraph == null || Controller.PreviewGraph == null)
                return;

            PaperGraphVisualizer previewVisualizer = Controller.PreviewGraph.GetComponent<PaperGraphVisualizer>();
            if (previewVisualizer == null || !previewVisualizer.ShowMesh)
                return;

            previewVisualizer.UpdateMesh();
            MeshCollider collider = previewVisualizer.GetComponent<MeshCollider>();
            if (collider == null || collider.sharedMesh == null)
                return;

            Transform meshSurfaceRoot = GetFoldingMeshSurfaceRoot();
            if (_surfaceQuery == null)
            {
                _surfaceQuery = new DecalSurfaceQuery(
                    _authoringGraph,
                    Controller.PreviewGraph,
                    meshSurfaceRoot,
                    collider);
            }

            _surfaceQuery.RebuildTriangleMap();
        }

        private Vector3 _cachedGuideLineStart;
        private Vector3 _cachedGuideLineEnd;
        private Vector3 _cachedGuidePlaneNormal;
        private Vector3 _cachedGuidePaperRotation;
        private int _cachedGuideFilterTagHash;
        private int _cachedGuideStyleHash;
        private bool _guidePlacementsCached;

        public void UpdateFoldGuide(
            Vector3 lineStart,
            Vector3 lineEnd,
            Vector3 planeNormal,
            Vector3 stepPaperRotation,
            IReadOnlyList<string> filterTags,
            GraphMesh styleSource)
        {
            if (Controller == null || _authoringGraph == null)
            {
                HideFoldGuide();
                return;
            }

            int filterTagHash = ComputeFilterTagHash(filterTags);
            int styleHash = ComputeGuideStyleHash(styleSource);
            if (_guidePlacementsCached
                && _cachedGuideLineStart == lineStart
                && _cachedGuideLineEnd == lineEnd
                && _cachedGuidePlaneNormal == planeNormal
                && _cachedGuidePaperRotation == stepPaperRotation
                && _cachedGuideFilterTagHash == filterTagHash
                && _cachedGuideStyleHash == styleHash)
                return;

            if (styleSource == null || !styleSource.GuideDashesEnabled || styleSource.GuideLineWidth <= 0f)
            {
                HideFoldGuide();
                return;
            }

            Vector3 axis = lineEnd - lineStart;
            float axisLength = axis.magnitude;
            if (axisLength < 0.0001f)
            {
                HideFoldGuide();
                return;
            }

            axis /= axisLength;
            planeNormal = planeNormal.sqrMagnitude > 0.0001f ? planeNormal.normalized : Vector3.up;
            Vector3 foldSplitPlaneNormal = Vector3.Cross(axis, planeNormal).normalized;
            if (foldSplitPlaneNormal.sqrMagnitude < 0.0001f)
            {
                HideFoldGuide();
                return;
            }

            HashSet<Face> allowedFaces = _authoringGraph.GetFacesForFoldGuide(
                lineStart,
                lineEnd,
                foldSplitPlaneNormal,
                filterTags);
            if (allowedFaces.Count == 0)
            {
                HideFoldGuide();
                return;
            }

            float dashLength = styleSource.GuideDashLength;
            float dashGap = styleSource.GuideDashGap;
            float period = dashLength + dashGap;
            if (period <= 0.0001f || dashLength <= 0f)
            {
                HideFoldGuide();
                return;
            }

            Texture2D tickTexture = GetGuideTickTexture();
            Color tickColor = styleSource.GuideLineColor;
            float tickWidth = styleSource.GuideLineWidth;
            float tickLength = styleSource.GuideDashLength;
            float along = styleSource.GuideDashOffset;
            Vector3 approachNormal = DecalSurfaceQuery.ResolveGuideApproachNormal(planeNormal, stepPaperRotation);

            var nextPlacements = new List<DecalPlacement>();
            const float boundsEpsilon = 0.00001f;
            while (along < axisLength)
            {
                if (along < -boundsEpsilon)
                {
                    along += period;
                    continue;
                }

                float dashEndAlong = along + dashLength;
                if (dashEndAlong > axisLength + boundsEpsilon)
                    break;

                float dashCenterAlong = along + dashLength * 0.5f;
                Vector3 samplePoint = lineStart + axis * dashCenterAlong;

                TryAddGuideDashPlacements(
                    samplePoint,
                    approachNormal,
                    allowedFaces,
                    axis,
                    tickTexture,
                    tickLength,
                    tickWidth,
                    nextPlacements);

                along += period;
            }

            ApplyGuidePlacements(nextPlacements, tickColor);

            _cachedGuideLineStart = lineStart;
            _cachedGuideLineEnd = lineEnd;
            _cachedGuidePlaneNormal = planeNormal;
            _cachedGuidePaperRotation = stepPaperRotation;
            _cachedGuideFilterTagHash = filterTagHash;
            _cachedGuideStyleHash = styleHash;
            _guidePlacementsCached = true;
        }

        public void HideFoldGuide()
        {
            if (_guidePlacements.Count == 0 && !_guidePlacementsCached)
                return;

            _guidePlacementsCached = false;
            _guidePlacements.Clear();
            RebuildTextures();
        }

        public void InvalidateFoldGuideCache()
        {
            _guidePlacementsCached = false;
        }

        public DecalSurfaceQuery.SurfaceHit RaycastScreen(Vector2 screenPosition)
        {
            if (_surfaceQuery == null)
            {
                Debug.LogError("PaperDecalManager: Surface query not prepared. Enter sticker phase before placing decals.");
                return new DecalSurfaceQuery.SurfaceHit { Hit = false };
            }

            if (FoldingCamera == null)
            {
                Debug.LogError("PaperDecalManager: FoldingCamera is not assigned.");
                return new DecalSurfaceQuery.SurfaceHit { Hit = false };
            }

            return _surfaceQuery.Raycast(FoldingCamera, screenPosition);
        }

        public void ShowGhost(Texture2D texture, DecalSurfaceQuery.SurfaceHit hit, float scale, float rotationUv = 0f)
        {
            if (texture == null)
                return;

            _ghostPlacement = DecalPlacementUtility.FromSurfaceHit(texture, hit, scale, rotationUv);
            _ghostVisible = true;
            RebuildTextures();
        }

        public void HideGhost()
        {
            _ghostVisible = false;
            _ghostPlacement = null;
            RebuildTextures();
        }

        public bool TryLiftDecalAtScreen(Vector2 screenPosition, out DecalPlacement placement)
        {
            placement = default;
            if (!TryPickDecalAtScreen(screenPosition, out int index))
                return false;

            return TryRemoveDecalAt(index, out placement);
        }

        public bool TryRemoveDecalAt(int index, out DecalPlacement placement)
        {
            placement = default;
            if (index < 0 || index >= _placements.Count)
                return false;

            placement = _placements[index];
            _placements.RemoveAt(index);
            RebuildTextures();
            return true;
        }

        public bool TryRemoveNewestDamageDecalOfType(int damageSourceType)
        {
            for (int i = _placements.Count - 1; i >= 0; i--)
            {
                if (!_placements[i].IsDamageDecal || _placements[i].DamageSourceType != damageSourceType)
                    continue;

                return TryRemoveDecalAt(i, out _);
            }

            return false;
        }

        private bool TryPickDecalAtScreen(Vector2 screenPosition, out int index)
        {
            index = -1;
            if (_placements.Count == 0 || _authoringGraph == null)
                return false;

            DecalSurfaceQuery.SurfaceHit surfaceHit = RaycastScreen(screenPosition);
            if (!surfaceHit.Hit)
                return false;

            for (int i = _placements.Count - 1; i >= 0; i--)
            {
                if (_placements[i].IsDamageDecal)
                    continue;

                if (DecalSurfaceQuery.TrySurfaceHitOverlapsPlacement(_authoringGraph, _placements[i], surfaceHit))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public bool PlaceDecal(
            Texture2D texture,
            DecalSurfaceQuery.SurfaceHit hit,
            float scale,
            float rotationUv = 0f,
            bool isDamageDecal = false,
            int damageSourceType = -1)
        {
            if (!hit.Hit || texture == null || _authoringGraph == null)
                return false;

            DecalPlacement placement = DecalPlacementUtility.FromSurfaceHit(texture, hit, scale, rotationUv);
            placement.IsDamageDecal = isDamageDecal;
            placement.DamageSourceType = isDamageDecal ? damageSourceType : -1;
            _placements.Add(placement);
            RebuildTextures();
            return true;
        }

        public bool PlaceDecalAtRandomOuterSurface(
            Texture2D texture,
            float scale,
            float rotationUv = 0f,
            bool isDamageDecal = false,
            int damageSourceType = -1)
        {
            if (texture == null || _authoringGraph == null)
                return false;

            if (!DecalSurfaceQuery.TryGetRandomVisibleSurfaceOnGraph(_authoringGraph, out DecalSurfaceQuery.SurfaceHit hit))
                return false;

            return PlaceDecal(texture, hit, scale, rotationUv, isDamageDecal, damageSourceType);
        }

        public void ClearDecals()
        {
            _placements.Clear();
            _guidePlacements.Clear();
            _ghostVisible = false;
            _ghostPlacement = null;
            TextureRenderer?.ClearTextures();
            RebuildTextures();
            OnDecalsCleared?.Invoke();
        }

        public event System.Action OnDecalsCleared;

        public void ClearUserStickers()
        {
            for (int i = _placements.Count - 1; i >= 0; i--)
            {
                if (_placements[i].IsDamageDecal)
                    continue;

                _placements.RemoveAt(i);
            }

            HideGhost();
        }

        public void ApplyDecalMapsToRenderer(Renderer renderer, GraphMesh graph = null)
        {
            if (renderer == null || TextureRenderer == null)
                return;

            PaperShading.ApplyRendererShading(
                renderer,
                graph,
                TextureRenderer.FrontTexture,
                TextureRenderer.BackTexture);
        }

        public void ApplyDecalMapsToVisualizers()
        {
            ApplyDecalMapsToAllRenderers();
        }

        private void RebuildTextures()
        {
            if (_authoringGraph == null || TextureRenderer == null)
                return;

            TextureRenderer.RequestRebuild(
                _authoringGraph,
                _placements,
                _guidePlacements,
                _ghostPlacement,
                _ghostVisible);
            ApplyDecalMapsToAllRenderers();
        }

        private void ApplyDecalMapsToAllRenderers()
        {
            if (Controller == null)
                return;

            if (Controller.PreviewGraph != null)
            {
                MeshRenderer previewRenderer = Controller.PreviewGraph.GetComponent<MeshRenderer>();
                ApplyDecalMapsToRenderer(previewRenderer, Controller.PreviewGraph);
            }

            if (_authoringGraph != null)
            {
                MeshRenderer authoringRenderer = _authoringGraph.GetComponent<MeshRenderer>();
                ApplyDecalMapsToRenderer(authoringRenderer, _authoringGraph);
            }
        }

        private void ApplyGuidePlacements(List<DecalPlacement> nextPlacements, Color tickColor)
        {
            _guidePlacements.Clear();
            for (int i = 0; i < nextPlacements.Count; i++)
            {
                DecalPlacement placement = nextPlacements[i];
                placement.StampColor = tickColor;
                _guidePlacements.Add(placement);
            }

            RebuildTextures();
        }

        private static Texture2D GetGuideTickTexture()
        {
            if (_guideTickTexture != null)
                return _guideTickTexture;

            _guideTickTexture = new Texture2D(4, 16, TextureFormat.RGBA32, mipChain: false)
            {
                name = "FoldGuideTick",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[4 * 16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            _guideTickTexture.SetPixels(pixels);
            _guideTickTexture.Apply(false, false);
            return _guideTickTexture;
        }

        private readonly List<DecalSurfaceQuery.SurfaceHit> _guideHitBuffer = new List<DecalSurfaceQuery.SurfaceHit>();

        private void TryAddGuideDashPlacements(
            Vector3 samplePoint,
            Vector3 approachNormal,
            HashSet<Face> allowedFaces,
            Vector3 guideAxis,
            Texture2D tickTexture,
            float tickLength,
            float tickWidth,
            List<DecalPlacement> output)
        {
            _guideHitBuffer.Clear();
            DecalSurfaceQuery.RaycastPlanarTopForGuideVisibleSides(
                _authoringGraph,
                _authoringGraph,
                samplePoint,
                approachNormal,
                allowedFaces,
                _guideHitBuffer);

            for (int i = 0; i < _guideHitBuffer.Count; i++)
            {
                if (TryCreateGuidePlacement(_guideHitBuffer[i], guideAxis, tickTexture, tickLength, tickWidth, out DecalPlacement placement))
                    output.Add(placement);
            }
        }

        private bool TryCreateGuidePlacement(
            DecalSurfaceQuery.SurfaceHit hit,
            Vector3 guideAxis,
            Texture2D tickTexture,
            float tickLength,
            float tickWidth,
            out DecalPlacement placement)
        {
            placement = null;
            if (!hit.Hit)
                return false;

            placement = DecalPlacementUtility.FromSurfaceHit(
                tickTexture,
                hit,
                tickLength,
                heightScale: tickWidth);
            placement.Texture = tickTexture;
            placement.RotationUv = DecalStampClipUtility.ComputeGuideRotationRad(_authoringGraph, hit, guideAxis) * Mathf.Rad2Deg;
            placement.UseAxisAlignment = false;
            return true;
        }

        private static int ComputeFilterTagHash(IReadOnlyList<string> filterTags)
        {
            if (filterTags == null || filterTags.Count == 0)
                return 0;

            unchecked
            {
                int hash = 17;
                for (int i = 0; i < filterTags.Count; i++)
                    hash = hash * 31 + (filterTags[i]?.GetHashCode() ?? 0);
                return hash;
            }
        }

        private static int ComputeGuideStyleHash(GraphMesh styleSource)
        {
            if (styleSource == null)
                return 0;

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + styleSource.GuideLineWidth.GetHashCode();
                hash = hash * 31 + styleSource.GuideDashLength.GetHashCode();
                hash = hash * 31 + styleSource.GuideDashGap.GetHashCode();
                hash = hash * 31 + styleSource.GuideDashOffset.GetHashCode();
                hash = hash * 31 + styleSource.GuideDashesEnabled.GetHashCode();
                hash = hash * 31 + styleSource.GuideLineColor.GetHashCode();
                return hash;
            }
        }

        private Transform GetFoldingMeshSurfaceRoot()
        {
            if (Controller == null || Controller.PreviewGraph == null)
                return Controller != null ? Controller.transform : null;
            return Controller.PreviewGraph.transform;
        }
    }
}
