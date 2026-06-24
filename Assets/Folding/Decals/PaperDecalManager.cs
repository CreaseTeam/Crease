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

        [Header("Surface Offset")]
        [Tooltip("Base lift along the surface normal so decals sit above the paper mesh.")]
        [Min(0f)]
        public float BaseSurfaceOffset = 0.0005f;
        [Tooltip("Extra lift per decal layer order step, applied along the surface normal.")]
        [Min(0f)]
        public float LayerOffsetStep = 0.0001f;

        private GraphMesh _authoringGraph;
        private DecalSurfaceQuery _surfaceQuery;
        private readonly List<DecalPlacement> _placements = new List<DecalPlacement>();
        private readonly List<DecalQuad> _quads = new List<DecalQuad>();
        private readonly List<PreviewAnchorCache> _previewCaches = new List<PreviewAnchorCache>();
        private readonly List<DecalPlacement> _guidePlacements = new List<DecalPlacement>();
        private readonly List<DecalQuad> _guideQuads = new List<DecalQuad>();
        private readonly List<PreviewAnchorCache> _guidePreviewCaches = new List<PreviewAnchorCache>();
        private DecalQuad _ghostQuad;
        private readonly PreviewAnchorCache _ghostPreviewCache = new PreviewAnchorCache();
        private Transform _flightMeshRoot;
        private Quaternion _flightVertexRotation = Quaternion.identity;
        private bool _attachedToFlight;

        private static Texture2D _guideTickTexture;

        public IReadOnlyList<DecalPlacement> Placements => _placements;
        public bool IsAttachedToFlight => _attachedToFlight;

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
        }

        public void PreparePlacement(bool syncPreviewFromAuthoring = true)
        {
            if (Controller == null || _authoringGraph == null || Controller.PreviewGraph == null)
                return;

            if (syncPreviewFromAuthoring)
                Controller.ClearPreview();

            EnsureSurfaceQuery();
        }

        public void EnsureSurfaceQuery()
        {
            RebuildSurfaceQuery();
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
            _surfaceQuery = new DecalSurfaceQuery(
                _authoringGraph,
                Controller.PreviewGraph,
                meshSurfaceRoot,
                collider);
            _surfaceQuery.RebuildTriangleMap();
        }

        /// <summary>
        /// Re-anchors sticker data on the authoring graph when topology changes, then redisplays
        /// every decal on the preview surface (authoring + current fold). Preview matches authoring
        /// when no fold is in progress.
        /// </summary>
        public void RefreshAfterMeshUpdate(bool reanchorAuthoring = true)
        {
            if (_attachedToFlight) return;

            _surfaceQuery?.RebuildTriangleMap();
            if (_authoringGraph == null || Controller == null) return;

            EnsurePreviewCacheCount();
            EnsureGuidePreviewCacheCount();

            for (int i = 0; i < _placements.Count; i++)
            {
                if (reanchorAuthoring)
                {
                    DecalSurfaceQuery.RefreshPlacementAnchors(_authoringGraph, _placements[i]);
                    _previewCaches[i].Invalidate();
                }

                if (i < _quads.Count && _quads[i] != null)
                    ApplyDecalDisplay(_placements[i], _quads[i], _previewCaches[i], i);
            }

            RefreshGuideDisplay();

            _ghostQuad?.SetLayerOrder(_placements.Count + _guideQuads.Count);
        }

        public void UpdateFoldGuide(
            Vector3 lineStart,
            Vector3 lineEnd,
            Vector3 planeNormal,
            IReadOnlyList<string> filterTags,
            GraphMesh styleSource)
        {
            if (_attachedToFlight || Controller == null || _authoringGraph == null)
            {
                HideFoldGuide();
                return;
            }

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

            HashSet<Face> allowedFaces = _authoringGraph.GetFacesSplitByFoldPlane(
                lineStart,
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
            float tickHeight = dashLength;
            float along = styleSource.GuideDashOffset;

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

                DecalSurfaceQuery.SurfaceHit hit = DecalSurfaceQuery.RaycastPlanarTopOnGraph(
                    _authoringGraph,
                    samplePoint,
                    planeNormal,
                    allowedFaces);

                if (hit.Hit)
                {
                    nextPlacements.Add(DecalPlacementUtility.FromSurfaceHit(
                        tickTexture,
                        hit,
                        tickWidth,
                        heightScale: tickHeight,
                        alignAxisLocal: axis,
                        useAxisAlignment: true));
                }

                along += period;
            }

            ApplyGuidePlacements(nextPlacements, tickColor);
        }

        public void HideFoldGuide()
        {
            ClearGuideQuads();
            _guidePlacements.Clear();
            _guidePreviewCaches.Clear();
        }

        public void AttachToFlight(Transform flightMeshRoot, Quaternion meshVertexRotation)
        {
            if (flightMeshRoot == null || _authoringGraph == null) return;

            _flightMeshRoot = flightMeshRoot;
            _flightVertexRotation = meshVertexRotation;
            _attachedToFlight = true;
            HideFoldGuide();
            ApplyAllPlacementsToQuads();
        }

        public void RestoreToFolding()
        {
            if (!_attachedToFlight || _authoringGraph == null) return;

            _attachedToFlight = false;
            _flightMeshRoot = null;
            _flightVertexRotation = Quaternion.identity;
            ApplyAllPlacementsToQuads();
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
            if (texture == null) return;
            EnsureGhost();
            _ghostQuad.SetTexture(texture);
            _ghostQuad.gameObject.SetActive(true);
            DecalPlacement temp = DecalPlacementUtility.FromSurfaceHit(texture, hit, scale, rotationUv);
            ApplyDecalDisplay(temp, _ghostQuad, _ghostPreviewCache, _placements.Count + _guideQuads.Count);
        }

        public void HideGhost()
        {
            if (_ghostQuad != null)
                _ghostQuad.gameObject.SetActive(false);
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

            if (index < _previewCaches.Count)
                _previewCaches.RemoveAt(index);

            if (index < _quads.Count && _quads[index] != null)
                Destroy(_quads[index].gameObject);
            _quads.RemoveAt(index);

            ApplyDecalLayerOrder(refreshTransforms: true);
            return true;
        }

        private bool TryPickDecalAtScreen(Vector2 screenPosition, out int index)
        {
            index = -1;
            if (_quads.Count == 0 || _authoringGraph == null)
                return false;

            DecalSurfaceQuery.SurfaceHit surfaceHit = RaycastScreen(screenPosition);
            if (!surfaceHit.Hit)
                return false;

            for (int i = _placements.Count - 1; i >= 0; i--)
            {
                if (DecalSurfaceQuery.TrySurfaceHitOverlapsPlacement(_authoringGraph, _placements[i], surfaceHit))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public bool PlaceDecal(Texture2D texture, DecalSurfaceQuery.SurfaceHit hit, float scale, float rotationUv = 0f)
        {
            if (!hit.Hit || texture == null || _authoringGraph == null) return false;

            DecalPlacement placement = DecalPlacementUtility.FromSurfaceHit(texture, hit, scale, rotationUv);
            _placements.Add(placement);

            Transform meshRoot = GetActiveMeshRoot();
            if (meshRoot == null) return false;

            var previewCache = new PreviewAnchorCache();
            _previewCaches.Add(previewCache);

            GameObject quadObj = new GameObject($"Decal_{_placements.Count}");
            quadObj.transform.SetParent(meshRoot, false);
            DecalQuad quad = quadObj.AddComponent<DecalQuad>();
            quad.Initialize(texture, isGhost: false);
            _quads.Add(quad);
            ApplyDecalDisplay(placement, quad, previewCache, _placements.Count - 1);
            _ghostQuad?.SetLayerOrder(_placements.Count + _guideQuads.Count);

            return true;
        }

        public void ClearDecals()
        {
            _placements.Clear();
            _previewCaches.Clear();
            foreach (DecalQuad quad in _quads)
            {
                if (quad != null) Destroy(quad.gameObject);
            }
            _quads.Clear();
            HideGhost();
        }

        public void InvalidatePreviewCaches()
        {
            for (int i = 0; i < _previewCaches.Count; i++)
                _previewCaches[i].Invalidate();
            for (int i = 0; i < _guidePreviewCaches.Count; i++)
                _guidePreviewCaches[i].Invalidate();
        }

        public void ReanchorPlacementDataOnly()
        {
            if (_attachedToFlight || _authoringGraph == null)
                return;

            EnsurePreviewCacheCount();

            for (int i = 0; i < _placements.Count; i++)
            {
                DecalSurfaceQuery.RefreshPlacementAnchors(_authoringGraph, _placements[i]);
                _previewCaches[i].Invalidate();
            }
        }

        /// <summary>
        /// Displays a decal on the preview mesh using authoring placement data.
        /// Preview is authoring plus the current fold, so stickers and guides follow folds.
        /// </summary>
        private void ApplyDecalDisplay(
            DecalPlacement placement,
            DecalQuad quad,
            PreviewAnchorCache previewCache,
            int renderOrder,
            Quaternion meshVertexRotation = default)
        {
            if (quad == null || _authoringGraph == null)
                return;

            if (meshVertexRotation == default)
                meshVertexRotation = GetActiveVertexRotation();

            GraphMesh surfaceGraph = null;
            PreviewAnchorCache cache = null;
            if (!_attachedToFlight && Controller?.PreviewGraph != null)
            {
                surfaceGraph = Controller.PreviewGraph;
                cache = previewCache;
            }

            quad.SetLayerOrder(renderOrder);
            quad.UpdateFromPlacement(
                placement,
                GetActiveMeshRoot(),
                _authoringGraph,
                meshVertexRotation,
                surfaceGraph,
                cache,
                BaseSurfaceOffset,
                LayerOffsetStep);
        }

        private void ApplyAllPlacementsToQuads()
        {
            if (GetActiveMeshRoot() == null) return;

            for (int i = 0; i < _placements.Count; i++)
            {
                if (i >= _quads.Count || _quads[i] == null) continue;
                ApplyDecalDisplay(_placements[i], _quads[i], _previewCaches[i], i);
            }

            RefreshGuideDisplay();
            _ghostQuad?.SetLayerOrder(_placements.Count + _guideQuads.Count);
        }

        private void ApplyDecalLayerOrder(bool refreshTransforms = false)
        {
            if (GetActiveMeshRoot() == null || _authoringGraph == null)
                return;

            for (int i = 0; i < _quads.Count; i++)
            {
                if (_quads[i] == null)
                    continue;

                if (!refreshTransforms || i >= _placements.Count)
                {
                    _quads[i].SetLayerOrder(i);
                    continue;
                }

                ApplyDecalDisplay(_placements[i], _quads[i], _previewCaches[i], i);
            }

            if (refreshTransforms)
                RefreshGuideDisplay();

            _ghostQuad?.SetLayerOrder(_placements.Count + _guideQuads.Count);
        }

        private void RefreshGuideDisplay()
        {
            for (int i = 0; i < _guideQuads.Count; i++)
            {
                if (_guideQuads[i] == null || i >= _guidePlacements.Count)
                    continue;

                ApplyDecalDisplay(
                    _guidePlacements[i],
                    _guideQuads[i],
                    _guidePreviewCaches[i],
                    _placements.Count + i);
            }
        }

        private void ApplyGuidePlacements(List<DecalPlacement> nextPlacements, Color tickColor)
        {
            Transform meshRoot = GetFoldingMeshSurfaceRoot();
            if (meshRoot == null)
                return;

            while (_guidePlacements.Count < nextPlacements.Count)
                _guidePlacements.Add(null);
            while (_guidePreviewCaches.Count < nextPlacements.Count)
                _guidePreviewCaches.Add(new PreviewAnchorCache());

            for (int i = 0; i < nextPlacements.Count; i++)
            {
                _guidePlacements[i] = nextPlacements[i];
                _guidePreviewCaches[i].Invalidate();

                if (i >= _guideQuads.Count)
                {
                    GameObject quadObj = new GameObject($"FoldGuideTick_{i}");
                    quadObj.transform.SetParent(meshRoot, false);
                    DecalQuad quad = quadObj.AddComponent<DecalQuad>();
                    quad.Initialize(nextPlacements[i].Texture, isGhost: false);
                    _guideQuads.Add(quad);
                }

                DecalQuad tickQuad = _guideQuads[i];
                tickQuad.gameObject.SetActive(true);
                tickQuad.SetTexture(nextPlacements[i].Texture);
                tickQuad.SetBaseColor(tickColor);
                ApplyDecalDisplay(
                    _guidePlacements[i],
                    tickQuad,
                    _guidePreviewCaches[i],
                    _placements.Count + i);
            }

            for (int i = _guideQuads.Count - 1; i >= nextPlacements.Count; i--)
            {
                if (_guideQuads[i] != null)
                    Destroy(_guideQuads[i].gameObject);
                _guideQuads.RemoveAt(i);
            }

            if (_guidePlacements.Count > nextPlacements.Count)
                _guidePlacements.RemoveRange(nextPlacements.Count, _guidePlacements.Count - nextPlacements.Count);
            if (_guidePreviewCaches.Count > nextPlacements.Count)
                _guidePreviewCaches.RemoveRange(nextPlacements.Count, _guidePreviewCaches.Count - nextPlacements.Count);
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

        private void ClearGuideQuads()
        {
            foreach (DecalQuad quad in _guideQuads)
            {
                if (quad != null)
                    Destroy(quad.gameObject);
            }

            _guideQuads.Clear();
        }

        private void EnsurePreviewCacheCount()
        {
            while (_previewCaches.Count < _placements.Count)
                _previewCaches.Add(new PreviewAnchorCache());
        }

        private void EnsureGuidePreviewCacheCount()
        {
            while (_guidePreviewCaches.Count < _guidePlacements.Count)
                _guidePreviewCaches.Add(new PreviewAnchorCache());
        }

        private Transform GetFoldingMeshSurfaceRoot()
        {
            if (Controller == null || Controller.PreviewGraph == null)
                return Controller != null ? Controller.transform : null;
            return Controller.PreviewGraph.transform;
        }

        private Transform GetActiveMeshRoot()
        {
            if (_attachedToFlight)
                return _flightMeshRoot;
            return GetFoldingMeshSurfaceRoot();
        }

        private Quaternion GetActiveVertexRotation()
        {
            return _attachedToFlight ? _flightVertexRotation : Quaternion.identity;
        }

        private void EnsureGhost()
        {
            if (_ghostQuad != null) return;
            Transform meshRoot = GetFoldingMeshSurfaceRoot();
            GameObject ghostObj = new GameObject("DecalGhost");
            ghostObj.transform.SetParent(meshRoot != null ? meshRoot : transform, false);
            _ghostQuad = ghostObj.AddComponent<DecalQuad>();
            _ghostQuad.Initialize(isGhost: true);
        }
    }
}
