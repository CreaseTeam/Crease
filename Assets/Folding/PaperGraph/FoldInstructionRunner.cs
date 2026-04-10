using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads a FoldInstruction asset and runs its steps sequentially.
/// Uses InputManager for Folding actions (ExecuteFold, Recenter).
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

    [Header("Fold Axis Guide Line")]
    [Tooltip("LineRenderer used to display the ideal fold axis. Created automatically if not assigned.")]
    public LineRenderer foldAxisGuide;

    [Tooltip("Material for the guide line. Should use a dashed/dotted texture with Tiling.")]
    public Material guideLineMaterial;

    [Tooltip("Width of the guide line.")]
    public float guideLineWidth = 0.005f;

    [Tooltip("Color of the guide line.")]
    public Color guideLineColor = new Color(1f, 1f, 1f, 0.7f);

    [Tooltip("How far above the topmost paper layer the guide line floats.")]
    public float guideLineHeightOffset = 0.002f;

    [Header("Accuracy")]
    [Tooltip("Controls how steeply accuracy falls off with distance. Higher = sharper dropoff.")]
    public float accuracyFalloff = 5f;

    [Header("Paper Rotation Settings")]
    [Tooltip("How fast the paper lerps to its target rotation.")]
    public float paperLerpSpeed = 5f;

    [Tooltip("Current paper rotation (euler angles). Modify in editor to adjust paper live.")]
    public Vector3 currentPaperRotation;

    [Header("Unfold Settings")]
    [Tooltip("How fast the paper unfolds (degrees per second).")]
    public float unfoldAnimationSpeed = 180f;

    [Header("AutoFold Settings")]
    [Tooltip("How fast the paper folds automatically (degrees per second).")]
    public float foldAnimationSpeed = 180f;

    [Tooltip("If true, automatically snaps the drag handle to the outside of the paper surface when a step begins.")]
    public bool autoSnapDragHandle = false;

    private int currentStepIndex = -1;

    // Accuracy tracking
    private float totalAccuracy = 0f;
    private int foldCount = 0;

    // Paper rotation lerp state
    private bool isPaperLerping = false;
    private Quaternion targetPaperRotation;

    private bool isUnfolding = false;
    private bool isAutoFolding = false;

    private void OnValidate() {
        RecalculatePaperTarget();
    }

    private void Start() {
        if (controller == null)
            controller = GetComponent<PaperGraphController>();

        EnsureGuideLineRenderer();

        if (instruction != null)
            LoadInstruction(instruction);
    }

    /// <summary>
    /// Creates or configures the LineRenderer for the fold axis guide.
    /// </summary>
    private void EnsureGuideLineRenderer() {
        if (foldAxisGuide == null) {
            GameObject guideObj = new GameObject("FoldAxisGuide");
            guideObj.transform.SetParent(controller != null ? controller.transform : transform, false);
            foldAxisGuide = guideObj.AddComponent<LineRenderer>();
        }

        foldAxisGuide.useWorldSpace = false; // positions are local to the paper
        foldAxisGuide.positionCount = 2;
        foldAxisGuide.startWidth = guideLineWidth;
        foldAxisGuide.endWidth = guideLineWidth;
        foldAxisGuide.startColor = guideLineColor;
        foldAxisGuide.endColor = guideLineColor;
        foldAxisGuide.textureMode = LineTextureMode.Tile;

        if (guideLineMaterial != null)
            foldAxisGuide.material = guideLineMaterial;

        foldAxisGuide.enabled = false;
    }

    private void Update() {
        if (InputManager.Instance == null) return;
        if (isUnfolding || isAutoFolding) return;

        if (InputManager.Instance.ExecuteFoldTriggered)
            ExecuteCurrentStep();

        if (InputManager.Instance.RecenterTriggered)
            Recenter();
    }

    private void LateUpdate() {
        if (!isPaperLerping || controller == null) return;

        Transform paperTransform = controller.transform;
        paperTransform.rotation = Quaternion.Slerp(paperTransform.rotation, targetPaperRotation, paperLerpSpeed * Time.deltaTime);

        // Stop lerping when close enough
        if (Quaternion.Angle(paperTransform.rotation, targetPaperRotation) < 0.1f) {
            paperTransform.rotation = targetPaperRotation;
            isPaperLerping = false;
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

        // Reset accuracy tracking
        totalAccuracy = 0f;
        foldCount = 0;
        HUDCanvas.Instance.ResetAccuracyDisplay();

        // Reset the paper to a fresh sheet
        controller.ResetSheet();

        // Re-enable the drag handle
        if (dragHandle != null)
            dragHandle.gameObject.SetActive(true);

        // Load the first step
        currentStepIndex = 0;
        ApplyStepToController(instruction.steps[0]);

        // Debug.Log($"FoldInstructionRunner: Loaded instruction with {instruction.steps.Count} step(s). Press ExecuteFold to execute step 1.");
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

        // --- Calculate accuracy before executing the fold ---
        FoldStep currentStep = instruction.steps[currentStepIndex];
        float foldAccuracy = CalculateFoldAccuracy(currentStep);

        // Update cumulative tracking
        foldCount++;
        totalAccuracy += foldAccuracy;
        float overallAccuracy = totalAccuracy / foldCount;

        // Update HUD
        HUDCanvas.Instance.UpdateFoldAccuracy(foldAccuracy);
        HUDCanvas.Instance.UpdateOverallAccuracy(overallAccuracy);

        Debug.Log($"FoldInstructionRunner: Fold accuracy = {foldAccuracy:F1}%, overall = {overallAccuracy:F1}%");

        // Execute the fold with the values currently loaded in the controller
        controller.ExecuteFoldAction();
        AudioManager.Instance.Play("fold");
        Debug.Log($"FoldInstructionRunner: Executed step {currentStepIndex + 1}/{instruction.steps.Count}.");

        // Advance to the next step
        currentStepIndex++;

        if (currentStepIndex < instruction.steps.Count) {
            ApplyStepToController(instruction.steps[currentStepIndex]);
            Debug.Log($"FoldInstructionRunner: Loaded step {currentStepIndex + 1}/{instruction.steps.Count}. Press ExecuteFold to execute.");
        } else {
            // Clear the preview so no ghost fold lingers
            controller.ClearPreview();
            HideGuideLine();

            // Hide the drag handle — no more steps to drag
            if (dragHandle != null)
                dragHandle.gameObject.SetActive(false);

            Debug.Log("FoldInstructionRunner: All steps completed!");
        }
    }

    /// <summary>
    /// Compares the actual drag handle position (where the player dragged) against
    /// the ideal drag position defined in the step. Returns a 0–100 accuracy score.
    /// </summary>
    private float CalculateFoldAccuracy(FoldStep step) {
        // Get the actual drag position: the drag handle's current position in local space
        Vector3 actualDragPos;
        if (dragHandle != null && controller != null) {
            actualDragPos = controller.transform.InverseTransformPoint(dragHandle.transform.position);
        } else {
            // Fallback: use the controller's current drag handle position
            actualDragPos = controller != null ? controller.dragHandlePosition : step.dragHandlePosition;
        }

        float distance = Vector3.Distance(actualDragPos, step.idealDragPosition);
        float idealFoldDistance = Vector3.Distance(step.dragHandlePosition, step.idealDragPosition);
        float normalizedError = idealFoldDistance > 0.0001f ? distance / idealFoldDistance : 0f;
        float accuracy = Mathf.Exp(-accuracyFalloff * normalizedError) * 100f;
        return accuracy;
    }

    /// <summary>
    /// Writes a FoldStep's values into the PaperGraphController and positions the drag handle.
    /// </summary>
    private void ApplyStepToController(FoldStep step) {
        controller.dragHandlePosition = step.dragHandlePosition;
        controller.dragPlaneNormal = step.dragPlaneNormal;
        controller.foldDegrees = step.foldDegrees;
        controller.foldOffset = step.foldOffset;

        if (autoSnapDragHandle) {
            controller.SnapDragHandleToOutside();
        }

        // Apply tag (tag name to stamp on affected vertices)
        controller.foldTagName = string.IsNullOrEmpty(step.applyTag) ? "" : step.applyTag;

        // Filter tag — resolve index from the tag name, or clear to (None)
        controller.selectedFilterTagIndex = 0; // default to no filter
        if (!string.IsNullOrEmpty(step.filterTag)) {
            PaperGraph graph = controller.GetComponent<PaperGraph>();
            if (graph != null && graph.tags != null && graph.tags.Count > 0) {
                var tagKeys = new List<string>(graph.tags.Keys);
                int idx = tagKeys.IndexOf(step.filterTag);
                if (idx >= 0) {
                    controller.selectedFilterTagIndex = idx + 1; // +1 because 0 is "(None)"
                } else {
                    Debug.LogWarning($"FoldInstructionRunner: Filter tag \"{step.filterTag}\" not found on graph. Ignoring filter.");
                }
            }
        }

        // Position the drag handle if assigned (local → world)
        if (dragHandle != null) {
            dragHandle.transform.position = controller.transform.TransformPoint(step.dragHandlePosition);
        }

        // Set up paper rotation lerp if this step specifies it
        if (step.rotatePaper && controller != null) {
            currentPaperRotation = step.paperRotation;
            RecalculatePaperTarget();
            isPaperLerping = true;
        }

        // Clear the preview — no fold preview until the drag handle is moved
        controller.ClearPreview();

        // Show the ideal fold axis guide line
        UpdateGuideLine(step);
    }

    /// <summary>
    /// Computes the ideal fold axis from dragHandlePosition → idealDragPosition
    /// (same math as PaperGraphController.UpdateFoldFromDrag), clips it to
    /// the paper's edge geometry, and floats it above the topmost layer.
    /// </summary>
    private void UpdateGuideLine(FoldStep step) {
        if (foldAxisGuide == null) return;

        PaperGraph graph = controller != null ? controller.GetComponent<PaperGraph>() : null;

        Vector3 dragStart = step.dragHandlePosition;
        Vector3 dragEnd = step.idealDragPosition;
        Vector3 dragDelta = dragEnd - dragStart;

        // If ideal position is not set (same as start), hide the guide
        if (dragDelta.sqrMagnitude < 0.00001f) {
            foldAxisGuide.enabled = false;
            return;
        }

        Vector3 planeNormal = step.dragPlaneNormal.normalized;

        // Same math as PaperGraphController.UpdateFoldFromDrag
        Vector3 midpoint = (dragStart + dragEnd) * 0.5f;
        Vector3 dragDir = dragDelta.normalized;
        Vector3 foldAxisDir = Vector3.Cross(planeNormal, dragDir).normalized;

        if (foldAxisDir.sqrMagnitude < 0.0001f) {
            foldAxisGuide.enabled = false;
            return;
        }

        // --- Clip to paper edges and find local height ---
        float halfLength = controller != null ? controller.foldLineHalfLength : 1f;
        Vector3 lineP1 = midpoint + foldAxisDir * halfLength;
        Vector3 lineP2 = midpoint - foldAxisDir * halfLength;
        float localMaxHeight = Vector3.Dot(midpoint, planeNormal); // fallback

        if (graph != null && graph.edges.Count > 0) {
            Vector3 clippedP1, clippedP2;
            float clippedMaxHeight;
            if (ClipLineToPaperEdges(lineP1, lineP2, foldAxisDir, midpoint, planeNormal, graph, out clippedP1, out clippedP2, out clippedMaxHeight)) {
                lineP1 = clippedP1;
                lineP2 = clippedP2;
                localMaxHeight = clippedMaxHeight;
            } else {
                // Fold axis doesn't cross the paper — hide the guide
                foldAxisGuide.enabled = false;
                return;
            }
        }

        // --- Offset above the topmost layer at the fold axis ---
        {
            // Cap so the guide sits below the upcoming fold layer.
            // During flat folds the folded side offsets by foldOffset, so
            // keeping our offset smaller ensures the fold covers the line.
            float heightOffset = guideLineHeightOffset;
            if (Mathf.Abs(step.foldOffset) > 0.00001f) {
                heightOffset = Mathf.Min(heightOffset, Mathf.Abs(step.foldOffset) * 0.5f);
            }

            // Place the line at the local height (at the fold axis) + offset
            float currentHeight = Vector3.Dot(midpoint, planeNormal);
            float lift = (localMaxHeight - currentHeight) + heightOffset;
            Vector3 offset = planeNormal * lift;
            lineP1 += offset;
            lineP2 += offset;
        }

        foldAxisGuide.SetPosition(0, lineP1);
        foldAxisGuide.SetPosition(1, lineP2);
        foldAxisGuide.enabled = true;
    }

    /// <summary>
    /// Clips the fold axis line segment to only the region that overlaps the paper.
    /// Projects everything onto the 2D plane (perpendicular to planeNormal) and
    /// finds intersections between the fold axis and each paper edge.
    /// Returns the outermost pair of intersection points along the fold axis direction,
    /// plus the maximum height (along planeNormal) at the intersection points.
    /// </summary>
    private bool ClipLineToPaperEdges(
        Vector3 lineP1, Vector3 lineP2,
        Vector3 axisDir, Vector3 axisMidpoint,
        Vector3 planeNormal, PaperGraph graph,
        out Vector3 clippedP1, out Vector3 clippedP2,
        out float maxHeightAtAxis)
    {
        clippedP1 = lineP1;
        clippedP2 = lineP2;
        maxHeightAtAxis = 0f;

        // Build a 2D basis on the plane perpendicular to planeNormal
        Vector3 basisU = axisDir;
        Vector3 basisV = Vector3.Cross(planeNormal, basisU).normalized;

        // Project a 3D point onto 2D (u, v) on this plane
        // u = dot(pos - axisMidpoint, basisU)
        // v = dot(pos - axisMidpoint, basisV)

        float minT = float.PositiveInfinity;
        float maxT = float.NegativeInfinity;
        float maxH = float.NegativeInfinity;
        bool foundAny = false;

        // The fold axis in 2D is: point = (t, 0) for all t
        // An edge from A to B in 2D: point = A + s*(B-A), s in [0,1]
        // Intersection: A.v + s*(B.v - A.v) = 0  →  s = -A.v / (B.v - A.v)
        // Then t = A.u + s*(B.u - A.u)
        foreach (Edge edge in graph.edges) {
            float aU = Vector3.Dot(edge.v1.position - axisMidpoint, basisU);
            float aV = Vector3.Dot(edge.v1.position - axisMidpoint, basisV);
            float bU = Vector3.Dot(edge.v2.position - axisMidpoint, basisU);
            float bV = Vector3.Dot(edge.v2.position - axisMidpoint, basisV);

            float dV = bV - aV;
            if (Mathf.Abs(dV) < 0.000001f) continue; // Edge is parallel to the fold axis

            float s = -aV / dV;
            if (s < -0.0001f || s > 1.0001f) continue; // Intersection outside the edge segment

            float t = aU + s * (bU - aU);

            // Interpolate the actual 3D position along the edge to get the height
            Vector3 hitPos3D = edge.v1.position + s * (edge.v2.position - edge.v1.position);
            float h = Vector3.Dot(hitPos3D, planeNormal);
            if (h > maxH) maxH = h;

            if (t < minT) minT = t;
            if (t > maxT) maxT = t;
            foundAny = true;
        }

        if (!foundAny || (maxT - minT) < 0.0001f)
            return false;

        maxHeightAtAxis = maxH;

        // Reconstruct 3D from t values (project back onto the flat plane, height handled separately)
        clippedP1 = axisMidpoint + basisU * minT;
        clippedP2 = axisMidpoint + basisU * maxT;
        return true;
    }

    /// <summary>
    /// Hides the fold axis guide line.
    /// </summary>
    private void HideGuideLine() {
        if (foldAxisGuide != null)
            foldAxisGuide.enabled = false;
    }

    /// <summary>
    /// Lerps the paper rotation to the current step's paperRotation.
    /// Works regardless of whether the step has rotatePaper enabled.
    /// </summary>
    public void Recenter() {
        if (instruction == null || instruction.steps.Count == 0 || controller == null) return;

        // Use the current (or last valid) step index, clamped to valid range
        int idx = Mathf.Clamp(currentStepIndex, 0, instruction.steps.Count - 1);
        FoldStep step = instruction.steps[idx];

        currentPaperRotation = step.paperRotation;
        RecalculatePaperTarget();
    }

    /// <summary>
    /// Recomputes the paper target rotation from the exposed euler values.
    /// Called from OnValidate so tweaking values in the Inspector updates the paper live.
    /// </summary>
    private void RecalculatePaperTarget() {
        if (controller == null) return;

        targetPaperRotation = Quaternion.Euler(currentPaperRotation);
        isPaperLerping = true;
    }

    /// <summary>
    /// Animates every fold back, one at a time, until reaching the base state again.
    /// </summary>
    public void Unfold() {
        if (isUnfolding || instruction == null || controller == null) return;
        StartCoroutine(UnfoldAllRoutine());
    }

    /// <summary>
    /// Plays the unfold animation, then loads a new instruction.
    /// </summary>
    public void ResetPaper(FoldInstruction nextInstruction) {
        if (isUnfolding || controller == null) return;
        StartCoroutine(ResetPaperRoutine(nextInstruction));
    }

    private System.Collections.IEnumerator ResetPaperRoutine(FoldInstruction nextInstruction) {
        if (instruction != null) {
            yield return StartCoroutine(UnfoldAllRoutine(true));
        }
        LoadInstruction(nextInstruction);
    }

    private void PrepareForAnimation() {
        HideGuideLine();
        if (dragHandle != null) dragHandle.gameObject.SetActive(false);
        if (controller != null) controller.ClearPreview();
    }

    private System.Collections.IEnumerator RotatePaperRoutine(Vector3 targetRotation) {
        currentPaperRotation = targetRotation;
        RecalculatePaperTarget();
        while (isPaperLerping) {
            yield return null;
        }
    }

    private System.Collections.IEnumerator AnimateFoldDegreesRoutine(float startDegrees, float targetDegrees, float speed, float delayStart = 0f) {
        float currentDegrees = startDegrees;
        controller.foldDegrees = currentDegrees;
        controller.UpdatePreview();

        if (delayStart > 0f) {
            yield return new WaitForSeconds(delayStart);
        }

        while (Mathf.Abs(currentDegrees - targetDegrees) > 0.01f) {
            currentDegrees = Mathf.MoveTowards(currentDegrees, targetDegrees, speed * Time.deltaTime);
            controller.foldDegrees = currentDegrees;
            controller.UpdatePreview();
            yield return null;
        }

        controller.foldDegrees = targetDegrees;
        controller.UpdatePreview(); 
    }

    private System.Collections.IEnumerator UnfoldAllRoutine(bool skipReload = false) {
        isUnfolding = true;
        PrepareForAnimation();

        int executedCount = currentStepIndex >= 0 ? 
                            Mathf.Min(currentStepIndex, instruction.steps.Count) : 
                            instruction.steps.Count;

        for (int i = executedCount - 1; i >= 0; i--) {
            FoldStep step = instruction.steps[i];

            // Revert paper geometry (no visual change yet until we apply preview)
            controller.UndoFold();

            ConfigureControllerForStep(step);

            // Smoothly rotate the paper if the step (or a previous one) specified an orientation
            Vector3 targetRotation = Vector3.zero;
            bool foundRotation = false;
            for (int r = i; r >= 0; r--) {
                if (instruction.steps[r].rotatePaper) {
                    targetRotation = instruction.steps[r].paperRotation;
                    foundRotation = true;
                    break;
                }
            }

            if (foundRotation || currentPaperRotation != Vector3.zero) {
                // If we didn't find any rotation, default to Vector3.zero as the base state
                if (!foundRotation) targetRotation = Vector3.zero;
                yield return RotatePaperRoutine(targetRotation);
            }
            
            // Animate fold back to 0, start delayed by 0.1s
            yield return AnimateFoldDegreesRoutine(step.foldDegrees, 0f, unfoldAnimationSpeed, 0.1f);
            
            controller.ClearPreview();
            
            // Short pause between folds
            yield return new WaitForSeconds(0.2f);
        }

        if (!skipReload) {
            // Clean reset
            totalAccuracy = 0f;
            foldCount = 0;
            if (HUDCanvas.Instance != null) {
                HUDCanvas.Instance.ResetAccuracyDisplay();
            }
            
            currentStepIndex = 0;
            if (instruction.steps.Count > 0) {
                ApplyStepToController(instruction.steps[0]);
                if (dragHandle != null) dragHandle.gameObject.SetActive(true);
            }
        }

        isUnfolding = false;
    }

    private void ConfigureControllerForStep(FoldStep step) {
        // Set up fold parameters reflecting the ideal fold for this step
        controller.dragPlaneNormal = step.dragPlaneNormal;
        controller.foldTagName = string.IsNullOrEmpty(step.applyTag) ? "" : step.applyTag;
        controller.foldOffset = step.foldOffset;

        // Infer fold point 1 and 2 from ideal drag positions, like UpdateFoldFromDrag does
        Vector3 dragStartLocal = step.dragHandlePosition;
        Vector3 dragEndLocal = step.idealDragPosition;
        Vector3 dragDelta = dragEndLocal - dragStartLocal;

        if (dragDelta.sqrMagnitude >= 0.00001f) {
            Vector3 midpoint = (dragStartLocal + dragEndLocal) * 0.5f;
            // Note: user drags from dragStart to dragEnd. The fold axis is cross(normal, dragDir)
            Vector3 dragDir = dragDelta.normalized;
            Vector3 foldAxisDir = Vector3.Cross(step.dragPlaneNormal, dragDir).normalized;

            if (foldAxisDir.sqrMagnitude >= 0.0001f) {
                controller.foldPoint1 = midpoint + foldAxisDir * controller.foldLineHalfLength;
                controller.foldPoint2 = midpoint - foldAxisDir * controller.foldLineHalfLength;
                controller.foldPlaneVector = step.dragPlaneNormal;
            }
        }

        // Restore the filter tag to match what it was
        controller.selectedFilterTagIndex = 0;
        if (!string.IsNullOrEmpty(step.filterTag)) {
            PaperGraph graph = controller.GetComponent<PaperGraph>();
            if (graph != null && graph.tags != null && graph.tags.Count > 0) {
                var tagKeys = new System.Collections.Generic.List<string>(graph.tags.Keys);
                int idx = tagKeys.IndexOf(step.filterTag);
                if (idx >= 0) controller.selectedFilterTagIndex = idx + 1;
            }
        }
    }

    /// <summary>
    /// Automatically performs the remaining unfold instructions.
    /// </summary>
    public void AutoFold() {
        if (isUnfolding || isAutoFolding || instruction == null || controller == null) return;
        StartCoroutine(AutoFoldAllRoutine());
    }

    private System.Collections.IEnumerator AutoFoldAllRoutine() {
        isAutoFolding = true;
        PrepareForAnimation();

        int startIdx = Mathf.Max(0, currentStepIndex);

        for (int i = startIdx; i < instruction.steps.Count; i++) {
            FoldStep step = instruction.steps[i];

            // Smoothly rotate the paper if the step specified an orientation
            if (step.rotatePaper) {
                yield return RotatePaperRoutine(step.paperRotation);
            }

            ConfigureControllerForStep(step);
            
            // Mark accuracy for auto fold as 100%
            float foldAccuracy = 100f;
            foldCount++;
            totalAccuracy += foldAccuracy;
            float overallAccuracy = totalAccuracy / foldCount;

            if (HUDCanvas.Instance != null) {
                HUDCanvas.Instance.UpdateFoldAccuracy(foldAccuracy);
                HUDCanvas.Instance.UpdateOverallAccuracy(overallAccuracy);
            }

            // Animate forward from 0
            yield return AnimateFoldDegreesRoutine(0f, step.foldDegrees, foldAnimationSpeed);
            
            // Execute real fold
            controller.ExecuteFoldAction();
            controller.ClearPreview(); 

            if (AudioManager.Instance != null) {
                AudioManager.Instance.Play("fold");
            }
            
            currentStepIndex = i + 1;

            yield return new WaitForSeconds(0.2f);
        }

        // All steps done
        PrepareForAnimation();

        isAutoFolding = false;
    }
}

