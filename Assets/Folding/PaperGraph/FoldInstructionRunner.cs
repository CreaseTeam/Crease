using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Loads a FoldInstruction asset and runs its steps sequentially.
/// Press spacebar to execute the current fold step and advance to the next.
/// </summary>
public class FoldInstructionRunner : MonoBehaviour
{
    [Tooltip("The instruction set to run. Assign in the Inspector or call LoadInstruction() at runtime.")]
    public FoldInstruction instruction;

    [Header("References")]
    [Tooltip("The PaperGraphController on this GameObject.")]
    public PaperGraphController controller;

    [Tooltip("The drag handle whose position reflects the current step.")]
    public FoldDragHandle dragHandle;

    [Tooltip("Camera to reposition during instruction playback. Orbits the PaperGraph transform.")]
    public Camera instructionCamera;

    [Header("Camera Settings")]
    [Tooltip("How fast the camera lerps to its target position/rotation.")]
    public float cameraLerpSpeed = 5f;

    [Tooltip("Current orbit rotation (euler angles). Modify in editor to adjust camera live.")]
    public Vector3 currentOrbitRotation;

    [Tooltip("Current orbit distance. Modify in editor to adjust camera live.")]
    public float currentOrbitDistance = 3f;

    private int currentStepIndex = -1;
    private Keyboard keyboard;

    // Camera lerp state
    private bool isCameraLerping = false;
    private Vector3 cameraTargetPosition;
    private Quaternion cameraTargetRotation;

    private void OnValidate() {
        RecalculateCameraTarget();
    }

    private void Start() {
        keyboard = Keyboard.current;

        if (controller == null)
            controller = GetComponent<PaperGraphController>();

        if (instruction != null)
            LoadInstruction(instruction);
    }

    private void Update() {
        if (keyboard == null) return;

        if (keyboard.spaceKey.wasPressedThisFrame) {
            ExecuteCurrentStep();
        }
    }

    private void LateUpdate() {
        if (!isCameraLerping || instructionCamera == null) return;

        Transform camTransform = instructionCamera.transform;
        camTransform.position = Vector3.Lerp(camTransform.position, cameraTargetPosition, cameraLerpSpeed * Time.deltaTime);
        camTransform.rotation = Quaternion.Slerp(camTransform.rotation, cameraTargetRotation, cameraLerpSpeed * Time.deltaTime);

        // Stop lerping when close enough
        if (Vector3.Distance(camTransform.position, cameraTargetPosition) < 0.001f &&
            Quaternion.Angle(camTransform.rotation, cameraTargetRotation) < 0.1f) {
            camTransform.position = cameraTargetPosition;
            camTransform.rotation = cameraTargetRotation;
            isCameraLerping = false;
        }
    }

    /// <summary>
    /// Resets the paper and loads the first step of the given instruction set.
    /// </summary>
    public void LoadInstruction(FoldInstruction newInstruction) {
        instruction = newInstruction;

        if (instruction == null || instruction.steps.Count == 0) {
            Debug.LogWarning("FoldInstructionRunner: Instruction is null or has no steps.");
            currentStepIndex = -1;
            return;
        }

        // Reset the paper to a fresh sheet
        controller.ResetSheet();

        // Load the first step
        currentStepIndex = 0;
        ApplyStepToController(instruction.steps[0]);

        Debug.Log($"FoldInstructionRunner: Loaded instruction with {instruction.steps.Count} step(s). Press Space to execute step 1.");
    }

    /// <summary>
    /// Executes the current fold step, applies it to the mesh, then loads the next step's values.
    /// </summary>
    public void ExecuteCurrentStep() {
        if (instruction == null || instruction.steps.Count == 0) {
            Debug.LogWarning("FoldInstructionRunner: No instruction loaded.");
            return;
        }

        if (currentStepIndex < 0 || currentStepIndex >= instruction.steps.Count) {
            Debug.Log("FoldInstructionRunner: All steps completed.");
            return;
        }

        // Execute the fold with the values currently loaded in the controller
        controller.ExecuteFoldAction();
        Debug.Log($"FoldInstructionRunner: Executed step {currentStepIndex + 1}/{instruction.steps.Count}.");

        // Advance to the next step
        currentStepIndex++;

        if (currentStepIndex < instruction.steps.Count) {
            ApplyStepToController(instruction.steps[currentStepIndex]);
            Debug.Log($"FoldInstructionRunner: Loaded step {currentStepIndex + 1}/{instruction.steps.Count}. Press Space to execute.");
        } else {
            Debug.Log("FoldInstructionRunner: All steps completed!");
        }
    }

    /// <summary>
    /// Writes a FoldStep's values into the PaperGraphController and positions the drag handle.
    /// </summary>
    private void ApplyStepToController(FoldStep step) {
        controller.dragHandlePosition = step.dragHandlePosition;
        controller.dragPlaneNormal = step.dragPlaneNormal;
        controller.foldDegrees = step.foldDegrees;
        controller.foldOffset = step.foldOffset;

        // Apply tag (tag name to stamp on affected vertices)
        controller.foldTagName = string.IsNullOrEmpty(step.applyTag) ? "" : step.applyTag;

        // Filter tag — resolve index from the tag name, or clear to (None)
        controller.selectedFilterTagIndex = 0; // default to no filter
        if (!string.IsNullOrEmpty(step.filterTag)) {
            PaperGraph graph = controller.GetComponent<PaperGraph>();
            if (graph != null && graph.tags != null && graph.tags.Count > 0) {
                var tagKeys = new System.Collections.Generic.List<string>(graph.tags.Keys);
                int idx = tagKeys.IndexOf(step.filterTag);
                if (idx >= 0) {
                    controller.selectedFilterTagIndex = idx + 1; // +1 because 0 is "(None)"
                } else {
                    Debug.LogWarning($"FoldInstructionRunner: Filter tag \"{step.filterTag}\" not found on graph. Ignoring filter.");
                }
            }
        }

        // Position the drag handle if assigned
        if (dragHandle != null) {
            dragHandle.transform.position = step.dragHandlePosition;
        }

        // Set up camera orbit lerp if this step specifies it
        if (step.moveCamera && instructionCamera != null) {
            currentOrbitRotation = step.cameraOrbitRotation;
            currentOrbitDistance = step.cameraDistance;
            RecalculateCameraTarget();
            isCameraLerping = true;
        }

        // Refresh the preview
        controller.UpdatePreview();
    }

    /// <summary>
    /// Recomputes the camera target position and rotation from the exposed orbit values.
    /// Called from OnValidate so tweaking values in the Inspector updates the camera live.
    /// </summary>
    private void RecalculateCameraTarget() {
        if (instructionCamera == null || controller == null) return;

        Vector3 pivot = controller.transform.position;
        Quaternion orbitRotation = Quaternion.Euler(currentOrbitRotation);
        cameraTargetPosition = pivot + orbitRotation * (Vector3.back * currentOrbitDistance);
        cameraTargetRotation = Quaternion.LookRotation(pivot - cameraTargetPosition);
        isCameraLerping = true;
    }
}
