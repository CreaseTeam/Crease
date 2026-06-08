using System.Collections.Generic;
using Crease.Audio;
using Crease.Folding.Stickers;
using Crease.Managers.Input;
using Crease.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Folding.PaperGraph
{

public enum FoldingRunPhase
{
    Folding,
    Stickers
}

/// <summary>
/// Loads a FoldInstruction asset and runs its steps sequentially.
/// Uses InputManager for Folding actions (ExecuteFold, Recenter).
/// </summary>
public class FoldInstructionRunner : MonoBehaviour
{
    [Tooltip("The instruction set to run. Assign in the Inspector or call LoadInstruction() at runtime.")]
    [FormerlySerializedAs("instruction")]
    public FoldInstruction Instruction;

    [Header("References")]
    [Tooltip("The PaperGraphController on this GameObject.")]
    [FormerlySerializedAs("controller")]
    public PaperGraphController Controller;

    [Tooltip("The drag handle whose position reflects the current step.")]
    [FormerlySerializedAs("dragHandle")]
    public FoldDragHandle DragHandle;

    [Header("Fold Axis Guide Line")]
    [Tooltip("LineRenderer used to display the ideal fold axis. Created automatically if not assigned.")]
    [FormerlySerializedAs("foldAxisGuide")]
    public LineRenderer FoldAxisGuide;

    [Tooltip("Material for the guide line. Should use a dashed/dotted texture with Tiling.")]
    [FormerlySerializedAs("guideLineMaterial")]
    public Material GuideLineMaterial;

    [Tooltip("Width of the guide line.")]
    [FormerlySerializedAs("guideLineWidth")]
    public float GuideLineWidth = 0.005f;

    [Tooltip("Color of the guide line.")]
    [FormerlySerializedAs("guideLineColor")]
    public Color GuideLineColor = new Color(1f, 1f, 1f, 0.7f);

    [Tooltip("How far above the topmost paper layer the guide line floats.")]
    [FormerlySerializedAs("guideLineHeightOffset")]
    public float GuideLineHeightOffset = 0.002f;

    [Header("Accuracy")]
    [Tooltip("Controls how steeply accuracy falls off with distance. Higher = sharper dropoff.")]
    [FormerlySerializedAs("accuracyFalloff")]
    public float AccuracyFalloff = 5f;

    [Header("Paper Rotation Settings")]
    [Tooltip("How fast the paper lerps to its target rotation.")]
    [FormerlySerializedAs("paperLerpSpeed")]
    public float PaperLerpSpeed = 5f;

    [Tooltip("Current paper rotation (euler angles). Modify in editor to adjust paper live.")]
    [FormerlySerializedAs("currentPaperRotation")]
    public Vector3 CurrentPaperRotation;

    [Header("Unfold Settings")]
    [Tooltip("How fast the paper unfolds (degrees per second).")]
    [FormerlySerializedAs("unfoldAnimationSpeed")]
    public float UnfoldAnimationSpeed = 180f;

    [Header("AutoFold Settings")]
    [Tooltip("How fast the paper folds automatically (degrees per second).")]
    [FormerlySerializedAs("foldAnimationSpeed")]
    public float FoldAnimationSpeed = 180f;

    [Tooltip("If true, automatically snaps the drag handle to the outside of the paper surface when a step begins.")]
    [FormerlySerializedAs("autoSnapDragHandle")]
    public bool AutoSnapDragHandle = false;

    private PaperGraph _paperGraph;

    private int _currentStepIndex = -1;

    // Accuracy tracking
    private float _totalAccuracy = 0f;
    private int _foldCount = 0;

    // Paper rotation lerp state
    private bool _isPaperLerping = false;
    private Quaternion _targetPaperRotation;

    private bool _isUnfolding = false;
    private bool _isAutoFolding = false;
    private FoldingRunPhase _phase = FoldingRunPhase.Folding;

    public bool IsInStickerPhase => _phase == FoldingRunPhase.Stickers;

    public bool IsFoldingComplete =>
        Instruction != null
        && Instruction.Steps.Count > 0
        && _currentStepIndex >= Instruction.Steps.Count;

    // Set when a step with LockFoldAxis == true is executed.
    // Cleared when LoadInstruction is called.
    private bool _hasSavedFoldAxis = false;
    private Vector3 _savedFoldAxisP1;
    private Vector3 _savedFoldAxisP2;

    private void OnValidate() {
        RecalculatePaperTarget();
    }

    private void Awake() {
        if (Controller == null)
            Controller = GetComponent<PaperGraphController>();
        if (Controller != null)
            _paperGraph = Controller.GetComponent<PaperGraph>();
    }

    private void Start() {
        EnsureGuideLineRenderer();

        if (Instruction != null)
            LoadInstruction(Instruction);
    }

    private void EnsureGuideLineRenderer() {
        if (FoldAxisGuide == null) {
            GameObject guideObj = new GameObject("FoldAxisGuide");
            guideObj.transform.SetParent(Controller != null ? Controller.transform : transform, false);
            FoldAxisGuide = guideObj.AddComponent<LineRenderer>();
        }

        FoldAxisGuide.useWorldSpace = false;
        FoldAxisGuide.positionCount = 2;
        FoldAxisGuide.startWidth = GuideLineWidth;
        FoldAxisGuide.endWidth = GuideLineWidth;
        FoldAxisGuide.startColor = GuideLineColor;
        FoldAxisGuide.endColor = GuideLineColor;
        FoldAxisGuide.textureMode = LineTextureMode.Tile;

        if (GuideLineMaterial != null)
            FoldAxisGuide.material = GuideLineMaterial;

        FoldAxisGuide.enabled = false;
    }

    private void Update() {
        if (InputManager.Instance == null) return;
        if (_isUnfolding || _isAutoFolding) return;

        if (_phase == FoldingRunPhase.Folding && InputManager.Instance.ExecuteFoldTriggered)
            ExecuteCurrentStep();

        if (InputManager.Instance.RecenterTriggered)
            Recenter();
    }

    private void LateUpdate() {
        if (!_isPaperLerping || Controller == null) return;

        Transform paperTransform = Controller.transform;
        paperTransform.rotation = Quaternion.Slerp(paperTransform.rotation, _targetPaperRotation, PaperLerpSpeed * Time.deltaTime);

        // Stop lerping when close enough
        if (Quaternion.Angle(paperTransform.rotation, _targetPaperRotation) < 0.1f) {
            paperTransform.rotation = _targetPaperRotation;
            _isPaperLerping = false;
        }
    }

    /// <summary>
    /// Resets the paper and loads the first step of the given instruction set.
    /// </summary>
    public void LoadInstruction(FoldInstruction newInstruction) {
        Instruction = newInstruction;
        ExitStickerPhase(clearStickers: true);

        if (Instruction == null || Instruction.Steps.Count == 0) {
            Debug.LogWarning("FoldInstructionRunner: Instruction is null or has no steps.");
            _currentStepIndex = -1;
            return;
        }

        // Reset accuracy tracking
        _totalAccuracy = 0f;
        _foldCount = 0;
        if (HUDCanvas.Instance != null) {
            HUDCanvas.Instance.ResetAccuracyDisplay();
            HUDCanvas.Instance.StartFoldingTimer();
        }

        // Clear any saved fold-axis lock from a previous instruction
        _hasSavedFoldAxis = false;
        if (Controller != null) Controller.HasFoldAxisLock = false;

        // Reset the paper to a fresh sheet
        Controller.ResetSheet();

        // Re-enable the drag handle
        if (DragHandle != null)
            DragHandle.gameObject.SetActive(true);

        // Load the first step
        _currentStepIndex = 0;
        ApplyStepToController(Instruction.Steps[0]);

        // Debug.Log($"FoldInstructionRunner: Loaded instruction with {Instruction.Steps.Count} step(s). Press ExecuteFold to execute step 1.");
    }

    /// <summary>
    /// Executes the current fold step, applies it to the mesh, then loads the next step's values.
    /// </summary>
    public void ExecuteCurrentStep() {
        if (Instruction == null || Instruction.Steps.Count == 0) {
            Debug.LogWarning("FoldInstructionRunner: No instruction loaded.");
            return;
        }

        if (_currentStepIndex < 0 || _currentStepIndex >= Instruction.Steps.Count) {
            Debug.Log("FoldInstructionRunner: All steps completed.");
            return;
        }

        // --- Calculate accuracy before executing the fold ---
        FoldStep currentStep = Instruction.Steps[_currentStepIndex];
        float foldAccuracy = CalculateFoldAccuracy(currentStep);

        // Update cumulative tracking
        _foldCount++;
        _totalAccuracy += foldAccuracy;
        float overallAccuracy = _totalAccuracy / _foldCount;

        // Update HUD
        HUDCanvas.Instance.UpdateFoldAccuracy(foldAccuracy);
        HUDCanvas.Instance.UpdateOverallAccuracy(overallAccuracy);

        Debug.Log($"FoldInstructionRunner: Fold accuracy = {foldAccuracy:F1}%, overall = {overallAccuracy:F1}%");

        // Execute the fold with the values currently loaded in the controller
        Controller.ExecuteFoldAction();
        AudioManager.Instance.Play("fold");
        Debug.Log($"FoldInstructionRunner: Executed step {_currentStepIndex + 1}/{Instruction.Steps.Count}.");

        // After the fold is committed, check if we should save the fold axis.
        if (currentStep.LockFoldAxis) {
            // Snap each end of the fold axis to the closest vertex in the graph so the
            // lock boundary is defined by actual paper geometry, not the raw half-length points.
            _hasSavedFoldAxis = true;
            _savedFoldAxisP1 = SnapToNearestVertex(Controller.FoldPoint1);
            _savedFoldAxisP2 = SnapToNearestVertex(Controller.FoldPoint2);
        }

        // Advance to the next step
        _currentStepIndex++;

        if (_currentStepIndex < Instruction.Steps.Count) {
            ApplyStepToController(Instruction.Steps[_currentStepIndex]);
            Debug.Log($"FoldInstructionRunner: Loaded step {_currentStepIndex + 1}/{Instruction.Steps.Count}. Press ExecuteFold to execute.");
        } else {
            // Clear the preview so no ghost fold lingers
            Controller.ClearPreview();
            HideGuideLine();

            // Hide the drag handle — no more steps to drag
            if (DragHandle != null)
                DragHandle.gameObject.SetActive(false);

            if (HUDCanvas.Instance != null)
                HUDCanvas.Instance.StopFoldingTimer();

            EnterStickerPhase();
            Debug.Log("FoldInstructionRunner: All steps completed!");
        }
    }

    /// <summary>
    /// Restores folding-mode UI when returning from flight. Re-enters sticker phase if all folds are done.
    /// </summary>
    public void OnEnterFoldingMode() {
        if (IsFoldingComplete)
            ReenterStickerPhaseFromFlight();
        else if (_phase == FoldingRunPhase.Stickers)
            ExitStickerPhase(clearStickers: false);
    }

    private void EnterStickerPhase() {
        _phase = FoldingRunPhase.Stickers;
        if (HUDCanvas.Instance != null)
            HUDCanvas.Instance.ShowStickerUI(true);

        if (Controller != null) {
            Controller.ClearPreview();
            Controller.DecalManager?.PreparePlacement();
        }

        if (DragHandle != null)
            DragHandle.gameObject.SetActive(false);

        HideGuideLine();

        StickerUIController stickerUi = FindFirstObjectByType<StickerUIController>();
        if (stickerUi != null)
            stickerUi.PopulateDropdown();
    }

    private void ReenterStickerPhaseFromFlight() {
        _phase = FoldingRunPhase.Stickers;
        if (HUDCanvas.Instance != null)
            HUDCanvas.Instance.ShowStickerUI(true);

        if (Controller != null)
            Controller.DecalManager?.PreparePlacement(syncPreviewFromAuthoring: false);

        if (DragHandle != null)
            DragHandle.gameObject.SetActive(false);

        HideGuideLine();

        StickerUIController stickerUi = FindFirstObjectByType<StickerUIController>();
        if (stickerUi != null)
            stickerUi.PopulateDropdown();
    }

    private void ExitStickerPhase(bool clearStickers) {
        _phase = FoldingRunPhase.Folding;
        if (clearStickers)
            ClearStickersOnPaper();
        if (HUDCanvas.Instance != null)
            HUDCanvas.Instance.ShowStickerUI(false);
    }

    private void ClearStickersOnPaper() {
        if (Controller == null || Controller.DecalManager == null) return;
        Controller.DecalManager.ClearDecals();
    }

    /// <summary>
    /// Instantly resets paper, stickers, and fold progress to step 0.
    /// </summary>
    public void InstantResetPaper() {
        if (_isUnfolding || Instruction == null) return;
        LoadInstruction(Instruction);
    }

    /// <summary>
    /// Compares the actual drag handle position (where the player dragged) against
    /// the ideal drag position defined in the step. Returns a 0–100 accuracy score.
    /// </summary>
    private float CalculateFoldAccuracy(FoldStep step) {
        // Get the actual drag position: the drag handle's current position in local space
        Vector3 actualDragPos;
        if (DragHandle != null && Controller != null) {
            actualDragPos = Controller.transform.InverseTransformPoint(DragHandle.transform.position);
        } else {
            // Fallback: use the controller's current drag handle position
            actualDragPos = Controller != null ? Controller.DragHandlePosition : step.DragHandlePosition;
        }

        float distance = Vector3.Distance(actualDragPos, step.IdealDragPosition);
        float idealFoldDistance = Vector3.Distance(step.DragHandlePosition, step.IdealDragPosition);
        float normalizedError = idealFoldDistance > 0.0001f ? distance / idealFoldDistance : 0f;
        float accuracy = Mathf.Exp(-AccuracyFalloff * normalizedError) * 100f;
        return accuracy;
    }

    /// <summary>
    /// Writes a FoldStep's values into the PaperGraphController and positions the drag handle.
    /// </summary>
    private void ApplyStepToController(FoldStep step) {
        Controller.DragHandlePosition = step.DragHandlePosition;
        Controller.IdealDragPosition = step.IdealDragPosition;
        Controller.DragPlaneNormal = step.DragPlaneNormal;
        Controller.FoldDegrees = step.FoldDegrees;
        Controller.FoldOffset = step.FoldOffset;

        if (AutoSnapDragHandle) {
            Controller.SnapDragHandleToOutside();
        }

        // Apply tag (tag name to stamp on affected vertices)
        Controller.FoldTagName = string.IsNullOrEmpty(step.ApplyTag) ? "" : step.ApplyTag;

        // Filter tag — resolve index from the tag name, or clear to (None)
        Controller.SelectedFilterTagIndex = 0; // default to no filter
        if (!string.IsNullOrEmpty(step.FilterTag)) {
            if (_paperGraph != null && _paperGraph.Tags != null && _paperGraph.Tags.Count > 0) {
                var tagKeys = new List<string>(_paperGraph.Tags.Keys);
                int idx = tagKeys.IndexOf(step.FilterTag);
                if (idx >= 0) {
                    Controller.SelectedFilterTagIndex = idx + 1; // +1 because 0 is "(None)"
                } else {
                    Debug.LogWarning($"FoldInstructionRunner: Filter tag \"{step.FilterTag}\" not found on graph. Ignoring filter.");
                }
            }
        }

        // Position the drag handle if assigned (local → world)
        if (DragHandle != null) {
            DragHandle.transform.position = Controller.transform.TransformPoint(step.DragHandlePosition);
        }

        // Set up paper rotation lerp if this step specifies it
        if (step.RotatePaper && Controller != null) {
            CurrentPaperRotation = step.PaperRotation;
            RecalculatePaperTarget();
            _isPaperLerping = true;
        }

        // Apply the fold-axis lock if one has been saved from a previous step.
        if (_hasSavedFoldAxis) {
            Controller.HasFoldAxisLock = true;
            Controller.FoldAxisLockP1 = _savedFoldAxisP1;
            Controller.FoldAxisLockP2 = _savedFoldAxisP2;
        } else {
            Controller.HasFoldAxisLock = false;
        }

        // Derive the initial fold axis from the current handle position, THEN seed the
        // "last valid" fallback cache. This ensures that if the very first drag move
        // immediately flags an invalid condition, there is a correct, non-broken axis
        // to restore — not whatever the previous step left behind.
        Controller.RecalculateFoldAxis();
        Controller.LockedFoldPoint1 = Controller.FoldPoint1;
        Controller.LockedFoldPoint2 = Controller.FoldPoint2;

        // Clear the preview — no fold preview until the drag handle is moved
        Controller.ClearPreview();

        UpdateGuideLine(step);
    }

    private void UpdateGuideLine(FoldStep step) {
        if (FoldAxisGuide == null) return;

        Vector3 dragStart = step.DragHandlePosition;
        Vector3 dragEnd = step.IdealDragPosition;
        Vector3 dragDelta = dragEnd - dragStart;

        if (dragDelta.sqrMagnitude < 0.00001f) {
            HideGuideLine();
            return;
        }

        Vector3 planeNormal = step.DragPlaneNormal.normalized;
        Vector3 dragDir = dragDelta.normalized;
        Vector3 foldAxisDir = Vector3.Cross(planeNormal, dragDir).normalized;
        if (foldAxisDir.sqrMagnitude < 0.0001f) {
            HideGuideLine();
            return;
        }

        Vector3 idealMidpoint = (dragStart + dragEnd) * 0.5f;

        float halfLength = Controller != null ? Controller.FoldLineHalfLength : 1f;
        Vector3 lineP1 = idealMidpoint + foldAxisDir * halfLength;
        Vector3 lineP2 = idealMidpoint - foldAxisDir * halfLength;
        float localMaxHeight = Vector3.Dot(idealMidpoint, planeNormal);

        if (_paperGraph != null && _paperGraph.Edges.Count > 0) {
            Vector3 clippedP1, clippedP2;
            float clippedMaxHeight;
            if (ClipLineToPaperEdges(lineP1, lineP2, foldAxisDir, idealMidpoint, planeNormal, _paperGraph, out clippedP1, out clippedP2, out clippedMaxHeight)) {
                lineP1 = clippedP1;
                lineP2 = clippedP2;
                localMaxHeight = clippedMaxHeight;
            } else {
                FoldAxisGuide.enabled = false;
                return;
            }
        }

        float heightOffset = GuideLineHeightOffset;
        if (Mathf.Abs(step.FoldOffset) > 0.00001f)
            heightOffset = Mathf.Min(heightOffset, Mathf.Abs(step.FoldOffset) * 0.5f);

        float lift = (localMaxHeight - Vector3.Dot(idealMidpoint, planeNormal)) + heightOffset;
        Vector3 offset = planeNormal * lift;
        FoldAxisGuide.SetPosition(0, lineP1 + offset);
        FoldAxisGuide.SetPosition(1, lineP2 + offset);
        FoldAxisGuide.enabled = true;
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
        foreach (Edge edge in _paperGraph.Edges) {
            float aU = Vector3.Dot(edge.V1.Position - axisMidpoint, basisU);
            float aV = Vector3.Dot(edge.V1.Position - axisMidpoint, basisV);
            float bU = Vector3.Dot(edge.V2.Position - axisMidpoint, basisU);
            float bV = Vector3.Dot(edge.V2.Position - axisMidpoint, basisV);

            float dV = bV - aV;
            if (Mathf.Abs(dV) < 0.000001f) continue; // Edge is parallel to the fold axis

            float s = -aV / dV;
            if (s < -0.0001f || s > 1.0001f) continue; // Intersection outside the edge segment

            float t = aU + s * (bU - aU);

            // Interpolate the actual 3D position along the edge to get the height
            Vector3 hitPos3D = edge.V1.Position + s * (edge.V2.Position - edge.V1.Position);
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

    private void HideGuideLine() {
        if (FoldAxisGuide != null) FoldAxisGuide.enabled = false;
    }

    /// <summary>
    /// Lerps the paper rotation to the current step's paperRotation.
    /// Works regardless of whether the step has rotatePaper enabled.
    /// </summary>
    public void Recenter() {
        if (Instruction == null || Instruction.Steps.Count == 0 || Controller == null) return;

        // Use the current (or last valid) step index, clamped to valid range
        int idx = Mathf.Clamp(_currentStepIndex, 0, Instruction.Steps.Count - 1);
        FoldStep step = Instruction.Steps[idx];

        CurrentPaperRotation = step.PaperRotation;
        RecalculatePaperTarget();
    }

    /// <summary>
    /// Recomputes the paper target rotation from the exposed euler values.
    /// Called from OnValidate so tweaking values in the Inspector updates the paper live.
    /// </summary>
    private void RecalculatePaperTarget() {
        if (Controller == null) return;

        _targetPaperRotation = Quaternion.Euler(CurrentPaperRotation);
        _isPaperLerping = true;
    }

    /// <summary>
    /// Animates every fold back, one at a time, until reaching the base state again.
    /// </summary>
    public void Unfold() {
        if (_isUnfolding || Instruction == null || Controller == null) return;
        bool skipReload = _phase == FoldingRunPhase.Stickers;
        StartCoroutine(UnfoldAllRoutine(skipReload));
    }

    private void PrepareForAnimation() {
        HideGuideLine();
        if (DragHandle != null) DragHandle.gameObject.SetActive(false);
        if (Controller != null) Controller.ClearPreview();
    }

    private System.Collections.IEnumerator RotatePaperRoutine(Vector3 targetRotation) {
        CurrentPaperRotation = targetRotation;
        RecalculatePaperTarget();
        while (_isPaperLerping) {
            yield return null;
        }
    }

    private System.Collections.IEnumerator AnimateFoldDegreesRoutine(float startDegrees, float targetDegrees, float speed, float delayStart = 0f) {
        float currentDegrees = startDegrees;
        Controller.FoldDegrees = currentDegrees;
        Controller.UpdatePreview();

        if (delayStart > 0f) {
            yield return new WaitForSeconds(delayStart);
        }

        while (Mathf.Abs(currentDegrees - targetDegrees) > 0.01f) {
            currentDegrees = Mathf.MoveTowards(currentDegrees, targetDegrees, speed * Time.deltaTime);
            Controller.FoldDegrees = currentDegrees;
            Controller.UpdatePreview();
            yield return null;
        }

        Controller.FoldDegrees = targetDegrees;
        Controller.UpdatePreview(); 
    }

    private System.Collections.IEnumerator UnfoldAllRoutine(bool skipReload = false) {
        _isUnfolding = true;
        PrepareForAnimation();

        int executedCount = _currentStepIndex >= 0 ? 
                            Mathf.Min(_currentStepIndex, Instruction.Steps.Count) : 
                            Instruction.Steps.Count;

        for (int i = executedCount - 1; i >= 0; i--) {
            FoldStep step = Instruction.Steps[i];

            // Revert paper geometry (no visual change yet until we apply preview)
            Controller.UndoFold();

            ConfigureControllerForStep(step);

            // Smoothly rotate the paper if the step (or a previous one) specified an orientation
            Vector3 targetRotation = Vector3.zero;
            bool foundRotation = false;
            for (int r = i; r >= 0; r--) {
                if (Instruction.Steps[r].RotatePaper) {
                    targetRotation = Instruction.Steps[r].PaperRotation;
                    foundRotation = true;
                    break;
                }
            }

            if (foundRotation || CurrentPaperRotation != Vector3.zero) {
                // If we didn't find any rotation, default to Vector3.zero as the base state
                if (!foundRotation) targetRotation = Vector3.zero;
                yield return RotatePaperRoutine(targetRotation);
            }
            
            // Animate fold back to 0, start delayed by 0.1s
            yield return AnimateFoldDegreesRoutine(step.FoldDegrees, 0f, UnfoldAnimationSpeed, 0.1f);
            
            Controller.ClearPreview();
            
            // Short pause between folds
            yield return new WaitForSeconds(0.2f);
        }

        if (!skipReload) {
            // Clean reset
            _totalAccuracy = 0f;
            _foldCount = 0;
            if (HUDCanvas.Instance != null) {
                HUDCanvas.Instance.ResetAccuracyDisplay();
            }
            
            _currentStepIndex = 0;
            if (Instruction.Steps.Count > 0) {
                ApplyStepToController(Instruction.Steps[0]);
                if (DragHandle != null) DragHandle.gameObject.SetActive(true);
            }
        } else if (_phase == FoldingRunPhase.Stickers) {
            Controller.ClearPreview();
            Controller.DecalManager?.PreparePlacement();
            if (DragHandle != null) DragHandle.gameObject.SetActive(false);
        }

        _isUnfolding = false;
    }

    private void ConfigureControllerForStep(FoldStep step) {
        // Set up fold parameters reflecting the ideal fold for this step
        Controller.DragHandlePosition = step.DragHandlePosition;
        Controller.IdealDragPosition = step.IdealDragPosition;
        Controller.DragPlaneNormal = step.DragPlaneNormal;
        Controller.FoldTagName = string.IsNullOrEmpty(step.ApplyTag) ? "" : step.ApplyTag;
        Controller.FoldOffset = step.FoldOffset;

        // Infer fold point 1 and 2 from ideal drag positions, like UpdateFoldFromDrag does
        Vector3 dragStartLocal = step.DragHandlePosition;
        Vector3 dragEndLocal = step.IdealDragPosition;
        Vector3 dragDelta = dragEndLocal - dragStartLocal;

        if (dragDelta.sqrMagnitude >= 0.00001f) {
            Vector3 midpoint = (dragStartLocal + dragEndLocal) * 0.5f;
            // Note: user drags from dragStart to dragEnd. The fold axis is cross(normal, dragDir)
            Vector3 dragDir = dragDelta.normalized;
            Vector3 foldAxisDir = Vector3.Cross(step.DragPlaneNormal, dragDir).normalized;

            if (foldAxisDir.sqrMagnitude >= 0.0001f) {
                Controller.FoldPoint1 = midpoint + foldAxisDir * Controller.FoldLineHalfLength;
                Controller.FoldPoint2 = midpoint - foldAxisDir * Controller.FoldLineHalfLength;
                Controller.FoldPlaneVector = step.DragPlaneNormal;
            }
        }

        // Restore the filter tag to match what it was
        Controller.SelectedFilterTagIndex = 0;
        if (!string.IsNullOrEmpty(step.FilterTag)) {
            if (_paperGraph != null && _paperGraph.Tags != null && _paperGraph.Tags.Count > 0) {
                var tagKeys = new System.Collections.Generic.List<string>(_paperGraph.Tags.Keys);
                int idx = tagKeys.IndexOf(step.FilterTag);
                if (idx >= 0) Controller.SelectedFilterTagIndex = idx + 1;
            }
        }
    }

    /// <summary>
    /// Automatically performs the remaining unfold instructions.
    /// </summary>
    public void AutoFold() {
        if (_isUnfolding || _isAutoFolding || Instruction == null || Controller == null) return;
        StartCoroutine(AutoFoldAllRoutine());
    }

    private System.Collections.IEnumerator AutoFoldAllRoutine() {
        _isAutoFolding = true;
        PrepareForAnimation();

        int startIdx = Mathf.Max(0, _currentStepIndex);

        for (int i = startIdx; i < Instruction.Steps.Count; i++) {
            FoldStep step = Instruction.Steps[i];

            // Smoothly rotate the paper if the step specified an orientation
            if (step.RotatePaper) {
                yield return RotatePaperRoutine(step.PaperRotation);
            }

            ConfigureControllerForStep(step);
            
            // Mark accuracy for auto fold as 100%
            float foldAccuracy = 100f;
            _foldCount++;
            _totalAccuracy += foldAccuracy;
            float overallAccuracy = _totalAccuracy / _foldCount;

            if (HUDCanvas.Instance != null) {
                HUDCanvas.Instance.UpdateFoldAccuracy(foldAccuracy);
                HUDCanvas.Instance.UpdateOverallAccuracy(overallAccuracy);
            }

            // Animate forward from 0
            yield return AnimateFoldDegreesRoutine(0f, step.FoldDegrees, FoldAnimationSpeed);
            
            // Execute real fold
            Controller.ExecuteFoldAction();
            Controller.ClearPreview(); 

            if (AudioManager.Instance != null) {
                AudioManager.Instance.Play("fold");
            }
            
            _currentStepIndex = i + 1;

            yield return new WaitForSeconds(0.2f);
        }

        // All steps done
        PrepareForAnimation();

        if (HUDCanvas.Instance != null)
            HUDCanvas.Instance.StopFoldingTimer();

        EnterStickerPhase();
        _isAutoFolding = false;
    }

    /// <summary>
    /// Returns the position of the vertex closest to <paramref name="point"/> in 3D local space.
    /// Falls back to <paramref name="point"/> itself if the graph is null or empty.
    /// </summary>
    private Vector3 SnapToNearestVertex(Vector3 point) {
        if (_paperGraph == null || _paperGraph.Vertices == null || _paperGraph.Vertices.Count == 0)
            return point;

        Vector3 best = point;
        float bestDist = float.PositiveInfinity;
        foreach (Vertex v in _paperGraph.Vertices) {
            float d = (v.Position - point).sqrMagnitude;
            if (d < bestDist) {
                bestDist = d;
                best = v.Position;
            }
        }
        return best;
    }
}
}
