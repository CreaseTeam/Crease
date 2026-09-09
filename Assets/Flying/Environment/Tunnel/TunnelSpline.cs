using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crease.Flying.Environment
{
    /// <summary>
    /// Instantiates prefabs along a spline, matching Spline Instantiate, using segments that
    /// each have their own prefab list and a relative share of the path length.
    /// Can also extrude a hollow pipe MeshCollider that seals the player inside the tunnel.
    /// </summary>
    [ExecuteInEditMode]
    [AddComponentMenu("Crease/Tunnel Spline")]
    public class TunnelSpline : SplineComponent
    {
        const string InstancesRootName = "tunnel-root-";
        const string PipeRootName = "tunnel-pipe-";
        const float Epsilon = 0.001f;

        /// <summary>
        /// How items are chosen from the instantiate list.
        /// </summary>
        public enum PlacementMode
        {
            /// <summary>Pick items randomly using each item's probability weight.</summary>
            [InspectorName("Weighted Random")]
            WeightedRandom,
            /// <summary>Repeat the items list in order, skipping empty entries.</summary>
            [InspectorName("Ordered")]
            Ordered
        }

        /// <summary>
        /// The space in which to interpret an offset.
        /// </summary>
        public enum OffsetSpace
        {
            [InspectorName("Spline Element")]
            Spline = Space.Spline,
            [InspectorName("Spline Object")]
            Local = Space.Local,
            [InspectorName("World Space")]
            World = Space.World,
            [InspectorName("Instantiated Object")]
            Object
        }

        /// <summary>
        /// How instances are spaced along the spline.
        /// </summary>
        public enum Method
        {
            [InspectorName("Instance Count")]
            InstanceCount,
            [InspectorName("Spline Distance")]
            SpacingDistance,
            [InspectorName("Linear Distance")]
            LinearDistance
        }

        /// <summary>
        /// Coordinate space used to orient instantiated objects.
        /// </summary>
        public enum Space
        {
            [InspectorName("Spline Element")]
            Spline,
            [InspectorName("Spline Object")]
            Local,
            [InspectorName("World Space")]
            World
        }

        /// <summary>
        /// A prefab to instantiate and its weight when using weighted random placement.
        /// </summary>
        [Serializable]
        public struct InstantiableItem
        {
            public GameObject Prefab;
            public float Probability;
        }

        /// <summary>
        /// A stretch of the spline with its own prefab list and a relative share of the path length.
        /// </summary>
        [Serializable]
        public class Segment
        {
            [Tooltip("Relative share of the path. Under Instance Count this is a share of the instance count; under distance methods it is a share of the spline length.")]
            [Min(0f)]
            public float LengthProportion = 1f;
            public List<InstantiableItem> Items = new List<InstantiableItem>();
        }

        [Serializable]
        public struct Vector3Offset
        {
            [Flags]
            public enum Setup
            {
                None = 0x0,
                HasOffset = 0x1,
                HasCustomSpace = 0x2
            }

            public Setup setup;
            public Vector3 min;
            public Vector3 max;
            public bool randomX;
            public bool randomY;
            public bool randomZ;
            public OffsetSpace space;

            public bool HasOffset => (setup & Setup.HasOffset) != 0;
            public bool HasCustomSpace => (setup & Setup.HasCustomSpace) != 0;

            internal Vector3 GetNextOffset()
            {
                if (!HasOffset)
                    return Vector3.zero;

                return new Vector3(
                    randomX ? UnityEngine.Random.Range(min.x, max.x) : min.x,
                    randomY ? UnityEngine.Random.Range(min.y, max.y) : min.y,
                    randomZ ? UnityEngine.Random.Range(min.z, max.z) : min.z);
            }

            internal void CheckMinMaxValidity()
            {
                max.x = Mathf.Max(min.x, max.x);
                max.y = Mathf.Max(min.y, max.y);
                max.z = Mathf.Max(min.z, max.z);
            }

            internal void CheckMinMax()
            {
                CheckMinMaxValidity();
                if (max.magnitude > 0)
                    setup |= Setup.HasOffset;
                else
                    setup &= ~Setup.HasOffset;
            }

            internal void CheckCustomSpace(Space instanceSpace)
            {
                if ((int)space == (int)instanceSpace)
                    setup &= ~Setup.HasCustomSpace;
                else
                    setup |= Setup.HasCustomSpace;
            }
        }

        [SerializeField]
        SplineContainer _container;

        [SerializeField]
        List<Segment> _segments = new List<Segment>();

        [SerializeField]
        PlacementMode _placementMode = PlacementMode.Ordered;

        [SerializeField]
        Method _method = Method.SpacingDistance;

        [SerializeField]
        Space _space = Space.Spline;

        [SerializeField]
        Vector2 _spacing = new(1f, 1f);

        [SerializeField]
        AlignAxis _up = AlignAxis.YAxis;

        [SerializeField]
        AlignAxis _forward = AlignAxis.ZAxis;

        [SerializeField]
        Vector3Offset _positionOffset;

        [SerializeField]
        Vector3Offset _rotationOffset;

        [SerializeField]
        Vector3Offset _scaleOffset;

        [SerializeField]
        bool _autoRefresh = true;

        [SerializeField]
        int _seed;

        [SerializeField]
        bool _generatePipeCollider = true;

        [SerializeField]
        [Min(0.01f)]
        float _pipeRadius = 2f;

        [SerializeField]
        AnimationCurve _pipeRadiusCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [SerializeField]
        [Range(2, 256)]
        int _pipeRingCount = 32;

        [SerializeField]
        [Range(3, 64)]
        int _pipeRadialSegments = 12;

        [SerializeField]
        [Min(0f)]
        float _pipeThickness;

        [SerializeField]
        bool _pipeCapEnds;

        [SerializeField]
        string _pipeTag = "Untagged";

        [SerializeField]
        int _pipeLayer;

        [SerializeField]
        PhysicsMaterial _pipePhysicsMaterial;

        GameObject _instancesRoot;
        GameObject _pipeRoot;
        MeshCollider _pipeCollider;
        Mesh _pipeMesh;
        readonly List<GameObject> _instances = new();
        readonly List<GameObject> _instanceSources = new();
        readonly List<float> _timesCache = new();
        readonly List<float> _lengthsCache = new();
        bool _instancesCacheDirty;
        bool _splineDirty;
        PlacementMode _lastPlacementMode;
        int _segmentLayoutHash;

        public SplineContainer Container
        {
            get => _container;
            set => _container = value;
        }

        public Segment[] Segments
        {
            get => _segments.ToArray();
            set
            {
                _segments.Clear();
                if (value != null)
                    _segments.AddRange(value);
                SetDirty();
            }
        }

        public PlacementMode ItemPlacement
        {
            get => _placementMode;
            set
            {
                if (_placementMode == value)
                    return;
                _placementMode = value;
                SetDirty();
            }
        }

        public Method InstantiateMethod
        {
            get => _method;
            set => _method = value;
        }

        public Space CoordinateSpace
        {
            get => _space;
            set => _space = value;
        }

        public float MinSpacing
        {
            get => _spacing.x;
            set
            {
                _spacing = new Vector2(value, _spacing.y);
                ValidateSpacing();
            }
        }

        public float MaxSpacing
        {
            get => _spacing.y;
            set
            {
                _spacing = new Vector2(_spacing.x, value);
                ValidateSpacing();
            }
        }

        public AlignAxis UpAxis
        {
            get => _up;
            set => _up = value;
        }

        public AlignAxis ForwardAxis
        {
            get => _forward;
            set
            {
                _forward = value;
                ValidateAxis();
            }
        }

        public Vector3 MinPositionOffset
        {
            get => _positionOffset.min;
            set
            {
                _positionOffset.min = value;
                _positionOffset.CheckMinMax();
            }
        }

        public Vector3 MaxPositionOffset
        {
            get => _positionOffset.max;
            set
            {
                _positionOffset.max = value;
                _positionOffset.CheckMinMax();
            }
        }

        public OffsetSpace PositionSpace
        {
            get => _positionOffset.space;
            set
            {
                _positionOffset.space = value;
                _positionOffset.CheckCustomSpace(_space);
            }
        }

        public Vector3 MinRotationOffset
        {
            get => _rotationOffset.min;
            set
            {
                _rotationOffset.min = value;
                _rotationOffset.CheckMinMax();
            }
        }

        public Vector3 MaxRotationOffset
        {
            get => _rotationOffset.max;
            set
            {
                _rotationOffset.max = value;
                _rotationOffset.CheckMinMax();
            }
        }

        public OffsetSpace RotationSpace
        {
            get => _rotationOffset.space;
            set
            {
                _rotationOffset.space = value;
                _rotationOffset.CheckCustomSpace(_space);
            }
        }

        public Vector3 MinScaleOffset
        {
            get => _scaleOffset.min;
            set
            {
                _scaleOffset.min = value;
                _scaleOffset.CheckMinMax();
            }
        }

        public Vector3 MaxScaleOffset
        {
            get => _scaleOffset.max;
            set
            {
                _scaleOffset.max = value;
                _scaleOffset.CheckMinMax();
            }
        }

        public OffsetSpace ScaleSpace
        {
            get => _scaleOffset.space;
            set
            {
                _scaleOffset.space = value;
                _scaleOffset.CheckCustomSpace(_space);
            }
        }

        public int Seed
        {
            get => _seed;
            set
            {
                _seed = value;
                _instancesCacheDirty = true;
            }
        }

        public bool AutoRefresh
        {
            get => _autoRefresh;
            set => _autoRefresh = value;
        }

        public bool GeneratePipeCollider
        {
            get => _generatePipeCollider;
            set => _generatePipeCollider = value;
        }

        public float PipeRadius
        {
            get => _pipeRadius;
            set => _pipeRadius = Mathf.Max(0.01f, value);
        }

        public AnimationCurve PipeRadiusCurve
        {
            get => _pipeRadiusCurve;
            set => _pipeRadiusCurve = value;
        }

        public int PipeRingCount
        {
            get => _pipeRingCount;
            set => _pipeRingCount = Mathf.Clamp(value, 2, 256);
        }

        public int PipeRadialSegments
        {
            get => _pipeRadialSegments;
            set => _pipeRadialSegments = Mathf.Clamp(value, 3, 64);
        }

        public float PipeThickness
        {
            get => _pipeThickness;
            set => _pipeThickness = Mathf.Max(0f, value);
        }

        public bool PipeCapEnds
        {
            get => _pipeCapEnds;
            set => _pipeCapEnds = value;
        }

        public string PipeTag
        {
            get => _pipeTag;
            set => _pipeTag = value;
        }

        public int PipeLayer
        {
            get => _pipeLayer;
            set => _pipeLayer = value;
        }

        public PhysicsMaterial PipePhysicsMaterial
        {
            get => _pipePhysicsMaterial;
            set => _pipePhysicsMaterial = value;
        }

        public GameObject InstancesRoot => _instancesRoot;
        public GameObject PipeRoot => _pipeRoot;
        public List<GameObject> Instances => _instances;

        Transform InstancesRootTransform
        {
            get
            {
                if (_instancesRoot == null)
                {
                    _instancesRoot = new GameObject(InstancesRootName + GetInstanceID());
                    _instancesRoot.hideFlags |= HideFlags.HideAndDontSave;
                    _instancesRoot.transform.parent = transform;
                    _instancesRoot.transform.localPosition = Vector3.zero;
                    _instancesRoot.transform.localRotation = Quaternion.identity;
                }

                return _instancesRoot.transform;
            }
        }

        void OnEnable()
        {
            if (_seed == 0)
                AssignNewSeed();

#if UNITY_EDITOR
            Undo.undoRedoPerformed += UndoRedoPerformed;
#endif
            _lastPlacementMode = _placementMode;
            CheckChildrenValidity();
            Spline.Changed += OnSplineChanged;
            _splineDirty = true;

            // Prefab stage applies the isolation transform after OnEnable. Placing here
            // evaluates the spline with a stale matrix, stacking instances at the start.
            if (Application.isPlaying)
                UpdateInstances();
#if UNITY_EDITOR
            else
                EditorApplication.delayCall += DelayedUpdateInstances;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= DelayedUpdateInstances;
            Undo.undoRedoPerformed -= UndoRedoPerformed;
#endif
            Spline.Changed -= OnSplineChanged;
            Clear();
            DestroyPipe();
        }

        void Update()
        {
            if (_splineDirty)
                UpdateInstances();
        }

#if UNITY_EDITOR
        void DelayedUpdateInstances()
        {
            if (this == null || !isActiveAndEnabled)
                return;

            UpdateInstances();
        }

        public void RequestRebuild()
        {
            SetDirty();
            _splineDirty = true;
            if (isActiveAndEnabled)
                UpdateInstances();
        }
#endif

        void OnValidate()
        {
            ValidateSpacing();
            ValidateSegments();
            ValidatePipeSettings();
            _splineDirty = _autoRefresh;
            EnsureItemsValidity();
            _positionOffset.CheckMinMaxValidity();
            _rotationOffset.CheckMinMaxValidity();
            _scaleOffset.CheckMinMaxValidity();

            if (_lastPlacementMode != _placementMode)
            {
                _lastPlacementMode = _placementMode;
                SetDirty();
            }

            int layoutHash = ComputeSegmentLayoutHash();
            if (layoutHash != _segmentLayoutHash)
            {
                _segmentLayoutHash = layoutHash;
                if (_autoRefresh)
                    SetDirty();
            }
        }

        void UndoRedoPerformed()
        {
            _instancesCacheDirty = true;
            _splineDirty = true;
        }

        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
        {
            if (_container != null && _container.Spline == spline)
                _splineDirty = _autoRefresh;
        }

        public void SetSplineDirty(Spline spline)
        {
            if (_container != null && _container.Splines.Contains(spline) && _autoRefresh)
                UpdateInstances();
        }

        public void Clear()
        {
            SetDirty();
            TryClearCache();
        }

        public void SetDirty()
        {
            _instancesCacheDirty = true;
        }

        public void Randomize()
        {
            AssignNewSeed();
            _splineDirty = true;
        }

        void AssignNewSeed()
        {
            Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        void InitContainer()
        {
            if (_container == null)
                _container = GetComponent<SplineContainer>();
        }

        void ValidateSegments()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i] == null)
                    _segments[i] = new Segment();
                if (_segments[i].Items == null)
                    _segments[i].Items = new List<InstantiableItem>();
                if (_segments[i].LengthProportion < 0f)
                    _segments[i].LengthProportion = 0f;
            }
        }

        void ValidatePipeSettings()
        {
            _pipeRadius = Mathf.Max(0.01f, _pipeRadius);
            _pipeRingCount = Mathf.Clamp(_pipeRingCount, 2, 256);
            _pipeRadialSegments = Mathf.Clamp(_pipeRadialSegments, 3, 64);
            _pipeThickness = Mathf.Max(0f, _pipeThickness);
            if (_pipeRadiusCurve == null || _pipeRadiusCurve.length == 0)
                _pipeRadiusCurve = AnimationCurve.Constant(0f, 1f, 1f);
        }

        void EnsureItemsValidity()
        {
            for (int segmentIndex = 0; segmentIndex < _segments.Count; segmentIndex++)
            {
                Segment segment = _segments[segmentIndex];
                if (segment?.Items == null)
                    continue;

                for (int i = 0; i < segment.Items.Count; i++)
                {
                    InstantiableItem item = segment.Items[i];
                    if (item.Prefab == null)
                        continue;

                    if (!transform.IsChildOf(item.Prefab.transform))
                        continue;

                    Debug.LogWarning(
                        $"Instantiating a parent of the TunnelSpline object itself is not permitted ({item.Prefab.name} is a parent of {gameObject.name}).",
                        this);
                    item.Prefab = null;
                    segment.Items[i] = item;
                    SetDirty();
                }
            }
        }

        void CheckChildrenValidity()
        {
            var ids = new List<int>();
            TunnelSpline[] components = GetComponents<TunnelSpline>();
            for (int i = 0; i < components.Length; i++)
                ids.Add(components[i].GetInstanceID());

            for (int i = transform.childCount - 1; i >= 0; --i)
            {
                GameObject child = transform.GetChild(i).gameObject;
                string prefix = null;
                if (child.name.StartsWith(InstancesRootName))
                    prefix = InstancesRootName;
                else if (child.name.StartsWith(PipeRootName))
                    prefix = PipeRootName;
                else
                    continue;

                bool invalid = true;
                for (int idIndex = 0; idIndex < ids.Count; idIndex++)
                {
                    if (child.name.Equals(prefix + ids[idIndex]))
                    {
                        invalid = false;
                        break;
                    }
                }

                if (invalid)
                    DestroyInstance(child);
            }
        }

        void RecoverRuntimeState()
        {
            string rootName = InstancesRootName + GetInstanceID();
            string pipeName = PipeRootName + GetInstanceID();
            GameObject foundRoot = _instancesRoot;
            GameObject foundPipe = _pipeRoot;

            for (int i = transform.childCount - 1; i >= 0; --i)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child.name == rootName)
                {
                    if (foundRoot == null)
                        foundRoot = child;
                    else if (child != foundRoot)
                        DestroyInstance(child);
                }
                else if (child.name == pipeName)
                {
                    if (foundPipe == null)
                        foundPipe = child;
                    else if (child != foundPipe)
                        DestroyInstance(child);
                }
            }

            if (_instancesRoot == null && foundRoot != null)
                _instancesRoot = foundRoot;

            if (_instancesRoot != null &&
                (_instances.Count == 0 || _instances.Count != _instancesRoot.transform.childCount))
                _instancesCacheDirty = true;

            if (_pipeRoot == null && foundPipe != null)
            {
                _pipeRoot = foundPipe;
                _pipeCollider = foundPipe.GetComponent<MeshCollider>();
            }
        }

        int ComputeSegmentLayoutHash()
        {
            unchecked
            {
                int hash = _segments.Count;
                hash = hash * 31 + (int)_placementMode;
                hash = hash * 31 + (int)_method;
                for (int i = 0; i < _segments.Count; i++)
                {
                    Segment segment = _segments[i];
                    if (segment == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    hash = hash * 31 + segment.LengthProportion.GetHashCode();
                    if (segment.Items == null)
                        continue;

                    hash = hash * 31 + segment.Items.Count;
                    for (int itemIndex = 0; itemIndex < segment.Items.Count; itemIndex++)
                    {
                        InstantiableItem item = segment.Items[itemIndex];
                        hash = hash * 31 + (item.Prefab != null ? item.Prefab.GetInstanceID() : 0);
                        hash = hash * 31 + item.Probability.GetHashCode();
                    }
                }

                return hash;
            }
        }

        void ValidateSpacing()
        {
            float xSpacing = Mathf.Max(0.1f, _spacing.x);
            if (_method != Method.LinearDistance)
            {
                float ySpacing = float.IsNaN(_spacing.y) ? xSpacing : Mathf.Max(0.1f, _spacing.y);
                _spacing = new Vector2(xSpacing, Mathf.Max(xSpacing, ySpacing));
                return;
            }

            float linearY = float.IsNaN(_spacing.y) ? _spacing.y : xSpacing;
            _spacing = new Vector2(xSpacing, linearY);
        }

        void ValidateAxis()
        {
            if (_forward == _up || (int)_forward == ((int)_up + 3) % 6)
                _forward = (AlignAxis)(((int)_forward + 1) % 6);
        }

        void TryClearCache()
        {
            if (!_instancesCacheDirty)
            {
                for (int i = 0; i < _instances.Count; i++)
                {
                    if (_instances[i] == null)
                    {
                        _instancesCacheDirty = true;
                        break;
                    }
                }
            }

            if (!_instancesCacheDirty)
                return;

            for (int i = _instances.Count - 1; i >= 0; --i)
                DestroyInstance(_instances[i]);

            DestroyInstance(_instancesRoot);
            _instancesRoot = null;
            _instances.Clear();
            _instanceSources.Clear();
            _instancesCacheDirty = false;
        }

        public void UpdateInstances()
        {
            RecoverRuntimeState();
            TryClearCache();

            if (_container == null)
                InitContainer();

            if (_container == null || _container.Splines.Count == 0 || _segments.Count == 0)
            {
                RebuildPipeCollider();
                _splineDirty = false;
                return;
            }

            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(_seed);
            int index = 0;
            int indexOffset = 0;

            _lengthsCache.Clear();
            float totalSplineLength = 0f;
            for (int splineIndex = 0; splineIndex < _container.Splines.Count; splineIndex++)
            {
                float length = _container.CalculateLength(splineIndex);
                _lengthsCache.Add(length);
                totalSplineLength += length;
            }

            float spacing = UnityEngine.Random.Range(_spacing.x, _spacing.y);
            float currentDist = 0f;
            float instanceCountModeStep = 0f;

            if (_method == Method.InstanceCount)
            {
                if (spacing == 1)
                    currentDist = totalSplineLength / 2f;
                else if (spacing < 1)
                    currentDist = totalSplineLength + 1f;

                if (_container.Splines.Count == 1)
                    instanceCountModeStep = totalSplineLength / (_container.Splines[0].Closed ? (int)spacing : (int)spacing - 1);
                else
                    instanceCountModeStep = totalSplineLength / ((int)spacing - 1);
            }

            EnsureItemsValidity();
            int[] orderedCounts = new int[_segments.Count];
            int[] instanceQuotas = _method == Method.InstanceCount
                ? AllocateSegmentInstanceCounts((int)spacing)
                : null;
            int instanceSlotIndex = 0;
            float previousLength = 0f;
            for (int splineIndex = 0; splineIndex < _container.Splines.Count; splineIndex++)
            {
                Spline spline = _container.Splines[splineIndex];
                using var nativeSpline = new NativeSpline(spline, _container.transform.localToWorldMatrix, Allocator.TempJob);
                float splineLength = _lengthsCache[splineIndex];
                bool terminateSpawning = false;

                if (_method == Method.InstanceCount)
                {
                    if (currentDist > splineLength + Epsilon && currentDist <= totalSplineLength + Epsilon)
                    {
                        currentDist -= splineLength;
                        terminateSpawning = true;
                    }
                }
                else
                    currentDist = 0f;

                _timesCache.Clear();
                int timeIndex = 0;

                while (currentDist <= splineLength + Epsilon && !terminateSpawning)
                {
                    float globalDistance = previousLength + Mathf.Clamp(currentDist, 0f, splineLength);
                    bool hasItem = TrySelectItem(
                        globalDistance,
                        totalSplineLength,
                        instanceSlotIndex,
                        instanceQuotas,
                        orderedCounts,
                        out InstantiableItem currentItem,
                        out int prefabIndex);
                    if (hasItem && !SpawnPrefab(index, currentItem, prefabIndex))
                        break;

                    if (hasItem)
                        _timesCache.Add(currentDist / splineLength);

                    if (_method == Method.SpacingDistance)
                    {
                        spacing = UnityEngine.Random.Range(_spacing.x, _spacing.y);
                        currentDist += spacing;
                    }
                    else if (_method == Method.InstanceCount)
                    {
                        if (spacing > 1)
                        {
                            float previousDist = currentDist;
                            currentDist += instanceCountModeStep;
                            if (previousDist < splineLength && currentDist > splineLength + Epsilon)
                            {
                                currentDist -= splineLength;
                                terminateSpawning = true;
                            }
                        }
                        else
                            currentDist += totalSplineLength;
                    }
                    else if (_method == Method.LinearDistance)
                    {
                        if (float.IsNaN(_spacing.y))
                            spacing = hasItem ? GetAutoLinearSpacing(index) : _spacing.x;
                        else
                            spacing = UnityEngine.Random.Range(_spacing.x, _spacing.y);

                        if (hasItem)
                        {
                            nativeSpline.GetPointAtLinearDistance(_timesCache[timeIndex], spacing, out float nextT);
                            currentDist = nextT >= 1f ? splineLength + 1f : nextT * splineLength;
                        }
                        else
                            currentDist += spacing;
                    }

                    if (hasItem)
                    {
                        index++;
                        timeIndex++;
                    }

                    instanceSlotIndex++;
                }

                previousLength += splineLength;

                for (int i = _instances.Count - 1; i >= index; i--)
                {
                    if (_instances[i] != null)
                        DestroyInstance(_instances[i]);
                    _instances.RemoveAt(i);
                    if (i < _instanceSources.Count)
                        _instanceSources.RemoveAt(i);
                }

                for (int i = indexOffset; i < index; i++)
                {
                    GameObject instance = _instances[i];
                    float splineT = _timesCache[i - indexOffset];

                    nativeSpline.Evaluate(splineT, out float3 position, out float3 direction, out float3 splineUp);
                    instance.transform.position = position;

                    if (_method == Method.LinearDistance)
                    {
                        float3 nextPosition = nativeSpline.EvaluatePosition(i + 1 < index ? _timesCache[i + 1 - indexOffset] : 1f);
                        direction = nextPosition - position;
                    }

                    float3 up = math.normalizesafe(splineUp);
                    float3 forward = math.normalizesafe(direction);
                    if (_space == Space.World)
                    {
                        up = Vector3.up;
                        forward = Vector3.forward;
                    }
                    else if (_space == Space.Local)
                    {
                        up = transform.TransformDirection(Vector3.up);
                        forward = transform.TransformDirection(Vector3.forward);
                    }

                    float3 remappedForward = math.normalizesafe(GetAxis(_forward));
                    float3 remappedUp = math.normalizesafe(GetAxis(_up));
                    Quaternion axisRemapRotation = Quaternion.Inverse(quaternion.LookRotationSafe(remappedForward, remappedUp));
                    instance.transform.rotation = quaternion.LookRotationSafe(forward, up) * axisRemapRotation;

                    float3 customUp = up;
                    float3 customForward = forward;
                    if (_positionOffset.HasOffset)
                    {
                        if (_positionOffset.HasCustomSpace)
                            GetCustomSpaceAxis(_positionOffset.space, splineUp, direction, instance.transform, out customUp, out customForward);

                        Vector3 offset = _positionOffset.GetNextOffset();
                        Vector3 right = Vector3.Cross(customUp, customForward).normalized;
                        instance.transform.position += offset.x * right + offset.y * (Vector3)customUp + offset.z * (Vector3)customForward;
                    }

                    if (_scaleOffset.HasOffset)
                    {
                        customUp = up;
                        customForward = forward;
                        if (_scaleOffset.HasCustomSpace)
                            GetCustomSpaceAxis(_scaleOffset.space, splineUp, direction, instance.transform, out customUp, out customForward);

                        customUp = instance.transform.InverseTransformDirection(customUp).normalized;
                        customForward = instance.transform.InverseTransformDirection(customForward).normalized;

                        Vector3 offset = _scaleOffset.GetNextOffset();
                        Vector3 right = Vector3.Cross(customUp, customForward).normalized;
                        instance.transform.localScale += offset.x * right + offset.y * (Vector3)customUp + offset.z * (Vector3)customForward;
                    }

                    if (_rotationOffset.HasOffset)
                    {
                        customUp = up;
                        customForward = forward;
                        if (_rotationOffset.HasCustomSpace)
                        {
                            GetCustomSpaceAxis(_rotationOffset.space, splineUp, direction, instance.transform, out customUp, out customForward);
                            if (_rotationOffset.space == OffsetSpace.Object)
                                axisRemapRotation = quaternion.identity;
                        }

                        Vector3 offset = _rotationOffset.GetNextOffset();
                        Vector3 right = Vector3.Cross(customUp, customForward).normalized;
                        customForward = Quaternion.AngleAxis(offset.y, customUp) * Quaternion.AngleAxis(offset.x, right) * customForward;
                        customUp = Quaternion.AngleAxis(offset.x, right) * Quaternion.AngleAxis(offset.z, customForward) * customUp;
                        instance.transform.rotation = quaternion.LookRotationSafe(customForward, customUp) * axisRemapRotation;
                    }
                }

                indexOffset = index;
            }

            RebuildPipeCollider();
            _splineDirty = false;
            UnityEngine.Random.state = randomState;
        }

        float GetAutoLinearSpacing(int index)
        {
            MeshFilter meshFilter = _instances[index].GetComponent<MeshFilter>();
            Vector3 axis = Vector3.right;
            if (_forward == AlignAxis.ZAxis || _forward == AlignAxis.NegativeZAxis)
                axis = Vector3.forward;
            if (_forward == AlignAxis.YAxis || _forward == AlignAxis.NegativeYAxis)
                axis = Vector3.up;

            if (meshFilter == null)
            {
                meshFilter = _instances[index].GetComponentInChildren<MeshFilter>();
                if (meshFilter != null)
                    axis = Vector3.Scale(meshFilter.transform.InverseTransformDirection(_instances[index].transform.TransformDirection(axis)), meshFilter.transform.lossyScale);
            }

            if (meshFilter == null || meshFilter.sharedMesh == null)
                return _spacing.x;

            Bounds bounds = meshFilter.sharedMesh.bounds;
            MeshFilter[] filters = meshFilter.GetComponentsInChildren<MeshFilter>();
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null)
                    continue;
                Bounds localBounds = filters[i].sharedMesh.bounds;
                bounds.size = new Vector3(
                    Mathf.Max(bounds.size.x, localBounds.size.x),
                    Mathf.Max(bounds.size.y, localBounds.size.y),
                    Mathf.Max(bounds.size.z, localBounds.size.z));
            }

            return Vector3.Scale(bounds.size, axis).magnitude;
        }

        bool SpawnPrefab(int index, InstantiableItem currentItem, int prefabIndex)
        {
            if (currentItem.Prefab == null)
                return false;

            bool sourceMatches = index < _instanceSources.Count && _instanceSources[index] == currentItem.Prefab;
            if (index < _instances.Count && (!sourceMatches || _instances[index] == null))
            {
                DestroyInstance(_instances[index]);
                GameObject replacement = CreateInstanceObject(currentItem, prefabIndex);
                if (replacement == null)
                    return false;
                _instances[index] = replacement;
                if (index < _instanceSources.Count)
                    _instanceSources[index] = currentItem.Prefab;
                else
                    _instanceSources.Add(currentItem.Prefab);
            }
            else if (index >= _instances.Count)
            {
                GameObject created = CreateInstanceObject(currentItem, prefabIndex);
                if (created == null)
                    return false;
                _instances.Add(created);
                _instanceSources.Add(currentItem.Prefab);
            }

            _instances[index].transform.localPosition = currentItem.Prefab.transform.localPosition;
            _instances[index].transform.localRotation = currentItem.Prefab.transform.localRotation;
            _instances[index].transform.localScale = currentItem.Prefab.transform.localScale;
            return true;
        }

        GameObject CreateInstanceObject(InstantiableItem currentItem, int prefabIndex)
        {
            GameObject instance;
#if UNITY_EDITOR
            PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(currentItem.Prefab);
            if (assetType == PrefabAssetType.MissingAsset)
            {
                Debug.LogError($"Trying to instantiate a missing asset for item index [{prefabIndex}].", this);
                return null;
            }

            if (assetType != PrefabAssetType.NotAPrefab && !Application.isPlaying)
                instance = InstantiatePrefab(currentItem.Prefab);
            else
#endif
                instance = Instantiate(currentItem.Prefab, InstancesRootTransform);

#if UNITY_EDITOR
            instance.hideFlags |= HideFlags.HideAndDontSave;
            GameObjectUtility.SetStaticEditorFlags(instance, GameObjectUtility.GetStaticEditorFlags(gameObject));
#endif
            return instance;
        }

