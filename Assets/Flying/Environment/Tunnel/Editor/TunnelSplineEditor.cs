using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;
using Crease.Flying.Environment;

namespace Crease.Flying.Environment.Editor
{
    class TunnelSplineGizmoDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawGizmos(TunnelSpline tunnelSpline, GizmoType gizmoType)
        {
            foreach (GameObject instance in tunnelSpline.Instances)
            {
                if (instance == null)
                    continue;

                Vector3 pos = instance.transform.position;
                Handles.color = Color.red;
                Handles.DrawAAPolyLine(3f, pos, pos + 0.25f * instance.transform.right);
                Handles.color = Color.green;
                Handles.DrawAAPolyLine(3f, pos, pos + 0.25f * instance.transform.up);
                Handles.color = Color.blue;
                Handles.DrawAAPolyLine(3f, pos, pos + 0.25f * instance.transform.forward);
            }
        }
    }

    [CustomPropertyDrawer(typeof(TunnelSpline.InstantiableItem))]
    class TunnelInstantiableItemDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            SerializedProperty prefabProperty = property.FindPropertyRelative(nameof(TunnelSpline.InstantiableItem.Prefab));
            bool ordered = IsOrdered(property);

            using (new LabelWidthScope(0f))
            {
                if (ordered)
                {
                    EditorGUI.ObjectField(rect, prefabProperty, new GUIContent(""));
                    return;
                }

                SerializedProperty probaProperty = property.FindPropertyRelative(nameof(TunnelSpline.InstantiableItem.Probability));
                EditorGUI.ObjectField(ReserveLineSpace(rect.width - 100f, ref rect), prefabProperty, new GUIContent(""));
                ReserveLineSpace(10f, ref rect);
                EditorGUI.LabelField(ReserveLineSpace(15f, ref rect), new GUIContent("%", "Probability for that element to appear."));
                probaProperty.floatValue = EditorGUI.FloatField(ReserveLineSpace(60f, ref rect), probaProperty.floatValue);
            }
        }

        static bool IsOrdered(SerializedProperty property)
        {
            if (property.serializedObject.targetObject is not TunnelSpline tunnelSpline)
                return false;
            return tunnelSpline.ItemPlacement == TunnelSpline.PlacementMode.Ordered;
        }

        static Rect ReserveLineSpace(float width, ref Rect total)
        {
            Rect current = total;
            current.width = width;
            total.x += width;
            return current;
        }
    }

    [CustomPropertyDrawer(typeof(TunnelSpline.Segment))]
    class TunnelSplineSegmentDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty items = property.FindPropertyRelative("Items");
            return EditorGUIUtility.singleLineHeight
                + EditorGUIUtility.standardVerticalSpacing
                + EditorGUI.GetPropertyHeight(items, true);
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            SerializedProperty proportion = property.FindPropertyRelative("LengthProportion");
            SerializedProperty items = property.FindPropertyRelative("Items");

            Rect header = rect;
            header.height = EditorGUIUtility.singleLineHeight;

            float fieldWidth = Mathf.Max(80f, header.width - 160f);
            Rect proportionRect = new Rect(header.x, header.y, fieldWidth, header.height);
            Rect percentRect = new Rect(header.x + fieldWidth + 8f, header.y, header.width - fieldWidth - 8f, header.height);

            EditorGUI.PropertyField(proportionRect, proportion, new GUIContent(
                "Length Proportion",
                "Relative share of the path. Under Instance Count this is a share of the instance count; under distance methods it is a share of the spline length."));
            string shareLabel = IsInstanceCount(property) ? "of instances" : "of length";
            EditorGUI.LabelField(percentRect, $"{GetNormalizedPercent(property):0.#}% {shareLabel}");

            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            rect.height -= EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(rect, items, new GUIContent("Prefabs"), true);
        }

        static bool IsInstanceCount(SerializedProperty property)
        {
            return property.serializedObject.targetObject is TunnelSpline tunnelSpline
                && tunnelSpline.InstantiateMethod == TunnelSpline.Method.InstanceCount;
        }

        static float GetNormalizedPercent(SerializedProperty segmentProperty)
        {
            SerializedProperty list = GetParentArray(segmentProperty);
            if (list == null || !list.isArray)
                return 0f;

            float total = 0f;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                SerializedProperty proportion = element.FindPropertyRelative("LengthProportion");
                if (proportion != null)
                    total += Mathf.Max(0f, proportion.floatValue);
            }

            SerializedProperty self = segmentProperty.FindPropertyRelative("LengthProportion");
            float value = self != null ? Mathf.Max(0f, self.floatValue) : 0f;
            return total > 0f ? value / total * 100f : 0f;
        }

        static SerializedProperty GetParentArray(SerializedProperty element)
        {
            string path = element.propertyPath;
            int arrayIndex = path.LastIndexOf(".Array.data[", StringComparison.Ordinal);
            if (arrayIndex < 0)
                return null;
            return element.serializedObject.FindProperty(path.Substring(0, arrayIndex));
        }
    }

    [InitializeOnLoad]
    static class TunnelSplinePrefabStageHook
    {
        static TunnelSplinePrefabStageHook()
        {
            UnityEditor.SceneManagement.PrefabStage.prefabStageOpened += OnPrefabStageOpened;
        }

        static void OnPrefabStageOpened(UnityEditor.SceneManagement.PrefabStage stage)
        {
            if (stage?.prefabContentsRoot == null)
                return;

            TunnelSpline[] splines = stage.prefabContentsRoot.GetComponentsInChildren<TunnelSpline>(true);
            for (int i = 0; i < splines.Length; i++)
            {
                if (splines[i] != null && splines[i].isActiveAndEnabled)
                    splines[i].RequestRebuild();
            }
        }
    }

    [CustomEditor(typeof(TunnelSpline), false)]
    [CanEditMultipleObjects]
    class TunnelSplineEditor : UnityEditor.Editor
    {
        enum SpawnType
        {
            Exact,
            Random
        }

        enum OffsetType
        {
            Exact,
            Random
        }

        static readonly string[] SpacingTypesLabels =
        {
            "Count",
            "Spacing (Spline)",
            "Spacing (Linear)"
        };

        SerializedProperty _container;
        SerializedProperty _segments;
        SerializedProperty _placementMode;
        SerializedProperty _instantiateMethod;
        SerializedProperty _seed;
        SerializedProperty _space;
        SerializedProperty _upAxis;
        SerializedProperty _forwardAxis;
        SerializedProperty _spacing;
        SerializedProperty _positionOffset;
        SerializedProperty _rotationOffset;
        SerializedProperty _scaleOffset;
        SerializedProperty _autoRefresh;
        SerializedProperty _generatePipeCollider;
        SerializedProperty _pipeRadius;
        SerializedProperty _pipeRadiusCurve;
        SerializedProperty _pipeRingCount;
        SerializedProperty _pipeRadialSegments;
        SerializedProperty _pipeThickness;
        SerializedProperty _pipeCapEnds;
        SerializedProperty _pipeTag;
        SerializedProperty _pipeLayer;
        SerializedProperty _pipePhysicsMaterial;

        SpawnType _spacingType;
        bool _positionFoldout;
        bool _rotationFoldout;
        bool _scaleFoldout;
        TunnelSpline[] _components;

        TunnelSpline[] Components
        {
            get
            {
                if (_components == null)
                    _components = targets.Select(x => x as TunnelSpline).Where(y => y != null).ToArray();
                return _components;
            }
        }

        void OnEnable()
        {
            Spline.Changed += OnSplineChanged;
            EditorSplineUtility.AfterSplineWasModified += OnSplineModified;
            SplineContainer.SplineAdded += OnContainerSplineSetModified;
            SplineContainer.SplineRemoved += OnContainerSplineSetModified;
        }

        void OnDisable()
        {
            _components = null;
            Spline.Changed -= OnSplineChanged;
            EditorSplineUtility.AfterSplineWasModified -= OnSplineModified;
            SplineContainer.SplineAdded -= OnContainerSplineSetModified;
            SplineContainer.SplineRemoved -= OnContainerSplineSetModified;
        }

        bool Initialize()
        {
            if (_container != null && _components != null && _components.Length > 0)
            {
                if (_generatePipeCollider == null)
                    FindPipeProperties();

                return true;
            }

            _container = serializedObject.FindProperty("_container");
            _segments = serializedObject.FindProperty("_segments");
            _placementMode = serializedObject.FindProperty("_placementMode");
            _instantiateMethod = serializedObject.FindProperty("_method");
            _space = serializedObject.FindProperty("_space");
            _upAxis = serializedObject.FindProperty("_up");
            _forwardAxis = serializedObject.FindProperty("_forward");
            _spacing = serializedObject.FindProperty("_spacing");
            _positionOffset = serializedObject.FindProperty("_positionOffset");
            _rotationOffset = serializedObject.FindProperty("_rotationOffset");
            _scaleOffset = serializedObject.FindProperty("_scaleOffset");
            _seed = serializedObject.FindProperty("_seed");
            _autoRefresh = serializedObject.FindProperty("_autoRefresh");
            FindPipeProperties();

            if (_spacing != null)
            {
                _spacingType = Mathf.Approximately(_spacing.vector2Value.x, _spacing.vector2Value.y)
                    ? SpawnType.Exact
                    : SpawnType.Random;
            }

            _components = targets.Select(x => x as TunnelSpline).Where(y => y != null).ToArray();
            return _components.Length > 0;
        }

        void FindPipeProperties()
        {
            _generatePipeCollider = serializedObject.FindProperty("_generatePipeCollider");
            _pipeRadius = serializedObject.FindProperty("_pipeRadius");
            _pipeRadiusCurve = serializedObject.FindProperty("_pipeRadiusCurve");
            _pipeRingCount = serializedObject.FindProperty("_pipeRingCount");
            _pipeRadialSegments = serializedObject.FindProperty("_pipeRadialSegments");
            _pipeThickness = serializedObject.FindProperty("_pipeThickness");
            _pipeCapEnds = serializedObject.FindProperty("_pipeCapEnds");
            _pipeTag = serializedObject.FindProperty("_pipeTag");
            _pipeLayer = serializedObject.FindProperty("_pipeLayer");
            _pipePhysicsMaterial = serializedObject.FindProperty("_pipePhysicsMaterial");
        }

        void OnSplineModified(Spline spline)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            foreach (TunnelSpline tunnelSpline in Components)
            {
                if (tunnelSpline == null)
                    continue;
                if (tunnelSpline.Container != null && tunnelSpline.Container.Splines.Contains(spline))
                    tunnelSpline.SetSplineDirty(spline);
            }
        }

        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            OnSplineModified(spline);
        }

        void OnContainerSplineSetModified(SplineContainer container, int spline)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            foreach (TunnelSpline tunnelSpline in Components)
            {
                if (tunnelSpline.Container == container)
                    tunnelSpline.UpdateInstances();
            }
        }

        public override void OnInspectorGUI()
        {
            if (!Initialize())
                return;

            serializedObject.Update();

            bool dirtyInstances = false;
            bool updateInstances = false;

            EditorGUILayout.PropertyField(_container);
            if (_container.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Instantiated Objects need a SplineContainer target to be created.", MessageType.Warning);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_placementMode, new GUIContent(
                "Item Placement",
                "How prefabs are chosen within each segment. Weighted Random uses probability. Ordered repeats that segment's list."));
            if (EditorGUI.EndChangeCheck())
            {
                dirtyInstances = true;
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_segments, new GUIContent(
                "Segments",
                "Each segment has its own prefabs and a relative share of the path. Under Instance Count that share is of the instance count."), true);
            dirtyInstances |= EditorGUI.EndChangeCheck();

            DrawSetupSection();
            dirtyInstances |= DrawInstantiateSection();
            updateInstances |= DrawOffsets();
            updateInstances |= DrawPipeColliderSection();

            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_seed, new GUIContent(
                "Randomization Seed",
                "Value used to initialize the pseudorandom number generator of the instances."));
            bool newSeed = EditorGUI.EndChangeCheck();
            dirtyInstances |= newSeed;
            updateInstances |= newSeed;
            EditorGUILayout.PropertyField(_autoRefresh, new GUIContent(
                "Auto Refresh Generation",
                "Automatically refresh the instances when the spline or the values are changed."));
            EditorGUI.indentLevel--;
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Separator();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();
            if (GUILayout.Button(new GUIContent("Randomize", "Compute a new randomization of the instances along the spline."), GUILayout.MaxWidth(100f)))
            {
                Undo.SetCurrentGroupName("Change TunnelSpline Seed");
                int group = Undo.GetCurrentGroup();
                foreach (TunnelSpline tunnelSpline in _components)
                {
                    Undo.RecordObject(tunnelSpline, $"Change TunnelSpline Seed for {tunnelSpline.gameObject.name}");
                    tunnelSpline.Randomize();
                }

                Undo.CollapseUndoOperations(group);
                updateInstances = true;
            }

            bool hasInstances = false;
            foreach (TunnelSpline tunnelSpline in _components)
            {
                if (tunnelSpline.Instances.Count > 0)
                {
                    hasInstances = true;
                    break;
                }
            }

            if (GUILayout.Button(new GUIContent("Regenerate", "Regenerate the instances along the spline."), GUILayout.MaxWidth(100f)))
                updateInstances = true;

            GUI.enabled = hasInstances;
            if (GUILayout.Button(new GUIContent("Clear", "Clear the instances along the spline."), GUILayout.MaxWidth(100f)))
            {
                Undo.SetCurrentGroupName("Clear TunnelSpline");
                int group = Undo.GetCurrentGroup();
                foreach (TunnelSpline tunnelSpline in _components)
                {
                    Undo.RecordObject(tunnelSpline, $"Clear TunnelSpline for {tunnelSpline.gameObject.name}");
                    tunnelSpline.Clear();
                }

                Undo.CollapseUndoOperations(group);
            }

            if (GUILayout.Button(new GUIContent("Bake Instances", "Bake the instances and pipe collider in the Scene for custom editing and destroy this TunnelSpline component."), GUILayout.MaxWidth(120f)))
            {
                foreach (TunnelSpline tunnelSpline in _components)
                    BakeInstances(tunnelSpline);
            }

            GUI.enabled = true;
            EditorGUILayout.Space();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            if (_components == null)
                return;

            foreach (TunnelSpline tunnelSpline in _components)
            {
                if (dirtyInstances)
                    tunnelSpline.SetDirty();
                if (dirtyInstances || updateInstances)
                    tunnelSpline.UpdateInstances();
            }

            if (dirtyInstances || updateInstances)
                SceneView.RepaintAll();
        }

        void DrawSetupSection()
        {
            EditorGUILayout.LabelField("Instantiated Object Setup", EditorStyles.boldLabel);
            GUILayout.Space(5f);
            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            _upAxis.intValue = (int)(SplineComponent.AlignAxis)EditorGUILayout.EnumPopup(
                new GUIContent("Up Axis", "Object axis to use as Up Direction when instantiating on the Spline (default is Y)."),
                (SplineComponent.AlignAxis)_upAxis.intValue);

            Rect forwardRect = EditorGUILayout.GetControlRect();
            _forwardAxis.intValue = (int)(SplineComponent.AlignAxis)EditorGUI.EnumPopup(
                forwardRect,
                new GUIContent("Forward Axis", "Object axis to use as Forward Direction when instantiating on the Spline (default is Z)."),
                (SplineComponent.AlignAxis)_forwardAxis.intValue,
                item =>
                {
                    int axisItem = (int)(SplineComponent.AlignAxis)item;
                    return !(axisItem == _upAxis.intValue || axisItem == (_upAxis.intValue + 3) % 6);
                });
            if (EditorGUI.EndChangeCheck())
            {
                if (_forwardAxis.intValue == _upAxis.intValue || _forwardAxis.intValue == (_upAxis.intValue + 3) % 6)
                    _forwardAxis.intValue = (_forwardAxis.intValue + 1) % 6;
            }

            EditorGUILayout.PropertyField(_space, new GUIContent("Align To", "Define the space to use to orient the instantiated object."));
            EditorGUI.indentLevel--;
        }

        bool DrawInstantiateSection()
        {
            bool dirty = false;
            Vector2 spacingV2 = _spacing.vector2Value;

            EditorGUILayout.LabelField("Instantiation", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_instantiateMethod, new GUIContent("Instantiate Method", "How instances are generated along the spline."));
            if (EditorGUI.EndChangeCheck())
            {
                if (_spacingType == SpawnType.Random && _instantiateMethod.intValue == (int)TunnelSpline.Method.LinearDistance)
                    _spacing.vector2Value = new Vector2(spacingV2.x, float.NaN);
                dirty = true;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(SpacingTypesLabels[_instantiateMethod.intValue]));
            EditorGUI.indentLevel--;
            GUILayout.Space(2f);

            EditorGUI.BeginChangeCheck();
            float spacingX = _spacing.vector2Value.x;
            bool isExact = _spacingType == SpawnType.Exact;
            if (isExact || _instantiateMethod.intValue != (int)TunnelSpline.Method.LinearDistance)
            {
                using (new LabelWidthScope(30f))
                {
                    spacingX = (TunnelSpline.Method)_instantiateMethod.intValue == TunnelSpline.Method.InstanceCount
                        ? EditorGUILayout.IntField(new GUIContent(isExact ? string.Empty : "Min"), (int)_spacing.vector2Value.x, GUILayout.MinWidth(50f))
                        : EditorGUILayout.FloatField(new GUIContent(isExact ? "Dist" : "Min"), _spacing.vector2Value.x, GUILayout.MinWidth(50f));
                }
            }

            if (isExact)
                spacingV2 = new Vector2(spacingX, spacingX);
            else if (_instantiateMethod.intValue != (int)TunnelSpline.Method.LinearDistance)
            {
                using (new LabelWidthScope(30f))
                {
                    float spacingY = (TunnelSpline.Method)_instantiateMethod.intValue == TunnelSpline.Method.InstanceCount
                        ? EditorGUILayout.IntField(new GUIContent("Max"), (int)_spacing.vector2Value.y, GUILayout.MinWidth(50f))
                        : EditorGUILayout.FloatField(new GUIContent("Max"), _spacing.vector2Value.y, GUILayout.MinWidth(50f));

                    if (spacingX > _spacing.vector2Value.y)
                        spacingY = spacingX;
                    else if (spacingY < _spacing.vector2Value.x)
                        spacingX = spacingY;

                    spacingV2 = new Vector2(spacingX, spacingY);
                }
            }

            if (EditorGUI.EndChangeCheck())
                _spacing.vector2Value = spacingV2;

            EditorGUI.BeginChangeCheck();
            if (_instantiateMethod.intValue != (int)TunnelSpline.Method.LinearDistance)
                _spacingType = (SpawnType)EditorGUILayout.EnumPopup(_spacingType, GUILayout.MinWidth(30f));
            else
            {
                _spacingType = (SpawnType)EditorGUILayout.Popup(
                    _spacingType == SpawnType.Exact ? 0 : 1,
                    new[] { "Exact", "Auto" },
                    GUILayout.MinWidth(30f));
            }

            if (EditorGUI.EndChangeCheck())
            {
                if (_spacingType == SpawnType.Exact)
                    _spacing.vector2Value = new Vector2(spacingV2.x, spacingV2.x);
                else if (_instantiateMethod.intValue == (int)TunnelSpline.Method.LinearDistance)
                    _spacing.vector2Value = new Vector2(spacingV2.x, float.NaN);
                dirty = true;
            }

            EditorGUILayout.EndHorizontal();
            return dirty;
        }

        bool DrawPipeColliderSection()
        {
            EditorGUILayout.LabelField("Pipe Collider", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_generatePipeCollider, new GUIContent(
                "Generate Collider",
                "Extrude a hollow pipe MeshCollider along the spline that keeps objects inside the tunnel."));
            if (_generatePipeCollider.boolValue)
            {
                EditorGUILayout.PropertyField(_pipeRadius, new GUIContent(
                    "Radius",
                    "Inner radius of the pipe collider."));
                EditorGUILayout.PropertyField(_pipeRadiusCurve, new GUIContent(
                    "Radius Curve",
                    "Multiplies Radius along the spline (0 = start, 1 = end)."));
                EditorGUILayout.PropertyField(_pipeRingCount, new GUIContent(
                    "Sample Rings",
                    "How many rings are sampled along the spline. Higher is smoother on bends."));
                EditorGUILayout.PropertyField(_pipeRadialSegments, new GUIContent(
                    "Radial Sides",
                    "How many sides make up each ring. Higher is a rounder pipe."));
                EditorGUILayout.PropertyField(_pipeThickness, new GUIContent(
                    "Thickness",
                    "Extra outer wall thickness. 0 builds a single inward-facing wall."));
                EditorGUILayout.PropertyField(_pipeCapEnds, new GUIContent(
                    "Cap Ends",
                    "Close the start and end of the pipe so nothing can enter or leave along the spline."));

                EditorGUILayout.Space(4f);
                _pipeTag.stringValue = EditorGUILayout.TagField(
                    new GUIContent("Tag", "Tag assigned to the generated pipe collider object."),
                    _pipeTag.stringValue);
                _pipeLayer.intValue = EditorGUILayout.LayerField(
                    new GUIContent("Layer", "Layer assigned to the generated pipe collider object."),
                    _pipeLayer.intValue);
                EditorGUILayout.PropertyField(_pipePhysicsMaterial, new GUIContent(
                    "Physics Material",
                    "Optional physics material on the pipe collider."));
            }

            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.indentLevel--;
            return changed;
        }

        bool DrawOffsets()
        {
            bool updateNeeded = DrawOffsetProperties(_positionOffset, new GUIContent("Position Offset", "Whether or not to use a position offset."), _positionFoldout, out _positionFoldout);
            updateNeeded |= DrawOffsetProperties(_rotationOffset, new GUIContent("Rotation Offset", "Whether or not to use a rotation offset."), _rotationFoldout, out _rotationFoldout);
            updateNeeded |= DrawOffsetProperties(_scaleOffset, new GUIContent("Scale Offset", "Whether or not to use a scale offset."), _scaleFoldout, out _scaleFoldout);
            return updateNeeded;
        }

        bool DrawOffsetProperties(SerializedProperty offsetProperty, GUIContent content, bool foldoutValue, out bool newFoldoutValue)
        {
            bool changed = false;
            newFoldoutValue = foldoutValue;

            EditorGUILayout.BeginHorizontal();
            using (new LabelWidthScope(0f))
            {
                SerializedProperty setupProperty = offsetProperty.FindPropertyRelative("setup");
                var setup = (TunnelSpline.Vector3Offset.Setup)setupProperty.intValue;
                bool hasOffset = (setup & TunnelSpline.Vector3Offset.Setup.HasOffset) != 0;

                EditorGUI.BeginChangeCheck();
                hasOffset = EditorGUILayout.Toggle(hasOffset, GUILayout.MaxWidth(20f));
                if (EditorGUI.EndChangeCheck())
                {
                    if (hasOffset)
                        setup |= TunnelSpline.Vector3Offset.Setup.HasOffset;
                    else
                        setup &= ~TunnelSpline.Vector3Offset.Setup.HasOffset;
                    setupProperty.intValue = (int)setup;
                    changed = true;
                }

                EditorGUILayout.Space(10f);
                using (new EditorGUI.DisabledScope(!hasOffset))
                {
                    newFoldoutValue = Foldout(foldoutValue, content) && hasOffset;
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    if (!newFoldoutValue)
                        return changed;

                    EditorGUILayout.BeginHorizontal();
                    bool hasCustomSpace = (setup & TunnelSpline.Vector3Offset.Setup.HasCustomSpace) != 0;
                    EditorGUI.BeginChangeCheck();
                    string space = _space.intValue < 1 ? "Spline Element" : _space.intValue == 1 ? "Spline Object" : "World";
                    hasCustomSpace = EditorGUILayout.Toggle(new GUIContent("Override space", "Override current space (" + space + ")"), hasCustomSpace);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (hasCustomSpace)
                            setup |= TunnelSpline.Vector3Offset.Setup.HasCustomSpace;
                        else
                            setup &= ~TunnelSpline.Vector3Offset.Setup.HasCustomSpace;
                        setupProperty.intValue = (int)setup;
                        changed = true;
                    }

                    SerializedProperty spaceProperty = offsetProperty.FindPropertyRelative("space");
                    using (new EditorGUI.DisabledScope(!hasCustomSpace))
                    {
                        var type = (TunnelSpline.OffsetSpace)spaceProperty.intValue;
                        EditorGUI.BeginChangeCheck();
                        type = (TunnelSpline.OffsetSpace)EditorGUILayout.EnumPopup(type);
                        if (EditorGUI.EndChangeCheck())
                        {
                            spaceProperty.intValue = (int)type;
                            changed = true;
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    SerializedProperty minProperty = offsetProperty.FindPropertyRelative("min");
                    SerializedProperty maxProperty = offsetProperty.FindPropertyRelative("max");
                    Vector3 minPropertyValue = minProperty.vector3Value;
                    Vector3 maxPropertyValue = maxProperty.vector3Value;

                    for (int i = 0; i < 3; i++)
                    {
                        string axisLabel = i == 0 ? "X" : i == 1 ? "Y" : "Z";
                        EditorGUILayout.BeginHorizontal();
                        using (new LabelWidthScope(30f))
                            EditorGUILayout.LabelField(axisLabel);

                        SerializedProperty randomProperty = offsetProperty.FindPropertyRelative("random" + axisLabel);
                        GUILayout.FlexibleSpace();
                        if (randomProperty.boolValue)
                        {
                            EditorGUI.BeginChangeCheck();
                            float min;
                            float max;
                            using (new LabelWidthScope(30f))
                            {
                                min = EditorGUILayout.FloatField("from", minPropertyValue[i], GUILayout.MinWidth(95f), GUILayout.MaxWidth(95f));
                                max = EditorGUILayout.FloatField("  to", maxPropertyValue[i], GUILayout.MinWidth(95f), GUILayout.MaxWidth(95f));
                            }

                            if (EditorGUI.EndChangeCheck())
                            {
                                if (min > maxPropertyValue[i])
                                    maxPropertyValue[i] = min;
                                if (max < minPropertyValue[i])
                                    minPropertyValue[i] = max;
                                minPropertyValue[i] = min;
                                maxPropertyValue[i] = max;
                                minProperty.vector3Value = minPropertyValue;
                                maxProperty.vector3Value = maxPropertyValue;
                                changed = true;
                            }
                        }
                        else
                        {
                            EditorGUI.BeginChangeCheck();
                            float min;
                            using (new LabelWidthScope(30f))
                                min = EditorGUILayout.FloatField("is ", minPropertyValue[i], GUILayout.MinWidth(193f), GUILayout.MaxWidth(193f));

                            if (EditorGUI.EndChangeCheck())
                            {
                                minPropertyValue[i] = min;
                                if (min > maxPropertyValue[i])
                                    maxPropertyValue[i] = min;
                                minProperty.vector3Value = minPropertyValue;
                                maxProperty.vector3Value = maxPropertyValue;
                                changed = true;
                            }
                        }

                        EditorGUI.BeginChangeCheck();
                        OffsetType isOffsetRandom = randomProperty.boolValue ? OffsetType.Random : OffsetType.Exact;
                        using (new LabelWidthScope(0f))
                            isOffsetRandom = (OffsetType)EditorGUILayout.EnumPopup(isOffsetRandom, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f));
                        if (EditorGUI.EndChangeCheck())
                        {
                            randomProperty.boolValue = isOffsetRandom == OffsetType.Random;
                            changed = true;
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            return changed;
        }

        static bool Foldout(bool foldout, GUIContent content)
        {
            var style = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            return EditorGUILayout.Foldout(foldout, content, false, style);
        }

        void BakeInstances(TunnelSpline tunnelSpline)
        {
            Undo.SetCurrentGroupName("Baking TunnelSpline instances");
            int group = Undo.GetCurrentGroup();

            tunnelSpline.UpdateInstances();
            tunnelSpline.PreparePipeForBake();
            for (int i = 0; i < tunnelSpline.Instances.Count; ++i)
            {
                GameObject newInstance = tunnelSpline.Instances[i];
                newInstance.name = "Instance-" + i;
                newInstance.hideFlags = HideFlags.None;
                newInstance.transform.SetParent(tunnelSpline.gameObject.transform, true);
                Undo.RegisterCreatedObjectUndo(newInstance, "Baking instance");
            }

            tunnelSpline.Instances.Clear();
            if (tunnelSpline.InstancesRoot != null)
                Undo.DestroyObjectImmediate(tunnelSpline.InstancesRoot);

            Undo.DestroyObjectImmediate(tunnelSpline);
            Undo.CollapseUndoOperations(group);
        }

    }

    struct LabelWidthScope : IDisposable
    {
        readonly float _previousWidth;

        public LabelWidthScope(float width)
        {
            _previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = width;
        }

        public void Dispose()
        {
            EditorGUIUtility.labelWidth = _previousWidth;
        }
    }
}
