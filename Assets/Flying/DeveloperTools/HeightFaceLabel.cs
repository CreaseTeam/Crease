using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Crease.Flying.DeveloperTools
{
    /// <summary>
    /// Displays a world-space dimension label on a chosen face of a blockout object.
    /// Front/Back use -Z/+Z local (the side facing the default Unity camera is Front).
    /// </summary>
    [ExecuteAlways]
    public class HeightFaceLabel : MonoBehaviour
    {
        public enum LabelDimension
        {
            Height,
            Width
        }

        public enum LabelFace
        {
            Front,
            Back,
            Right,
            Left,
            Top,
            Bottom
        }

        const string LabelChildName = "DimensionLabel";

        [SerializeField]
        LabelDimension _dimension = LabelDimension.Height;

        [SerializeField]
        LabelFace _face = LabelFace.Front;

        [SerializeField]
        Color _fontColor = Color.black;

        [SerializeField]
        Color _meshColor = Color.white;

        [SerializeField]
        [Min(0)]
        int _decimalPlaces = 1;

        [SerializeField]
        float _fontSize = 4f;

        [SerializeField]
        float _surfaceOffset = 0.01f;

        TextMeshPro _label;
        Renderer _renderer;
        MeshRenderer _meshRenderer;
        Collider _collider;
        MaterialPropertyBlock _propertyBlock;
        int _meshColorPropertyId = -1;
        bool _meshColorSupported;

        void Awake()
        {
            CacheComponents();
            EnsureLabel();
            RefreshLabel();
        }

        void Reset()
        {
            CacheComponents();
            EnsureLabel();

            if (MeshColorPropertyBlock.TryGetColor(_meshRenderer, out Color color))
                _meshColor = color;

            RefreshLabel();
        }

        void OnValidate()
        {
            CacheComponents();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall -= DelayedRefresh;
                EditorApplication.delayCall += DelayedRefresh;
                return;
            }
#endif
            EnsureLabel();
            RefreshLabel();
        }

#if UNITY_EDITOR
        void DelayedRefresh()
        {
            if (this == null || Undo.isProcessing)
                return;

            EnsureLabel();
            RefreshLabel();
        }
#endif

        void CacheComponents()
        {
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();

            if (_renderer == null)
                _renderer = GetComponentInChildren<Renderer>();

            if (_collider == null)
                _collider = GetComponent<Collider>();

            if (_collider == null)
                _collider = GetComponentInChildren<Collider>();

            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();

            if (_meshRenderer == null)
                _meshRenderer = GetComponentInChildren<MeshRenderer>();

            _meshColorSupported = MeshColorPropertyBlock.TryGetColorPropertyId(_meshRenderer, out _meshColorPropertyId);
        }

        void EnsureLabel()
        {
            if (_label != null)
                return;

#if UNITY_EDITOR
            if (Undo.isProcessing)
                return;
#endif

            Transform existing = transform.Find(LabelChildName);
            if (existing != null)
            {
                _label = existing.GetComponent<TextMeshPro>();
                if (_label != null)
                    return;
            }

            Transform legacyLabel = transform.Find("HeightLabel");
            if (legacyLabel != null)
            {
                _label = legacyLabel.GetComponent<TextMeshPro>();
                if (_label != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        Undo.RecordObject(legacyLabel.gameObject, "Rename Dimension Label");
#endif
                    legacyLabel.name = LabelChildName;
                    return;
                }
            }

            CreateLabelObject();
        }

        void CreateLabelObject()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                GameObject labelObject = new GameObject(LabelChildName);
                Undo.RegisterCreatedObjectUndo(labelObject, "Create Dimension Label");
                Undo.SetTransformParent(labelObject.transform, transform, "Create Dimension Label");
                _label = Undo.AddComponent<TextMeshPro>(labelObject);
                ConfigureLabel(_label);
                return;
            }
#endif

            GameObject runtimeLabelObject = new GameObject(LabelChildName);
            runtimeLabelObject.transform.SetParent(transform, false);
            _label = runtimeLabelObject.AddComponent<TextMeshPro>();
            ConfigureLabel(_label);
        }

        void ConfigureLabel(TextMeshPro label)
        {
            label.alignment = TextAlignmentOptions.Center;
            label.horizontalAlignment = HorizontalAlignmentOptions.Center;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.overflowMode = TextOverflowModes.Overflow;
            label.fontWeight = FontWeight.Bold;
        }

        void RefreshLabel()
        {
            if (_label == null)
                return;

            Bounds localBounds = GetLocalBounds();

            _label.text = GetDimensionValue(localBounds).ToString($"F{_decimalPlaces}");
            _label.fontSize = _fontSize;
            _label.color = _fontColor;

            ApplyMeshColor();

            Vector3 localAxis = GetLocalAxis(_face);
            Vector3 faceCenter = localBounds.center + Vector3.Scale(localBounds.extents, localAxis);

            _label.transform.localPosition = faceCenter + localAxis * _surfaceOffset;
            _label.transform.localRotation = GetLabelRotation(localAxis);
        }

        void ApplyMeshColor()
        {
            if (!_meshColorSupported || _meshRenderer == null)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();
            MeshColorPropertyBlock.Apply(_meshRenderer, _meshColor, _meshColorPropertyId, _propertyBlock);
        }

        float GetDimensionValue(Bounds localBounds)
        {
            return _dimension == LabelDimension.Width
                ? localBounds.size.x
                : localBounds.size.y;
        }

        static Vector3 GetLocalAxis(LabelFace face)
        {
            return face switch
            {
                LabelFace.Front => Vector3.back,
                LabelFace.Back => Vector3.forward,
                LabelFace.Right => Vector3.right,
                LabelFace.Left => Vector3.left,
                LabelFace.Top => Vector3.up,
                LabelFace.Bottom => Vector3.down,
                _ => Vector3.back
            };
        }

        static Quaternion GetLabelRotation(Vector3 outwardNormal)
        {
            Vector3 up = Mathf.Abs(outwardNormal.y) > 0.9f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(-outwardNormal, up);
        }

        Bounds GetLocalBounds()
        {
            if (_renderer != null)
                return GetBoundsFromLocalCorners(_renderer.transform, _renderer.localBounds.center, _renderer.localBounds.extents);

            if (_collider is BoxCollider boxCollider)
                return GetBoundsFromLocalCorners(boxCollider.transform, boxCollider.center, boxCollider.size * 0.5f);

            if (_collider != null)
            {
                Bounds worldBounds = _collider.bounds;
                Vector3 center = transform.InverseTransformPoint(worldBounds.center);
                Vector3 size = transform.InverseTransformVector(worldBounds.size);
                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                return new Bounds(center, size);
            }

            return new Bounds(Vector3.zero, transform.localScale);
        }

        Bounds GetBoundsFromLocalCorners(Transform sourceTransform, Vector3 localCenter, Vector3 localExtents)
        {
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = localCenter + Vector3.Scale(
                    localExtents,
                    new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));

                Vector3 localCorner = transform.InverseTransformPoint(sourceTransform.TransformPoint(corner));
                min = Vector3.Min(min, localCorner);
                max = Vector3.Max(max, localCorner);
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }
    }
}
