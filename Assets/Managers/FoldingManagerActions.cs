using UnityEngine;
using Crease.Folding.PaperGraph;

namespace Crease.Managers
{
    /// <summary>
    /// Simple wrapper with instance methods that delegate to FoldingManager.Instance.
    /// Attach to any GameObject and drag these functions onto Button OnClick events, etc.
    /// </summary>
    public class FoldingManagerActions : MonoBehaviour
    {
        public void SaveMesh()
        {
            if (FoldingManager.Instance == null) { Debug.LogError("FoldingManagerActions: No FoldingManager instance."); return; }
            FoldingManager.Instance.SaveMesh();
        }

        public void EnterFoldingMode()
        {
            if (FoldingManager.Instance == null) { Debug.LogError("FoldingManagerActions: No FoldingManager instance."); return; }
            FoldingManager.Instance.EnterFoldingMode();
        }

        public void EnterFlyingMode()
        {
            if (FoldingManager.Instance == null) { Debug.LogError("FoldingManagerActions: No FoldingManager instance."); return; }
            FoldingManager.Instance.EnterFlyingMode();
        }

        public void EnterFlyingModeNoMesh()
        {
            if (FoldingManager.Instance == null) { Debug.LogError("FoldingManagerActions: No FoldingManager instance."); return; }
            FoldingManager.Instance.EnterFlyingModeNoMesh();
        }
    }
}