#if UNITY_EDITOR
        GameObject InstantiatePrefab(GameObject prefab)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(prefab) && !PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                GameObject newInstance = Instantiate(prefab, InstancesRootTransform);
                GameObject originalPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab);
                if (originalPrefab != null && PrefabUtility.IsAnyPrefabInstanceRoot(prefab))
                {
                    var convertSettings = new ConvertToPrefabInstanceSettings
                    {
                        changeRootNameToAssetName = false,
                        objectMatchMode = ObjectMatchMode.ByHierarchy,
                        componentsNotMatchedBecomesOverride = true,
                        gameObjectsNotMatchedBecomesOverride = true,
                        recordPropertyOverridesOfMatches = true
                    };
                    PrefabUtility.ConvertToPrefabInstance(newInstance, originalPrefab, convertSettings, InteractionMode.AutomatedAction);
                }

                return newInstance;
            }

            return PrefabUtility.InstantiatePrefab(prefab, InstancesRootTransform) as GameObject;
        }
#endif

        bool TrySelectItem(
            float globalDistance,
            float totalLength,
            int instanceSlotIndex,
            int[] instanceQuotas,
            int[] orderedCounts,
            out InstantiableItem item,
            out int prefabIndex)
        {
            item = default;
            prefabIndex = 0;
            int segmentIndex;
            bool foundSegment = instanceQuotas != null
                ? TryGetSegmentIndexByInstance(instanceSlotIndex, instanceQuotas, out segmentIndex)
                : TryGetSegmentIndex(globalDistance, totalLength, out segmentIndex);
            if (!foundSegment)
                return false;

            Segment segment = _segments[segmentIndex];
            if (segment?.Items == null || segment.Items.Count == 0)
                return false;

            if (_placementMode == PlacementMode.Ordered)
                prefabIndex = GetOrderedPrefabIndex(segment.Items, orderedCounts[segmentIndex]);
            else
                prefabIndex = GetWeightedPrefabIndex(segment.Items);

            if (prefabIndex < 0 || prefabIndex >= segment.Items.Count)
                return false;

            item = segment.Items[prefabIndex];
            if (item.Prefab == null)
                return false;

            orderedCounts[segmentIndex]++;
            return true;
        }

        int[] AllocateSegmentInstanceCounts(int totalInstances)
        {
            var counts = new int[_segments.Count];
            if (totalInstances <= 0 || _segments.Count == 0)
                return counts;

            float totalProportion = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i] != null)
                    totalProportion += Mathf.Max(0f, _segments[i].LengthProportion);
            }

            if (totalProportion <= 0f)
                return counts;

            var remainders = new float[_segments.Count];
            int assigned = 0;
            for (int i = 0; i < _segments.Count; i++)
            {
                float proportion = _segments[i] != null ? Mathf.Max(0f, _segments[i].LengthProportion) : 0f;
                if (proportion <= 0f)
                {
                    remainders[i] = -1f;
                    continue;
                }

                float share = totalInstances * (proportion / totalProportion);
                int whole = Mathf.FloorToInt(share);
                counts[i] = whole;
                remainders[i] = share - whole;
                assigned += whole;
            }

            int leftover = totalInstances - assigned;
            for (int n = 0; n < leftover; n++)
            {
                int best = -1;
                float bestRemainder = -1f;
                for (int i = 0; i < _segments.Count; i++)
                {
                    if (remainders[i] > bestRemainder)
                    {
                        bestRemainder = remainders[i];
                        best = i;
                    }
                }

                if (best < 0)
                    break;

                counts[best]++;
                remainders[best] = -1f;
            }

            return counts;
        }

        static bool TryGetSegmentIndexByInstance(int instanceSlotIndex, int[] quotas, out int segmentIndex)
        {
            segmentIndex = 0;
            if (quotas == null || quotas.Length == 0)
                return false;

            int cursor = 0;
            int lastValid = -1;
            for (int i = 0; i < quotas.Length; i++)
            {
                if (quotas[i] <= 0)
                    continue;

                lastValid = i;
                cursor += quotas[i];
                if (instanceSlotIndex < cursor)
                {
                    segmentIndex = i;
                    return true;
                }
            }

            if (lastValid < 0)
                return false;

            segmentIndex = lastValid;
            return true;
        }

        bool TryGetSegmentIndex(float globalDistance, float totalLength, out int segmentIndex)
        {
            segmentIndex = 0;
            if (_segments.Count == 0)
                return false;

            float totalProportion = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i] != null)
                    totalProportion += Mathf.Max(0f, _segments[i].LengthProportion);
            }

            if (totalProportion <= 0f)
                return false;

            float normalized = totalLength > 0f ? Mathf.Clamp01(globalDistance / totalLength) : 0f;
            float cursor = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                float proportion = _segments[i] != null ? Mathf.Max(0f, _segments[i].LengthProportion) : 0f;
                if (proportion <= 0f)
                    continue;

                cursor += proportion / totalProportion;
                if (normalized < cursor || Mathf.Approximately(normalized, cursor))
                {
                    segmentIndex = i;
                    return true;
                }
            }

            for (int i = _segments.Count - 1; i >= 0; i--)
            {
                if (_segments[i] == null || _segments[i].LengthProportion <= 0f)
                    continue;
                segmentIndex = i;
                return true;
            }

            return false;
        }

        static int GetOrderedPrefabIndex(List<InstantiableItem> items, int spawnIndex)
        {
            int validCount = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Prefab != null)
                    validCount++;
            }

            if (validCount == 0)
                return 0;

            int want = spawnIndex % validCount;
            int seen = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Prefab == null)
                    continue;
                if (seen == want)
                    return i;
                seen++;
            }

            return 0;
        }

        static int GetWeightedPrefabIndex(List<InstantiableItem> items)
        {
            float maxProbability = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Prefab != null)
                    maxProbability += items[i].Probability;
            }

            if (items.Count == 1)
                return 0;

            float prefabChoice = UnityEngine.Random.Range(0f, maxProbability);
            float currentProbability = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Prefab == null)
                    continue;

                float itemProbability = items[i].Probability;
                if (prefabChoice < currentProbability + itemProbability)
                    return i;

                currentProbability += itemProbability;
            }

            return 0;
        }

        void GetCustomSpaceAxis(
            OffsetSpace space,
            float3 splineUp,
            float3 direction,
            Transform instanceTransform,
            out float3 customUp,
            out float3 customForward)
        {
            customUp = Vector3.up;
            customForward = Vector3.forward;
            if (space == OffsetSpace.Local)
            {
                customUp = transform.TransformDirection(Vector3.up);
                customForward = transform.TransformDirection(Vector3.forward);
            }
            else if (space == OffsetSpace.Spline)
            {
                customUp = splineUp;
                customForward = direction;
            }
            else if (space == OffsetSpace.Object)
            {
                customUp = instanceTransform.TransformDirection(Vector3.up);
                customForward = instanceTransform.TransformDirection(Vector3.forward);
            }
        }

        public void RebuildPipeCollider()
        {
            if (!_generatePipeCollider)
            {
                DestroyPipe();
                return;
            }

            if (_container == null)
                InitContainer();

            if (_container == null || _container.Splines.Count == 0)
            {
                DestroyPipe();
                return;
            }

            if (!TryBuildPipeMesh())
            {
                DestroyPipe();
                return;
            }

            EnsurePipeCollider();
            ApplyPipeColliderSettings();
        }

        public void PreparePipeForBake()
        {
            RebuildPipeCollider();
            if (_pipeRoot == null)
                return;

            _pipeRoot.hideFlags = HideFlags.None;
            _pipeRoot.name = "Tunnel Pipe Collider";
            _pipeRoot.transform.SetParent(transform, true);
            if (_pipeMesh != null)
                _pipeMesh.hideFlags = HideFlags.None;

            _pipeRoot = null;
            _pipeCollider = null;
            _pipeMesh = null;
        }

        void EnsurePipeCollider()
        {
            if (_pipeRoot == null)
            {
                _pipeRoot = new GameObject(PipeRootName + GetInstanceID());
                _pipeRoot.hideFlags |= HideFlags.HideAndDontSave;
                _pipeRoot.transform.SetParent(transform, false);
                _pipeRoot.transform.localPosition = Vector3.zero;
                _pipeRoot.transform.localRotation = Quaternion.identity;
                _pipeRoot.transform.localScale = Vector3.one;
                _pipeRoot.layer = gameObject.layer;
            }

            _pipeCollider = _pipeRoot.GetComponent<MeshCollider>();
            if (_pipeCollider == null)
                _pipeCollider = _pipeRoot.AddComponent<MeshCollider>();
        }

        void ApplyPipeColliderSettings()
        {
            _pipeRoot.layer = _pipeLayer;
            _pipeRoot.tag = string.IsNullOrEmpty(_pipeTag) ? "Untagged" : _pipeTag;

            _pipeCollider.sharedMesh = null;
            _pipeCollider.sharedMesh = _pipeMesh;
            _pipeCollider.convex = false;
            _pipeCollider.isTrigger = false;
            _pipeCollider.sharedMaterial = _pipePhysicsMaterial;
        }

        void Reset()
        {
            _pipeLayer = gameObject.layer;
            _pipeTag = "Untagged";
        }

        void DestroyPipe()
        {
            if (_pipeCollider != null)
                _pipeCollider.sharedMesh = null;
            _pipeCollider = null;

            if (_pipeMesh != null)
            {
                DestroyInstance(_pipeMesh);
                _pipeMesh = null;
            }

            if (_pipeRoot != null)
            {
                DestroyInstance(_pipeRoot);
                _pipeRoot = null;
            }
        }

        bool TryBuildPipeMesh()
        {
            var rings = new List<PipeRing>();
            SamplePipeRings(rings);
            if (rings.Count < 2)
                return false;

            bool closed = _container.Splines.Count == 1 && _container.Splines[0].Closed;
            int sides = _pipeRadialSegments;
            bool hasThickness = _pipeThickness > 0f;
            int vertsPerRing = hasThickness ? sides * 2 : sides;
            int ringCount = rings.Count;
            int vertexCount = ringCount * vertsPerRing;

            var vertices = new Vector3[vertexCount];
            for (int r = 0; r < ringCount; r++)
            {
                GetRingAxes(rings[r], out Vector3 right, out Vector3 up);
                int ringStart = r * vertsPerRing;
                for (int i = 0; i < sides; i++)
                {
                    float angle = (float)i / sides * Mathf.PI * 2f;
                    Vector3 offset = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    vertices[ringStart + i] = rings[r].LocalPosition + offset * rings[r].Radius;
                    if (hasThickness)
                        vertices[ringStart + sides + i] = rings[r].LocalPosition + offset * (rings[r].Radius + _pipeThickness);
                }
            }

            var triangles = new List<int>(sides * (closed ? ringCount : ringCount - 1) * (hasThickness ? 12 : 6));
            int quadCount = closed ? ringCount : ringCount - 1;
            for (int r = 0; r < quadCount; r++)
            {
                int next = (r + 1) % ringCount;
                int currStart = r * vertsPerRing;
                int nextStart = next * vertsPerRing;
                for (int i = 0; i < sides; i++)
                {
                    int iNext = (i + 1) % sides;
                    AddQuad(triangles, currStart + i, nextStart + i, currStart + iNext, nextStart + iNext, true);
                    if (hasThickness)
                    {
                        int outer = sides;
                        AddQuad(triangles, currStart + outer + i, currStart + outer + iNext, nextStart + outer + i, nextStart + outer + iNext, true);
                    }
                }
            }

            if (_pipeCapEnds)
            {
                int last = (ringCount - 1) * vertsPerRing;
                if (hasThickness)
                {
                    for (int i = 0; i < sides; i++)
                    {
                        int iNext = (i + 1) % sides;
                        AddQuad(triangles, i, sides + i, iNext, sides + iNext, true);
                        AddQuad(triangles, last + i, last + iNext, last + sides + i, last + sides + iNext, true);
                    }
                }
                else
                {
                    AddCap(triangles, 0, sides, true);
                    AddCap(triangles, last, sides, false);
                }
            }

            if (_pipeMesh == null)
            {
                _pipeMesh = new Mesh { name = "TunnelPipeCollider" };
                _pipeMesh.hideFlags |= HideFlags.HideAndDontSave;
            }
            else
                _pipeMesh.Clear();

            if (vertexCount > 65535)
                _pipeMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            _pipeMesh.SetVertices(vertices);
            _pipeMesh.SetTriangles(triangles, 0);
            _pipeMesh.RecalculateNormals();
            _pipeMesh.RecalculateBounds();
            return true;
        }

        void SamplePipeRings(List<PipeRing> rings)
        {
            _lengthsCache.Clear();
            float totalLength = 0f;
            for (int i = 0; i < _container.Splines.Count; i++)
            {
                float length = _container.CalculateLength(i);
                _lengthsCache.Add(length);
                totalLength += length;
            }

            if (totalLength <= 0f)
                return;

            bool closed = _container.Splines.Count == 1 && _container.Splines[0].Closed;
            int ringCount = Mathf.Max(2, _pipeRingCount);

            for (int i = 0; i < ringCount; i++)
            {
                float normalized = closed
                    ? (float)i / ringCount
                    : (float)i / (ringCount - 1);
                if (!TryEvaluatePipeRing(normalized * totalLength, totalLength, out PipeRing ring))
                    continue;
                rings.Add(ring);
            }
        }

        bool TryEvaluatePipeRing(float distance, float totalLength, out PipeRing ring)
        {
            ring = default;
            float remaining = distance;
            int splineCount = _container.Splines.Count;
            for (int splineIndex = 0; splineIndex < splineCount; splineIndex++)
            {
                float splineLength = _lengthsCache[splineIndex];
                bool lastSpline = splineIndex == splineCount - 1;
                if (!lastSpline && remaining > splineLength + Epsilon)
                {
                    remaining -= splineLength;
                    continue;
                }

                float t = splineLength > 0f ? Mathf.Clamp01(remaining / splineLength) : 0f;
                Spline spline = _container.Splines[splineIndex];
                spline.Evaluate(t, out float3 localPos, out float3 localTangent, out float3 localUp);

                Vector3 worldPos = _container.transform.TransformPoint(localPos);
                Vector3 worldTangent = _container.transform.TransformDirection(((Vector3)localTangent).normalized);
                Vector3 worldUp = _container.transform.TransformDirection(((Vector3)localUp).normalized);
                float normalized = totalLength > 0f ? Mathf.Clamp01(distance / totalLength) : 0f;

                ring = new PipeRing
                {
                    LocalPosition = transform.InverseTransformPoint(worldPos),
                    LocalTangent = transform.InverseTransformDirection(worldTangent),
                    LocalUp = transform.InverseTransformDirection(worldUp),
                    Radius = EvaluatePipeRadius(normalized)
                };
                return true;
            }

            return false;
        }

        float EvaluatePipeRadius(float normalized)
        {
            float scale = 1f;
            if (_pipeRadiusCurve != null && _pipeRadiusCurve.length > 0)
                scale = Mathf.Max(0.001f, _pipeRadiusCurve.Evaluate(normalized));
            return _pipeRadius * scale;
        }

        static void GetRingAxes(PipeRing ring, out Vector3 right, out Vector3 up)
        {
            Vector3 forward = ring.LocalTangent.sqrMagnitude > 0.0001f ? ring.LocalTangent.normalized : Vector3.forward;
            up = ring.LocalUp.sqrMagnitude > 0.0001f ? ring.LocalUp.normalized : Vector3.up;
            right = Vector3.Cross(up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(Vector3.up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;
        }

        static void AddQuad(List<int> triangles, int a, int c, int b, int d, bool inward)
        {
            if (inward)
            {
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
                return;
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(d);
            triangles.Add(c);
        }

        static void AddCap(List<int> triangles, int ringStart, int sides, bool start)
        {
            for (int i = 1; i < sides - 1; i++)
            {
                if (start)
                {
                    triangles.Add(ringStart);
                    triangles.Add(ringStart + i);
                    triangles.Add(ringStart + i + 1);
                }
                else
                {
                    triangles.Add(ringStart);
                    triangles.Add(ringStart + i + 1);
                    triangles.Add(ringStart + i);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!_generatePipeCollider || _container == null || _container.Splines.Count == 0)
                return;

            var rings = new List<PipeRing>();
            SamplePipeRings(rings);
            if (rings.Count < 2)
                return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.85f, 0.55f, 0.2f, 0.9f);
            int previewSides = Mathf.Min(_pipeRadialSegments, 16);
            for (int r = 0; r < rings.Count; r++)
            {
                GetRingAxes(rings[r], out Vector3 right, out Vector3 up);
                Vector3 prev = rings[r].LocalPosition + right * rings[r].Radius;
                for (int i = 1; i <= previewSides; i++)
                {
                    float angle = (float)i / previewSides * Mathf.PI * 2f;
                    Vector3 next = rings[r].LocalPosition + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * rings[r].Radius;
                    Gizmos.DrawLine(prev, next);
                    prev = next;
                }

                if (r >= rings.Count - 1)
                    continue;

                GetRingAxes(rings[r + 1], out Vector3 nextRight, out _);
                Gizmos.DrawLine(
                    rings[r].LocalPosition + right * rings[r].Radius,
                    rings[r + 1].LocalPosition + nextRight * rings[r + 1].Radius);
            }
        }

        struct PipeRing
        {
            public Vector3 LocalPosition;
            public Vector3 LocalTangent;
            public Vector3 LocalUp;
            public float Radius;
        }

        static void DestroyInstance(UnityEngine.Object instance)
        {
            if (instance == null)
                return;
#if UNITY_EDITOR
            DestroyImmediate(instance);
#else
            Destroy(instance);
#endif
        }
    }
}
