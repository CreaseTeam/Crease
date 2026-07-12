using System.Collections;
using System.Collections.Generic;
using Crease.Folding.PaperGraph;
using UnityEngine;
using UnityEngine.Rendering;
using GraphMesh = Crease.Folding.PaperGraph.PaperGraph;

namespace Crease.Folding.PaperSurface.Decals
{
    /// <summary>
    /// Renders decal placements into front/back sheet-space render textures.
    /// Wire cameras, stamp roots, textures, and materials in the editor — see DECAL_RT_SETUP.md.
    /// </summary>
    public class DecalTextureRenderer : MonoBehaviour
    {
        public const float CapturePlaneWidth = 10f;
        public const float CapturePlaneHeight = 13f;

        public static Vector2 GraphSizeToCaptureSize(Vector2 graphSize, float graphWidth, float graphHeight)
        {
            if (graphWidth <= 0.00001f || graphHeight <= 0.00001f)
                return graphSize;

            return new Vector2(
                graphSize.x * CapturePlaneWidth / graphWidth,
                graphSize.y * CapturePlaneHeight / graphHeight);
        }

        [Header("Capture Cameras")]
        [Tooltip("Orthographic camera targeting FrontTexture. Output Texture must be assigned on the camera.")]
        public Camera FrontCamera;

        [Tooltip("Orthographic camera targeting BackTexture. Output Texture must be assigned on the camera.")]
        public Camera BackCamera;

        [Header("Stamp Roots")]
        public Transform FrontStampRoot;
        public Transform BackStampRoot;

        [Header("Stamp Assets")]
        public Material StampMaterial;
        public Mesh UnitQuadMesh;

        [Header("Output Textures")]
        public RenderTexture FrontTexture;
        public RenderTexture BackTexture;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly List<StampObject> _stampPool = new List<StampObject>();
        private GraphMesh _pendingGraph;
        private IReadOnlyList<DecalPlacement> _pendingPlacements;
        private IReadOnlyList<DecalPlacement> _pendingGuides;
        private DecalPlacement _pendingGhost;
        private bool _pendingIncludeGhost;
        private Coroutine _rebuildCoroutine;
        private bool _rebuildQueued;

        public void RequestRebuild(
            GraphMesh graph,
            IReadOnlyList<DecalPlacement> placements,
            IReadOnlyList<DecalPlacement> guides,
            DecalPlacement ghostPlacement,
            bool includeGhost)
        {
            _pendingGraph = graph;
            _pendingPlacements = placements;
            _pendingGuides = guides;
            _pendingGhost = ghostPlacement;
            _pendingIncludeGhost = includeGhost;

            if (_rebuildCoroutine != null)
            {
                _rebuildQueued = true;
                return;
            }

            _rebuildCoroutine = StartCoroutine(RebuildEndOfFrame());
        }

        public void ClearTextures()
        {
            ClearRenderTexture(FrontTexture);
            ClearRenderTexture(BackTexture);
        }

        private IEnumerator RebuildEndOfFrame()
        {
            do
            {
                _rebuildQueued = false;
                yield return new WaitForEndOfFrame();

                if (_pendingGraph == null || FrontTexture == null || BackTexture == null)
                    continue;

                if (!ValidateSetup())
                    continue;

                HideAllStamps();

                int stampIndex = 0;
                if (_pendingPlacements != null)
                {
                    for (int i = 0; i < _pendingPlacements.Count; i++)
                        stampIndex = PlaceStamp(_pendingGraph, _pendingPlacements[i], stampIndex, isGhost: false);
                }

                if (_pendingGuides != null)
                {
                    for (int i = 0; i < _pendingGuides.Count; i++)
                        stampIndex = PlaceStamp(_pendingGraph, _pendingGuides[i], stampIndex, isGhost: false);
                }

                if (_pendingIncludeGhost && _pendingGhost != null)
                    stampIndex = PlaceStamp(_pendingGraph, _pendingGhost, stampIndex, isGhost: true);

                TrimStampPool(stampIndex);

                RenderSide(PaperSide.Front, FrontCamera, FrontStampRoot, BackStampRoot);
                RenderSide(PaperSide.Back, BackCamera, BackStampRoot, FrontStampRoot);
            }
            while (_rebuildQueued);

            _rebuildCoroutine = null;
        }

