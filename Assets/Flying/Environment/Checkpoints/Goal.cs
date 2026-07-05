using UnityEngine;
using Crease.Folding.PaperGraph;

namespace Crease.Flying.Environment.Checkpoints
{
    /// <summary>
    /// End-of-level goal (the orange pillar). When the player enters its trigger
    /// the game returns to folding mode, reveals this level's letter on the front
    /// of the paper, shows the level-end UI, and resets the paper. 
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Goal : MonoBehaviour
    {
        [Tooltip("Only colliders with this tag complete the level.")]
        [SerializeField] private string targetTag = "Player";

        public Material LetterFront;

        private bool _triggered;

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || !other.CompareTag(targetTag))
                return;

            _triggered = true;

            if (FoldingManager.Instance != null)
                FoldingManager.Instance.TriggerLevelEnd(LetterFront);
            else
                Debug.LogWarning("Goal: no FoldingManager in scene; cannot trigger level end.");
        }
    }
}
