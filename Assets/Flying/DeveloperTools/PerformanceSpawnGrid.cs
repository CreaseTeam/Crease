using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crease.Flying.DeveloperTools
{
    /// <summary>
    /// Spawns a roughly square XZ grid of objects for performance testing.
    /// Regenerates in-editor when settings change and cleans up when disabled or destroyed.
    /// </summary>
    [ExecuteAlways]
    public class PerformanceSpawnGrid : MonoBehaviour
    {
        const string SpawnRootName = "SpawnedObjects";

        [Serializable]
        public class SpawnEntry
        {
            public GameObject Prefab;
            [Min(0)]
            public int Quantity = 1;
        }

        [SerializeField]
        List<SpawnEntry> _entries = new List<SpawnEntry>();

        [SerializeField]
        [Min(0f)]
        float _spacing = 2f;

        Transform _spawnRoot;
        bool _isRebuilding;
        bool _rebuildPending;

        public float Spacing
        {
            get => _spacing;
            set
            {
                _spacing = Mathf.Max(0f, value);
                ScheduleRebuild();
            }
        }

        void OnEnable()
        {
            ScheduleRebuild();
        }

        void OnDisable()
        {
            _rebuildPending = false;
#if UNITY_EDITOR
            EditorApplication.delayCall -= FlushRebuild;
#endif
            ClearSpawned();
        }

        void OnValidate()
        {
            // Instantiate is not allowed during OnValidate — always defer.
            ScheduleRebuild();
        }

        void Update()
        {
            if (_rebuildPending)
                FlushRebuild();
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

            _rebuildPending = false;

            if (this == null || !isActiveAndEnabled)
                return;

#if UNITY_EDITOR
            if (Undo.isProcessing)
                return;
#endif

            Rebuild();
        }

        [ContextMenu("Rebuild Grid")]
        public void Rebuild()
        {
            if (_isRebuilding)
                return;

            _isRebuilding = true;
            try
            {
                ClearSpawned();

                int totalCount = GetTotalCount();
                if (totalCount == 0)
                    return;

                EnsureSpawnRoot();

                int columns = Mathf.CeilToInt(Mathf.Sqrt(totalCount));
                int rows = Mathf.CeilToInt(totalCount / (float)columns);
                float halfWidth = (columns - 1) * _spacing * 0.5f;
                float halfDepth = (rows - 1) * _spacing * 0.5f;

                int index = 0;
                foreach (SpawnEntry entry in _entries)
                {
                    if (entry == null || entry.Prefab == null || entry.Quantity <= 0)
                        continue;

                    for (int i = 0; i < entry.Quantity; i++)
                    {
                        int column = index % columns;
                        int row = index / columns;
                        Vector3 localPosition = new Vector3(
                            column * _spacing - halfWidth,
                            0f,
                            row * _spacing - halfDepth);

                        SpawnInstance(entry.Prefab, localPosition, index);
                        index++;
                    }
                }
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        [ContextMenu("Clear Grid")]
        public void ClearSpawned()
        {
            if (_spawnRoot == null)
            {
                Transform existing = transform.Find(SpawnRootName);
                if (existing != null)
                    _spawnRoot = existing;
            }

            if (_spawnRoot == null)
                return;

            for (int i = _spawnRoot.childCount - 1; i >= 0; i--)
                DestroySpawnedObject(_spawnRoot.GetChild(i).gameObject);

            DestroySpawnedObject(_spawnRoot.gameObject);
            _spawnRoot = null;
        }

        int GetTotalCount()
        {
            int total = 0;
            foreach (SpawnEntry entry in _entries)
            {
                if (entry == null || entry.Prefab == null || entry.Quantity <= 0)
                    continue;

                total += entry.Quantity;
            }

            return total;
        }

        void EnsureSpawnRoot()
        {
            Transform existing = transform.Find(SpawnRootName);
            if (existing != null)
            {
                _spawnRoot = existing;
                return;
            }

            GameObject rootObject = new GameObject(SpawnRootName);
            rootObject.hideFlags = HideFlags.DontSave;
            rootObject.transform.SetParent(transform, false);
            _spawnRoot = rootObject.transform;
        }

        void SpawnInstance(GameObject prefab, Vector3 localPosition, int index)
        {
            GameObject instance = Instantiate(prefab, _spawnRoot);
            instance.name = $"{prefab.name}_{index}";
            instance.hideFlags = HideFlags.DontSave;
            instance.transform.localPosition = localPosition;
        }

        static void DestroySpawnedObject(GameObject target)
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
