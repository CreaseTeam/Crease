using Crease.Folding.PaperGraph;
using Crease.UI;
using UnityEngine;

namespace Crease.Flying.Environment.Checkpoints
{
    public class Checkpoint : MonoBehaviour
    {
        private MeshRenderer _meshRenderer;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        public void TriggerCheckpoint()
        {
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
    }
}