        private bool ValidateSetup()
        {
            if (FrontCamera == null || BackCamera == null || FrontStampRoot == null || BackStampRoot == null)
            {
                Debug.LogError("DecalTextureRenderer: Assign Front/Back cameras and stamp roots. See Assets/Folding/PaperSurface/Decals/DECAL_RT_SETUP.md", this);
                return false;
            }

            if (StampMaterial == null || UnitQuadMesh == null)
            {
                Debug.LogError("DecalTextureRenderer: Assign StampMaterial and UnitQuadMesh. See Assets/Folding/PaperSurface/Decals/DECAL_RT_SETUP.md", this);
                return false;
            }

            if (FrontCamera.targetTexture != FrontTexture)
                Debug.LogWarning("DecalTextureRenderer: FrontCamera Output Texture should reference FrontTexture.", FrontCamera);

            if (BackCamera.targetTexture != BackTexture)
                Debug.LogWarning("DecalTextureRenderer: BackCamera Output Texture should reference BackTexture.", BackCamera);

            return true;
        }

        private int PlaceStamp(GraphMesh graph, DecalPlacement placement, int stampIndex, bool isGhost)
        {
            if (placement == null || placement.Texture == null)
                return stampIndex;

            StampObject stamp = GetOrCreateStamp(stampIndex);
            stampIndex++;

            Transform root = placement.Side == PaperSide.Front ? FrontStampRoot : BackStampRoot;
            stamp.GameObject.transform.SetParent(root, false);
            stamp.GameObject.SetActive(true);

            stamp.Material.SetTexture(BaseMapId, placement.Texture);
            Color color = placement.StampColor;
            if (isGhost)
                color.a *= 0.5f;
            stamp.Material.SetColor(BaseColorId, color);

            Vector2 size = DecalStampClipUtility.GetPlacementSize(placement);
            Vector2 captureSize = GraphSizeToCaptureSize(size, graph.Width, graph.Height);
            float rotationRad = placement.UseAxisAlignment && placement.AlignAxisLocal.sqrMagnitude > 0.0001f
                ? DecalStampClipUtility.ComputeStampRotationRad(placement, graph.Width, graph.Height)
                : placement.RotationUv * Mathf.Deg2Rad;

            // SheetUv is already in the UV space for placement.Side (back uses mirrored U).
            stamp.GameObject.transform.localPosition = SheetUvToCapturePosition(placement.SheetUv);
            stamp.GameObject.transform.localRotation = Quaternion.Euler(90f, -rotationRad * Mathf.Rad2Deg, 0f);
            stamp.Filter.sharedMesh = UnitQuadMesh;
            stamp.GameObject.transform.localScale = new Vector3(captureSize.x, captureSize.y, 1f);

            return stampIndex;
        }

        private void RenderSide(PaperSide side, Camera camera, Transform activeRoot, Transform hiddenRoot)
        {
            if (camera == null)
                return;

            bool hiddenWasActive = hiddenRoot.gameObject.activeSelf;
            hiddenRoot.gameObject.SetActive(false);
            activeRoot.gameObject.SetActive(true);

            camera.Render();

            hiddenRoot.gameObject.SetActive(hiddenWasActive);
        }

        private StampObject GetOrCreateStamp(int index)
        {
            while (_stampPool.Count <= index)
            {
                var stampObject = new GameObject($"DecalStamp_{_stampPool.Count}");
                stampObject.transform.SetParent(transform, false);

                var filter = stampObject.AddComponent<MeshFilter>();
                var renderer = stampObject.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                Material material = new Material(StampMaterial);
                renderer.sharedMaterial = material;

                _stampPool.Add(new StampObject
                {
                    GameObject = stampObject,
                    Filter = filter,
                    Renderer = renderer,
                    Material = material
                });
            }

            return _stampPool[index];
        }

        private void HideAllStamps()
        {
            for (int i = 0; i < _stampPool.Count; i++)
                _stampPool[i].GameObject.SetActive(false);
        }

        /// <summary>
        /// Guide dashes change every step and are never reused; drop excess pool entries
        /// instead of leaving deactivated stamp objects in the hierarchy.
        /// </summary>
        private void TrimStampPool(int activeCount)
        {
            for (int i = _stampPool.Count - 1; i >= activeCount; i--)
            {
                StampObject stamp = _stampPool[i];
                if (stamp.Material != null)
                    Destroy(stamp.Material);
                if (stamp.GameObject != null)
                    Destroy(stamp.GameObject);
                _stampPool.RemoveAt(i);
            }
        }

        private static Vector3 SheetUvToCapturePosition(Vector2 sheetUv)
        {
            float x = (sheetUv.x - 0.5f) * CapturePlaneWidth;
            float z = (sheetUv.y - 0.5f) * CapturePlaneHeight;
            return new Vector3(x, 0f, z);
        }

        private static void ClearRenderTexture(RenderTexture target)
        {
            if (target == null)
                return;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
            RenderTexture.active = previous;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _stampPool.Count; i++)
            {
                if (_stampPool[i].Material != null)
                    Destroy(_stampPool[i].Material);
            }
        }

        private sealed class StampObject
        {
            public GameObject GameObject;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Material Material;
        }
    }
}
