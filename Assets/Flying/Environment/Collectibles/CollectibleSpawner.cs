using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crease.Flying.Environment.Collectibles
{
    /// <summary>
    /// Spawns a collectible prefab and respawns it after collection using a delay and local offset.
    /// </summary>
    [ExecuteAlways]
    public class CollectibleSpawner : MonoBehaviour
    {
        const string PreviewChildName = "CollectiblePreview";

        [Header("Spawn Settings")]
        [SerializeField]
        Collectible _collectiblePrefab;

        [SerializeField]
        [Tooltip("Local offset from this transform where collectibles are spawned.")]
        Vector3 _spawnOffset;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds to wait after collection before spawning again.")]
        float _respawnDelay = 3f;

        [SerializeField]
        bool _spawnOnStart = true;

        Collectible _activeCollectible;
        Coroutine _respawnRoutine;

        void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            if (!Application.isPlaying)
                ScheduleEditorPreviewRefresh();
#endif
        }

        void Start()
        {
            ClearEditorPreview();

            if (Application.isPlaying && _spawnOnStart)
                SpawnCollectible();
        }

        void OnDisable()
        {
            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.delayCall -= DelayedRefreshEditorPreview;
#endif
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                ScheduleEditorPreviewRefresh();
#endif
        }

#if UNITY_EDITOR
        bool _skipEditorPreviewRefresh;

        void ScheduleEditorPreviewRefresh()
        {
            EditorApplication.delayCall -= DelayedRefreshEditorPreview;
            EditorApplication.delayCall += DelayedRefreshEditorPreview;
        }

        void OnUndoRedoPerformed()
        {
            _skipEditorPreviewRefresh = true;
            EditorApplication.delayCall -= DelayedRefreshEditorPreview;
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                ClearEditorPreview();
            else if (state == PlayModeStateChange.EnteredEditMode)
                ScheduleEditorPreviewRefresh();
        }

        void DelayedRefreshEditorPreview()
        {
            if (this == null || Undo.isProcessing)
                return;

            if (_skipEditorPreviewRefresh)
            {
                _skipEditorPreviewRefresh = false;
                return;
            }

            RefreshEditorPreview();
        }

        void RefreshEditorPreview()
        {
            if (Application.isPlaying || Undo.isProcessing)
                return;

            if (!isActiveAndEnabled || !_spawnOnStart || _collectiblePrefab == null)
            {
                ClearEditorPreview();
                return;
            }

            if (TryGetEditorPreview(out Collectible existingPreview) && IsEditorPreviewCurrent(existingPreview))
            {
                existingPreview.gameObject.hideFlags = HideFlags.DontSave;
                return;
            }

            ClearEditorPreview();

            Collectible preview = (Collectible)PrefabUtility.InstantiatePrefab(_collectiblePrefab, transform);
            if (preview == null)
                return;

            preview.gameObject.name = PreviewChildName;
            preview.gameObject.hideFlags = HideFlags.DontSave;
            preview.transform.localPosition = _spawnOffset;
            preview.transform.localRotation = Quaternion.identity;
        }

        bool TryGetEditorPreview(out Collectible preview)
        {
            Transform previewTransform = transform.Find(PreviewChildName);
            preview = previewTransform != null ? previewTransform.GetComponent<Collectible>() : null;
            return preview != null;
        }

        bool IsEditorPreviewCurrent(Collectible preview)
        {
            Transform previewTransform = preview.transform;
            if (previewTransform.localPosition != _spawnOffset
                || previewTransform.localRotation != Quaternion.identity)
                return false;

            return PrefabUtility.GetCorrespondingObjectFromOriginalSource(preview) == _collectiblePrefab;
        }
#endif

        void ClearEditorPreview()
        {
            Transform preview = transform.Find(PreviewChildName);
            if (preview == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(preview.gameObject);
                return;
            }
#endif

            Destroy(preview.gameObject);
        }

        public void SpawnCollectible()
        {
            if (!Application.isPlaying)
                return;

            if (_collectiblePrefab == null)
            {
                Debug.LogWarning($"{nameof(CollectibleSpawner)} on {name} has no collectible prefab assigned.", this);
                return;
            }

            if (_activeCollectible != null)
                return;

            Vector3 spawnPosition = transform.TransformPoint(_spawnOffset);
            _activeCollectible = Instantiate(_collectiblePrefab, spawnPosition, transform.rotation);
            _activeCollectible.OnCollected.AddListener(OnCollectibleCollected);
        }

        void OnCollectibleCollected()
        {
            _activeCollectible = null;

            if (!isActiveAndEnabled || _respawnDelay <= 0f)
            {
                SpawnCollectible();
                return;
            }

            _respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(_respawnDelay);
            _respawnRoutine = null;
            SpawnCollectible();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.TransformPoint(_spawnOffset), 0.25f);
            Gizmos.DrawLine(transform.position, transform.TransformPoint(_spawnOffset));
        }
    }
}
