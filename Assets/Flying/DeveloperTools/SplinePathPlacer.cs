using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crease.Flying.DeveloperTools
{
    /// <summary>
    /// Designer tool: places N copies of a prefab evenly along an editable spline path.
    ///
    /// Drop this component on an empty GameObject. It auto-creates a <see cref="SplineContainer"/>
    /// seeded with a gentle default arc. Drag the spline knots in the Scene view to shape the path
    /// (an arc, an S-curve "snake", a loop — anything the Spline tool can draw) and the copies
    /// redistribute live. Set <see cref="Prefab"/> and <see cref="Count"/> and you are done.
    ///
    /// The placed objects are a live, regenerating preview (they are not saved into the scene).
    /// Use "Bake" from the inspector to commit them to permanent, saveable objects.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SplineContainer))]
    [AddComponentMenu("Crease/Tools/Spline Path Placer")]
    public class SplinePathPlacer : MonoBehaviour
    {
        const string PreviewRootName = "PathPreview";
        const float MinPathLength = 1e-4f;

        public enum UpAxisSource
        {
            SplineUp,
            WorldUp,
        }

        [Header("What To Place")]
        [Tooltip("The prefab (or scene object) to copy along the path.")]
        [SerializeField] GameObject _prefab;

        [Tooltip("How many copies to distribute along the path.")]
        [Min(0)]
        [SerializeField] int _count = 10;

        [Header("Distribution")]
        [Tooltip("Space copies evenly by real distance along the curve. Off = even by spline parameter (faster, but bunches on tight bends).")]
        [SerializeField] bool _uniformSpacing = true;

        [Tooltip("Where on the path the first copy sits (0 = start, 1 = end).")]
        [Range(0f, 1f)]
        [SerializeField] float _startNormalized = 0f;

        [Tooltip("Where on the path the last copy sits (0 = start, 1 = end).")]
        [Range(0f, 1f)]
        [SerializeField] float _endNormalized = 1f;

        [Header("Orientation")]
        [Tooltip("Rotate each copy to face along the path direction.")]
        [SerializeField] bool _alignToPath = true;

        [Tooltip("Which 'up' to use when aligning to the path.")]
        [SerializeField] UpAxisSource _upSource = UpAxisSource.SplineUp;

        [Tooltip("Extra rotation (Euler degrees) applied to every copy after aligning.")]
        [SerializeField] Vector3 _rotationOffset = Vector3.zero;

        [Tooltip("Positional nudge applied to every copy, in each copy's own local space (after alignment).")]
        [SerializeField] Vector3 _positionOffset = Vector3.zero;

        SplineContainer _container;
        Transform _previewRoot;
        GameObject _lastPreviewPrefab;
        bool _rebuildPending;
        bool _isRebuilding;

        public GameObject Prefab
        {
            get => _prefab;
            set { _prefab = value; ScheduleRebuild(); }
        }

        public int Count
        {
            get => _count;
            set { _count = Mathf.Max(0, value); ScheduleRebuild(); }
        }

        SplineContainer Container
        {
            get
            {
                if (_container == null)
                    _container = GetComponent<SplineContainer>();
                return _container;
            }
        }

        void OnEnable()
        {
            EnsureDefaultArc();
            Spline.Changed += OnPrimarySplineChanged;
#if UNITY_EDITOR
            Undo.undoRedoPerformed += ScheduleRebuild;
#endif
            ScheduleRebuild();
        }

        void OnDisable()
        {
            Spline.Changed -= OnPrimarySplineChanged;
#if UNITY_EDITOR
            Undo.undoRedoPerformed -= ScheduleRebuild;
            EditorApplication.delayCall -= FlushRebuild;
#endif
            _rebuildPending = false;
            ClearPreview();
        }

        void OnValidate()
        {
            if (_endNormalized < _startNormalized)
                _endNormalized = _startNormalized;

            // Instantiate is not allowed from OnValidate — always defer.
            ScheduleRebuild();
        }

        void Update()
        {
            if (_rebuildPending)
                FlushRebuild();
        }

        // Only the container's primary spline drives placement, so ignore edits to any other spline.
        void OnPrimarySplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (Container != null && Container.Spline == spline)
                ScheduleRebuild();
        }

        void ScheduleRebuild()
        {
            if (_rebuildPending)
                return;

            _rebuildPending = true;
#if UNITY_EDITOR
            EditorApplication.delayCall -= FlushRebuild;
            EditorApplication.delayCall += FlushRebuild;
#endif
        }

        void FlushRebuild()
        {
            if (!_rebuildPending)
                return;

#if UNITY_EDITOR
            // Don't consume the pending flag mid-undo; undoRedoPerformed reschedules us afterward.
            if (Undo.isProcessing)
                return;
#endif

            _rebuildPending = false;

            if (this == null || !isActiveAndEnabled)
                return;

            Rebuild();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (_isRebuilding)
                return;

            _isRebuilding = true;
            try
            {
                if (_prefab == null || _count <= 0 || Container == null)
                {
                    ClearPreview();
                    return;
                }

                Spline spline = Container.Spline;
                if (spline == null || spline.Count < 2)
                {
                    ClearPreview();
                    return;
                }

                float length = spline.GetLength();
                if (length < MinPathLength)
                {
                    ClearPreview();
                    return;
                }

                EnsurePreviewRoot();

                // A different prefab means the pooled instances are the wrong type — start fresh.
                if (_lastPreviewPrefab != _prefab)
                {
                    DestroyPreviewChildren();
                    _lastPreviewPrefab = _prefab;
                }

                SyncPreviewInstanceCount(_count);

                // For a full closed loop, the last copy would land on top of the first, so divide by
                // _count. Any open path — or a trimmed closed path — should place a copy on both ends.
                bool fullLoop = spline.Closed && _startNormalized <= 0f && _endNormalized >= 1f;
                float denominator = fullLoop ? _count : Mathf.Max(1, _count - 1);

                for (int i = 0; i < _count; i++)
                {
                    float f = i / denominator;                             // 0..1 across the copies
                    float u = Mathf.Lerp(_startNormalized, _endNormalized, f); // fraction of the whole path
                    float t = NormalizedToEvalT(spline, u, length);

                    Vector3 position = (Vector3)Container.EvaluatePosition(t);
                    Quaternion rotation = ResolveRotation(t);
                    Vector3 worldPosition = position + rotation * _positionOffset;

                    Transform child = _previewRoot.GetChild(i);
                    child.name = $"{_prefab.name}_{i}";
                    child.SetPositionAndRotation(worldPosition, rotation);
                }
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        float NormalizedToEvalT(Spline spline, float u, float length)
        {
            if (!_uniformSpacing || length < MinPathLength)
                return u;

            // Convert an arc-length fraction into the spline's evaluation parameter so copies are
            // evenly spaced by real distance rather than by curve parameter.
            return spline.ConvertIndexUnit(u * length, PathIndexUnit.Distance, PathIndexUnit.Normalized);
        }

        Quaternion ResolveRotation(float t)
        {
            Quaternion offset = Quaternion.Euler(_rotationOffset);

            if (!_alignToPath)
                return transform.rotation * offset;

            Vector3 forward = (Vector3)Container.EvaluateTangent(t);
            if (forward.sqrMagnitude < 1e-6f)
                return transform.rotation * offset;
            forward.Normalize();

            Vector3 up = _upSource == UpAxisSource.SplineUp
                ? (Vector3)Container.EvaluateUpVector(t)
                : Vector3.up;
            if (up.sqrMagnitude < 1e-6f)
                up = Vector3.up;
            up.Normalize();

            // If 'up' is nearly parallel to the path direction, LookRotation is unstable (a real case
            // for vertical 3D snakes). Fall back to an axis guaranteed not to be parallel.
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.999f)
                up = Mathf.Abs(forward.y) < 0.999f ? Vector3.up : Vector3.right;

            return Quaternion.LookRotation(forward, up) * offset;
        }

        void EnsureDefaultArc()
        {
            if (Container == null)
                return;

            if (Container.Splines.Count == 0)
                Container.AddSpline();

            Spline spline = Container.Spline;
            if (spline == null || spline.Count > 0)
                return;

            // A gentle default arc so the tool works the moment it is added.
            spline.Add(new BezierKnot(new float3(-5f, 0f, 0f)));
            spline.Add(new BezierKnot(new float3(0f, 3f, 0f)));
            spline.Add(new BezierKnot(new float3(5f, 0f, 0f)));
            spline.SetTangentMode(TangentMode.AutoSmooth);
        }

        void EnsurePreviewRoot()
        {
            if (_previewRoot != null)
                return;

            _previewRoot = FindOwnedPreviewRoot();
            if (_previewRoot != null)
                return;

            GameObject rootObject = new GameObject(PreviewRootName);
            rootObject.hideFlags = HideFlags.DontSave;
            rootObject.transform.SetParent(transform, false);
            _previewRoot = rootObject.transform;
        }

        // Only ever adopt a preview root WE generated (marked DontSave). This keeps the tool from
        // deleting a real designer object that merely happens to share the PathPreview name.
        Transform FindOwnedPreviewRoot()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == PreviewRootName && child.gameObject.hideFlags == HideFlags.DontSave)
                    return child;
            }

            return null;
        }

        // Grow/shrink the pooled preview instances to match the target count (reused between rebuilds
        // so dragging a knot repositions objects instead of destroying and recreating them).
        void SyncPreviewInstanceCount(int target)
        {
            for (int i = _previewRoot.childCount - 1; i >= target; i--)
                DestroyObject(_previewRoot.GetChild(i).gameObject);

            while (_previewRoot.childCount < target)
            {
                GameObject instance = Instantiate(_prefab, _previewRoot);
                instance.hideFlags = HideFlags.DontSave;
            }
        }

        void DestroyPreviewChildren()
        {
            if (_previewRoot == null)
                return;

            for (int i = _previewRoot.childCount - 1; i >= 0; i--)
                DestroyObject(_previewRoot.GetChild(i).gameObject);
        }

        [ContextMenu("Clear Preview")]
        public void ClearPreview()
        {
            if (_previewRoot == null)
                _previewRoot = FindOwnedPreviewRoot();

            if (_previewRoot == null)
                return;

            DestroyPreviewChildren();
            DestroyObject(_previewRoot.gameObject);
            _previewRoot = null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Commits the current live preview into permanent, saveable objects under a new sibling
        /// GameObject. In the editor these are real prefab instances (they keep their prefab link).
        /// The placer is left intact — disable or delete it afterward to remove the live preview.
        /// </summary>
        public void Bake()
        {
            if (_prefab == null || _count <= 0)
                return;

            // Make sure the preview reflects the latest settings before copying its transforms.
            Rebuild();
            if (_previewRoot == null || _previewRoot.childCount == 0)
                return;

            GameObject bakedRoot = new GameObject($"{name} (Baked)");
            Undo.RegisterCreatedObjectUndo(bakedRoot, "Bake Spline Path");

            // Mirror the placer's full local transform (including scale) so baked instances end up at
            // the same world scale as the preview, which lives under this placer.
            bakedRoot.transform.SetParent(transform.parent, worldPositionStays: false);
            bakedRoot.transform.localPosition = transform.localPosition;
            bakedRoot.transform.localRotation = transform.localRotation;
            bakedRoot.transform.localScale = transform.localScale;

            for (int i = 0; i < _previewRoot.childCount; i++)
            {
                Transform src = _previewRoot.GetChild(i);
                GameObject baked = InstantiatePermanent(bakedRoot.transform);
                if (baked == null)
                    continue;

                baked.name = $"{_prefab.name}_{i}";
                baked.transform.SetPositionAndRotation(src.position, src.rotation);
                Undo.RegisterCreatedObjectUndo(baked, "Bake Spline Path");
            }

            Selection.activeGameObject = bakedRoot;
            Debug.Log($"[SplinePathPlacer] Baked {_previewRoot.childCount} copies into '{bakedRoot.name}'. " +
                      "Disable or delete this placer to remove the live preview.", bakedRoot);
        }

        GameObject InstantiatePermanent(Transform parent)
        {
            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(_prefab)
                || PrefabUtility.GetPrefabAssetType(_prefab) != PrefabAssetType.NotAPrefab
                && !_prefab.scene.IsValid();

            GameObject instance = isPrefabAsset
                ? (GameObject)PrefabUtility.InstantiatePrefab(_prefab)
                : Instantiate(_prefab);

            if (instance != null)
                instance.transform.SetParent(parent, worldPositionStays: false);

            return instance;
        }

        [ContextMenu("Reset Path To Default Arc")]
        public void ResetPathToDefaultArc()
        {
            if (Container == null)
                return;

            if (Container.Splines.Count == 0)
                Container.AddSpline();

            Spline spline = Container.Spline;
            // Spline data is serialized on the container, so record that for a working undo.
            Undo.RecordObject(Container, "Reset Spline Path");
            spline.Clear();
            spline.Add(new BezierKnot(new float3(-5f, 0f, 0f)));
            spline.Add(new BezierKnot(new float3(0f, 3f, 0f)));
            spline.Add(new BezierKnot(new float3(5f, 0f, 0f)));
            spline.SetTangentMode(TangentMode.AutoSmooth);
            ScheduleRebuild();
        }
#endif

        static void DestroyObject(GameObject target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
