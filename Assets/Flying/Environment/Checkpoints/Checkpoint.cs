using Crease.Events;
using Crease.Folding.Paper;
using Crease.Managers;
using Crease.UI;
using UnityEngine;

namespace Crease.Flying.Environment.Checkpoints
{
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Respawn location and rotation relative to this checkpoint. Location ignores the checkpoint's scale.")]
        private RespawnPose _relativeRespawn = new RespawnPose(Vector3.zero, Quaternion.identity);

        private MeshRenderer _meshRenderer;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void OnValidate()
        {
            if (_relativeRespawn.Rotation == default)
                _relativeRespawn.Rotation = Quaternion.identity;
        }

        public void TriggerCheckpoint()
        {
            if (FlyingManager.Instance != null)
                FlyingManager.Instance.SetRespawn(GetWorldRespawn());
            else
                Debug.LogWarning("Checkpoint: no FlyingManager in scene.");

            GameEvents.OnCheckpointReached?.Invoke();

            if (HUDCanvas.Instance != null)
                HUDCanvas.Instance.SetRefoldAvailable(true);

            if (FoldingManager.Instance != null)
                FoldingManager.Instance.EnterFoldingMode();
            else
                Debug.LogWarning("Checkpoint: no FoldingManager in scene.");
            Debug.Log("Checkpoint triggered!");

            if (_meshRenderer != null)
                _meshRenderer.enabled = false;
        }

        private RespawnPose GetWorldRespawn()
        {
            return new RespawnPose(
                transform.position + transform.rotation * _relativeRespawn.Location,
                transform.rotation * _relativeRespawn.Rotation);
        }
    }
}
