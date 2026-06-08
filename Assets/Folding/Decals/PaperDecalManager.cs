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

        private GraphMesh _authoringGraph;
        private DecalSurfaceQuery _surfaceQuery;
        private readonly List<DecalPlacement> _placements = new List<DecalPlacement>();
        private readonly List<DecalQuad> _quads = new List<DecalQuad>();
        private DecalQuad _ghostQuad;
        private Transform _flightMeshRoot;
        private Quaternion _flightVertexRotation = Quaternion.identity;
        private bool _attachedToFlight;
        private readonly List<PreviewAnchorCache> _previewCaches = new List<PreviewAnchorCache>();

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
        /// Updates decal transforms after a mesh change.
        /// Re-anchors committed placements when authoring topology changes; tracks the animated preview surface otherwise.
        /// </summary>
        public void RefreshAfterMeshUpdate(bool reanchorAuthoring = true, bool trackPreviewSurface = false)
        {
            if (_attachedToFlight) return;

            _surfaceQuery?.RebuildTriangleMap();
            if (_authoringGraph == null || Controller == null) return;

            Transform meshRoot = GetFoldingMeshSurfaceRoot();
            if (meshRoot == null) return;

            GraphMesh surfaceGraph = trackPreviewSurface && Controller.PreviewGraph != null
                ? Controller.PreviewGraph
                : null;

            EnsurePreviewCacheCount();

            for (int i = 0; i < _placements.Count; i++)
            {
                if (reanchorAuthoring)
                {
                    DecalSurfaceQuery.RefreshPlacementAnchors(_authoringGraph, _placements[i]);
                    _previewCaches[i].Invalidate();
                }

                if (i < _quads.Count && _quads[i] != null)
                {
                    _quads[i].SetLayerOrder(i);
                    PreviewAnchorCache previewCache = trackPreviewSurface ? _previewCaches[i] : null;
                    _quads[i].UpdateFromPlacement(
                        _placements[i],
                        meshRoot,
                        _authoringGraph,
                        surfaceGraph: surfaceGraph,
                        previewCache: previewCache);
                }
            }

            _ghostQuad?.SetLayerOrder(_quads.Count);
        }

        /// <summary>
        /// Reparents decals onto the flight mesh using the same placement data — no re-anchoring.
        /// Vertex rotation matches SaveMesh so locals align with rotated flight mesh vertices.
        /// </summary>
        public void AttachToFlight(Transform flightMeshRoot, Quaternion meshVertexRotation)
        {
            if (flightMeshRoot == null || _authoringGraph == null) return;

            _flightMeshRoot = flightMeshRoot;
            _flightVertexRotation = meshVertexRotation;
            _attachedToFlight = true;
            ApplyAllPlacementsToQuads();
        }

        /// <summary>
        /// Returns decals to the folding preview mesh root.
        /// </summary>
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
            ApplyGhost(hit, scale, rotationUv);
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
            if (FoldingCamera == null || _quads.Count == 0)
                return false;

            Ray worldRay = FoldingCamera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(worldRay, 100f);
            float bestDistance = float.MaxValue;
            const float pickDepthEpsilon = 0.0001f;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                DecalQuad quad = hit.collider != null ? hit.collider.GetComponent<DecalQuad>() : null;
                if (quad == null || quad == _ghostQuad)
                    continue;

                int quadIndex = _quads.IndexOf(quad);
                if (quadIndex < 0)
                    continue;

                bool isCloser = hit.distance < bestDistance - pickDepthEpsilon;
                bool isSameDepthButLater = Mathf.Abs(hit.distance - bestDistance) <= pickDepthEpsilon && quadIndex > index;
                if (!isCloser && !isSameDepthButLater)
                    continue;

                bestDistance = hit.distance;
                index = quadIndex;
            }

            return index >= 0;
        }

        public bool PlaceDecal(Texture2D texture, DecalSurfaceQuery.SurfaceHit hit, float scale, float rotationUv = 0f)
        {
            if (!hit.Hit || texture == null || _authoringGraph == null) return false;

            var placement = new DecalPlacement
            {
                Texture = texture,
                Anchor0Index = hit.Anchor0Index,
                Anchor1Index = hit.Anchor1Index,
                Anchor2Index = hit.Anchor2Index,
                Barycentric = hit.Barycentric,
                SheetUv = hit.SheetUv,
                RotationUv = rotationUv,
                Scale = scale,
                Side = hit.Side,
                LocalPoint = hit.LocalPoint,
                LocalNormal = hit.LocalNormal,
                ViewRayOriginLocal = hit.ViewRayOriginLocal,
                ViewRayDirLocal = hit.ViewRayDirLocal
            };

            _placements.Add(placement);

            Transform meshRoot = GetActiveMeshRoot();
            if (meshRoot == null) return false;

            GameObject quadObj = new GameObject($"Decal_{_placements.Count}");
            quadObj.transform.SetParent(meshRoot, false);
            DecalQuad quad = quadObj.AddComponent<DecalQuad>();
            quad.Initialize(texture, isGhost: false);
            _quads.Add(quad);
            quad.SetLayerOrder(_quads.Count - 1);
            quad.UpdateFromPlacement(placement, meshRoot, _authoringGraph, GetActiveVertexRotation());
            _ghostQuad?.SetLayerOrder(_quads.Count);

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

        private void ApplyAllPlacementsToQuads()
        {
            Transform meshRoot = GetActiveMeshRoot();
            if (meshRoot == null) return;

            Quaternion vertexRotation = GetActiveVertexRotation();
            for (int i = 0; i < _placements.Count; i++)
            {
                if (i >= _quads.Count || _quads[i] == null) continue;
                _quads[i].SetLayerOrder(i);
                _quads[i].UpdateFromPlacement(_placements[i], meshRoot, _authoringGraph, vertexRotation);
            }

            _ghostQuad?.SetLayerOrder(_quads.Count);
        }

        private void ApplyDecalLayerOrder(bool refreshTransforms = false)
        {
            Transform meshRoot = GetActiveMeshRoot();
            if (meshRoot == null || _authoringGraph == null)
                return;

            Quaternion vertexRotation = GetActiveVertexRotation();
            for (int i = 0; i < _quads.Count; i++)
            {
                if (_quads[i] == null)
                    continue;

                _quads[i].SetLayerOrder(i);
                if (!refreshTransforms || i >= _placements.Count)
                    continue;

                _quads[i].UpdateFromPlacement(_placements[i], meshRoot, _authoringGraph, vertexRotation);
            }

            _ghostQuad?.SetLayerOrder(_quads.Count);
        }

        public void InvalidatePreviewCaches()
        {
            for (int i = 0; i < _previewCaches.Count; i++)
                _previewCaches[i].Invalidate();
        }

        /// <summary>
        /// Re-resolves sticker anchors on the authoring graph without updating quad transforms.
        /// </summary>
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

        private void EnsurePreviewCacheCount()
        {
            while (_previewCaches.Count < _placements.Count)
                _previewCaches.Add(new PreviewAnchorCache());
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

        private void ApplyGhost(DecalSurfaceQuery.SurfaceHit hit, float scale, float rotationUv)
        {
            var temp = new DecalPlacement
            {
                Anchor0Index = hit.Anchor0Index,
                Anchor1Index = hit.Anchor1Index,
                Anchor2Index = hit.Anchor2Index,
                Barycentric = hit.Barycentric,
                SheetUv = hit.SheetUv,
                RotationUv = rotationUv,
                Scale = scale,
                Side = hit.Side,
                LocalPoint = hit.LocalPoint,
                LocalNormal = hit.LocalNormal,
                ViewRayOriginLocal = hit.ViewRayOriginLocal,
                ViewRayDirLocal = hit.ViewRayDirLocal
            };

            Transform meshRoot = GetFoldingMeshSurfaceRoot();
            if (meshRoot == null) return;
            _ghostQuad.SetLayerOrder(_quads.Count);
            _ghostQuad.UpdateFromPlacement(temp, meshRoot, _authoringGraph);
        }
    }
}
