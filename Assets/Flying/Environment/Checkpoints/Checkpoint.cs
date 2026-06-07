using UnityEngine;
using Crease.Folding.PaperGraph;

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
            FoldingManager.Instance.EnterFoldingMode();
            Debug.Log("Checkpoint triggered!");

            if (_meshRenderer != null)
                _meshRenderer.enabled = false;
        }
    }
}
