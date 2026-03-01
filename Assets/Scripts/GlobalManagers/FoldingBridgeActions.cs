using UnityEngine;

/// <summary>
/// Simple wrapper with instance methods that delegate to FoldingBridge.Instance.
/// Attach to any GameObject and drag these functions onto Button OnClick events, etc.
/// </summary>
public class FoldingBridgeActions : MonoBehaviour
{
    public void SaveMesh() {
        if (FoldingBridge.Instance == null) { Debug.LogError("FoldingBridgeActions: No FoldingBridge instance."); return; }
        FoldingBridge.Instance.SaveMesh();
    }

    public void TransitionToFlightScene() {
        if (FoldingBridge.Instance == null) { Debug.LogError("FoldingBridgeActions: No FoldingBridge instance."); return; }
        FoldingBridge.Instance.TransitionToFlightScene();
    }

    public void TransitionToFlightSceneNoMesh() {
        if (FoldingBridge.Instance == null) { Debug.LogError("FoldingBridgeActions: No FoldingBridge instance."); return; }
        FoldingBridge.Instance.TransitionToFlightSceneNoMesh();
    }

    public void ReturnToFoldingScene() {
        if (FoldingBridge.Instance == null) { Debug.LogError("FoldingBridgeActions: No FoldingBridge instance."); return; }
        FoldingBridge.Instance.ReturnToFoldingScene();
    }
}
