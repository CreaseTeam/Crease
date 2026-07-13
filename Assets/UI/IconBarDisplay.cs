using UnityEngine;

namespace Crease.UI
{
    /// <summary>
    /// Displays a numeric value as a row of instantiated icon prefabs.
    /// Attach to a container with a HorizontalLayoutGroup (or assign a child container).
    /// </summary>
    public class IconBarDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private RectTransform _container;
        [SerializeField]
        private GameObject _iconPrefab;

        [Header("Settings")]
        [Tooltip("Optional cap on the displayed value. Leave at 0 for no limit.")]
        [SerializeField]
        private int _maxValue;
        [SerializeField]
        private int _initialValue;

        public int Value { get; private set; }
        public int MaxValue => _maxValue;

        private void Awake()
        {
            if (_container == null)
                _container = transform as RectTransform;

            SetValue(_initialValue);
        }

        public void SetValue(int value)
        {
            int clamped = _maxValue > 0 ? Mathf.Clamp(value, 0, _maxValue) : Mathf.Max(0, value);

            if (clamped == Value && _container.childCount == clamped)
                return;

            Value = clamped;
            SyncChildCount();
        }

        public void SetMaxValue(int maxValue)
        {
            _maxValue = Mathf.Max(0, maxValue);

            if (_maxValue > 0 && Value > _maxValue)
                SetValue(_maxValue);
        }

        public void Add(int amount) => SetValue(Value + amount);

        public void Subtract(int amount) => SetValue(Value - amount);

        public void Increment() => Add(1);

        public void Decrement() => Subtract(1);

        public void Clear() => SetValue(0);

        private void SyncChildCount()
        {
            if (_container == null)
                return;

            if (_iconPrefab != null)
            {
                while (_container.childCount < Value)
                    Instantiate(_iconPrefab, _container);
            }
            else if (_container.childCount < Value)
            {
                Debug.LogWarning("IconBarDisplay: Cannot spawn icons because no icon prefab is assigned.", this);
            }

            // Use a counted loop — Destroy is deferred during play mode, so `while (childCount > Value)` never terminates.
            for (int i = _container.childCount - 1; i >= Value; i--)
                Destroy(_container.GetChild(i).gameObject);
        }
    }
}
