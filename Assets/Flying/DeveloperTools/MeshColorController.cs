using UnityEngine;

namespace Crease.Flying.DeveloperTools
{
    /// <summary>
    /// Controls a MeshRenderer color via MaterialPropertyBlock when the material supports _BaseColor or _Color.
    /// </summary>
    [ExecuteAlways]
    public class MeshColorController : MonoBehaviour
    {
        [SerializeField]
        Color _meshColor = Color.white;

        MeshRenderer _meshRenderer;
        MaterialPropertyBlock _propertyBlock;
        int _colorPropertyId = -1;

        public Color MeshColor
        {
            get => _meshColor;
            set
            {
                _meshColor = value;
                Apply();
            }
        }

        public bool IsSupported { get; private set; }

        void Awake()
        {
            Refresh();
        }

        void Reset()
        {
            CacheComponents();

            if (MeshColorPropertyBlock.TryGetColor(_meshRenderer, out Color color))
                _meshColor = color;

            Apply();
        }

        void OnValidate()
        {
            Refresh();
        }

        void CacheComponents()
        {
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();

            if (_meshRenderer == null)
                _meshRenderer = GetComponentInChildren<MeshRenderer>();

            IsSupported = MeshColorPropertyBlock.TryGetColorPropertyId(_meshRenderer, out _colorPropertyId);
        }

        void Refresh()
        {
            CacheComponents();
            Apply();
        }

        void Apply()
        {
            if (!IsSupported)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();
            MeshColorPropertyBlock.Apply(_meshRenderer, _meshColor, _colorPropertyId, _propertyBlock);
        }
    }
}
