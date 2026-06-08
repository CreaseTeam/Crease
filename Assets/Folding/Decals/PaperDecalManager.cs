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
        /// Updates decal transforms after a mesh change. Re-anchors only when topology may have changed
        /// (fold/unfold), not when the preview is simply synced back from the committed graph.
        /// </summary>
        public void RefreshAfterMeshUpdate(bool reanchorPlacements = true)
        {
            if (_attachedToFlight) return;

            _surfaceQuery?.RebuildTriangleMap();
            if (_authoringGraph == null || Controller == null) return;

            Transform meshRoot = GetFoldingMeshSurfaceRoot();
            if (meshRoot == null) return;

            for (int i = 0; i < _placements.Count; i++)
            {
                if (reanchorPlacements)
                    DecalSurfaceQuery.RefreshPlacementAnchors(_authoringGraph, _placements[i]);
                if (i < _quads.Count && _quads[i] != null)
                    _quads[i].UpdateFromPlacement(_placements[i], meshRoot, _authoringGraph);
            }
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

        public void ShowGhost(Texture2D texture, DecalSurfaceQuery.SurfaceHit hit, float scale)
        {
            if (texture == null) return;
            EnsureGhost();
            _ghostQuad.SetTexture(texture);
            _ghostQuad.gameObject.SetActive(true);
            ApplyGhost(hit, scale);
        }

        public void HideGhost()
        {
            if (_ghostQuad != null)
                _ghostQuad.gameObject.SetActive(false);
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
            quad.UpdateFromPlacement(placement, meshRoot, _authoringGraph, GetActiveVertexRotation());
            _quads.Add(quad);

            return true;
        }

        public void ClearDecals()
        {
            _placements.Clear();
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
                _quads[i].UpdateFromPlacement(_placements[i], meshRoot, _authoringGraph, vertexRotation);
            }
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

        private void ApplyGhost(DecalSurfaceQuery.SurfaceHit hit, float scale)
        {
            var temp = new DecalPlacement
            {
                Anchor0Index = hit.Anchor0Index,
                Anchor1Index = hit.Anchor1Index,
                Anchor2Index = hit.Anchor2Index,
                Barycentric = hit.Barycentric,
                SheetUv = hit.SheetUv,
                RotationUv = 0f,
                Scale = scale,
                Side = hit.Side,
                LocalPoint = hit.LocalPoint,
                LocalNormal = hit.LocalNormal,
                ViewRayOriginLocal = hit.ViewRayOriginLocal,
                ViewRayDirLocal = hit.ViewRayDirLocal
            };

            Transform meshRoot = GetFoldingMeshSurfaceRoot();
            if (meshRoot == null) return;
            _ghostQuad.UpdateFromPlacement(temp, meshRoot, _authoringGraph);
        }
    }
}
