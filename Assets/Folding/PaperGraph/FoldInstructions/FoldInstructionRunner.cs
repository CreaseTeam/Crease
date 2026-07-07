using System;
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
/// Committed crease axis stored by <see cref="FoldInstructionRunner"/> for accordion folds.
/// </summary>
public struct SavedCrease
{
    public string Tag;
    public Vector3 P1;
    public Vector3 P2;
    public Vector3 PlaneNormal;
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

    [SerializeField, HideInInspector, FormerlySerializedAs("GuideLineMaterial")]
    Material _legacyGuideLineMaterial;

    [SerializeField, HideInInspector, FormerlySerializedAs("GuideLineWidth")]
    float _legacyGuideLineWidth = 0.005f;

    [SerializeField, HideInInspector, FormerlySerializedAs("GuideLineColor")]
    Color _legacyGuideLineColor = new Color(1f, 1f, 1f, 0.7f);

    [SerializeField, HideInInspector, FormerlySerializedAs("GuideLineHeightOffset")]
    float _legacyGuideLineHeightOffset;

    [SerializeField, HideInInspector, FormerlySerializedAs("CreaseLineColor")]
    Color _legacyCreaseLineColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    [SerializeField, HideInInspector]
    bool _legacyLineStylesMigrated;

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

    [Tooltip("Duration of the accordion collapse animation in seconds.")]
    [FormerlySerializedAs("accordionCollapseDuration")]
    public float AccordionCollapseDuration = 0.75f;

    [Tooltip("How fast vertex rotation runs after a fold (degrees per second).")]
    public float VertexRotationSpeed = 180f;

    [Tooltip("Minimum accordion drag progress (0–1) required before ExecuteFold commits the collapse.")]
    [Range(0.9f, 1f)]
    public float AccordionExecuteMinProgress = 0.99f;

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
    private bool _isCreaseAnimating = false;
    private bool _isExecutingStepAnimation = false;
    private FoldingRunPhase _phase = FoldingRunPhase.Folding;

    private readonly Dictionary<string, SavedCrease> _savedCreases = new Dictionary<string, SavedCrease>();

    public IReadOnlyDictionary<string, SavedCrease> SavedCreases => _savedCreases;

    public bool IsInStickerPhase => _phase == FoldingRunPhase.Stickers;

    public bool IsUnfolding => _isUnfolding;

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

        MigrateLegacyLineStyles();
        RefreshDragHandleVisibility();
    }

    private void Start() {
        if (HUDCanvas.Instance != null) {
            RefreshDragHandleVisibility();
            return;
        }

        if (Instruction != null)
            LoadInstruction(Instruction);
    }

    private void MigrateLegacyLineStyles() {
        if (_legacyLineStylesMigrated)
            return;

        if (_paperGraph != null) {
            if (_legacyGuideLineWidth > 0f)
                _paperGraph.GuideLineWidth = _legacyGuideLineWidth;
            if (_legacyGuideLineColor.a > 0f)
                _paperGraph.GuideLineColor = _legacyGuideLineColor;
            if (_legacyCreaseLineColor.a > 0f) {
                _paperGraph.CreaseMinBrightness = Mathf.Clamp01(_legacyCreaseLineColor.grayscale * 0.5f);
            }
        }

        _legacyLineStylesMigrated = true;
    }

    private void Update() {
        if (InputManager.Instance == null) return;
        if (_isUnfolding || _isAutoFolding || _isCreaseAnimating || _isExecutingStepAnimation) return;

        if (_phase == FoldingRunPhase.Folding && InputManager.Instance.ExecuteFoldTriggered)
            ExecuteCurrentStep();

        if (InputManager.Instance.RecenterTriggered)
            Recenter();
    }

    private void LateUpdate() {
        if (Controller != null) {
            if (_isPaperLerping) {
                Transform paperTransform = Controller.transform;
                paperTransform.rotation = Quaternion.Slerp(paperTransform.rotation, _targetPaperRotation, PaperLerpSpeed * Time.deltaTime);

                if (Quaternion.Angle(paperTransform.rotation, _targetPaperRotation) < 0.1f) {
                    paperTransform.rotation = _targetPaperRotation;
                    _isPaperLerping = false;
                }
            }
        }

        if (_phase != FoldingRunPhase.Folding || Instruction == null || _currentStepIndex < 0
            || _currentStepIndex >= Instruction.Steps.Count || _isCreaseAnimating || _isExecutingStepAnimation)
            return;

        UpdateGuideLine(Instruction.Steps[_currentStepIndex]);
    }

    /// <summary>
    /// Resets the paper and loads the first step of the given instruction set.
    /// </summary>
    public void LoadInstruction(FoldInstruction newInstruction, bool clearDecals = true) {
        Instruction = newInstruction;
        ExitStickerPhase(clearStickers: clearDecals);

        if (HUDCanvas.Instance != null)
            HUDCanvas.Instance.SetFlyCurrentVisible(false);

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

        ClearSavedCreases();
        HideGuideLine();

        // Reset the paper to a fresh sheet
        Controller.ResetSheet();

        // Load the first step
        _currentStepIndex = 0;
        ApplyStepToController(Instruction.Steps[0]);
        RefreshDragHandleVisibility();

        if (!clearDecals && Controller?.DecalManager != null) {
            Controller.DecalManager.InvalidatePreviewCaches();
            Controller.DecalManager.RefreshAfterMeshUpdate(reanchorAuthoring: true);
        }

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

        FoldStep currentStep = Instruction.Steps[_currentStepIndex];

        if (currentStep.IsAccordionFold) {
            if (Controller == null || _paperGraph == null || !_paperGraph.HasAccordionData) {
                Debug.LogWarning("FoldInstructionRunner: Accordion step is not prepared.");
                return;
            }

            if (!Controller.IsAccordionDragComplete(AccordionExecuteMinProgress)) {
                Debug.Log("FoldInstructionRunner: Drag the handle all the way down before executing the accordion fold.");
                return;
            }
        }

        // --- Calculate accuracy before executing the fold ---
        float foldAccuracy = CalculateFoldAccuracy(currentStep);
        RecordStepAccuracy(foldAccuracy);

        Debug.Log($"FoldInstructionRunner: Fold accuracy = {foldAccuracy:F1}%, overall = {_totalAccuracy / _foldCount:F1}%");

        if (currentStep.IsCrease) {
            StartCoroutine(ExecuteCreaseStepRoutine(currentStep));
            return;
        }

        if (currentStep.IsAccordionFold) {
            StartCoroutine(ExecuteAccordionFoldStepRoutine(currentStep));
            return;
        }

        StartCoroutine(ExecuteStandardFoldStepRoutine(currentStep));
    }

    private System.Collections.IEnumerator ExecuteStandardFoldStepRoutine(FoldStep step) {
        _isExecutingStepAnimation = true;
        RefreshDragHandleVisibility();

        Controller.ExecuteFoldAction();
        AudioManager.Instance.Play("fold");
        Debug.Log($"FoldInstructionRunner: Executed step {_currentStepIndex + 1}/{Instruction.Steps.Count}.");

        ApplyLockFoldAxisIfNeeded(step);
        yield return AnimateVertexRotationIfNeeded(step);
        AdvanceToNextStep();

        _isExecutingStepAnimation = false;
        RefreshDragHandleVisibility();
    }

    private System.Collections.IEnumerator ExecuteAccordionFoldStepRoutine(FoldStep step) {
        _isExecutingStepAnimation = true;
        RefreshDragHandleVisibility();

        Controller.CommitAccordionAction();
        Controller.EndAccordionDragStep();
        Controller.ClearPreview();
        AudioManager.Instance.Play("fold");
        Debug.Log($"FoldInstructionRunner: Executed accordion step {_currentStepIndex + 1}/{Instruction.Steps.Count}.");

        ApplyLockFoldAxisIfNeeded(step);
        yield return AnimateVertexRotationIfNeeded(step);
        AdvanceToNextStep();

        _isExecutingStepAnimation = false;
        RefreshDragHandleVisibility();
    }

    private System.Collections.IEnumerator ExecuteCreaseStepRoutine(FoldStep executedStep, bool animateFoldInFirst = false, bool advanceStep = true) {
        _isCreaseAnimating = true;
        RefreshDragHandleVisibility();

        if (animateFoldInFirst) {
            yield return AnimateFoldDegreesRoutine(0f, executedStep.FoldDegrees, FoldAnimationSpeed);

            float startDegrees = Controller.FoldDegrees;
            if (Mathf.Abs(startDegrees) > 0.01f) {
                yield return AnimateFoldDegreesRoutine(startDegrees, 0f, UnfoldAnimationSpeed, 0.1f);
            }

            Controller.FoldDegrees = 0f;
        }

        float creaseDegrees = animateFoldInFirst ? executedStep.FoldDegrees : Controller.FoldDegrees;
        Controller.FoldDegrees = creaseDegrees;
        bool creaseValid = Controller.ExecuteCreaseAction(refreshVisuals: false);

        if (creaseValid) {
            AudioManager.Instance.Play("fold");
            Debug.Log($"FoldInstructionRunner: Executed crease step {_currentStepIndex + 1}/{Instruction.Steps.Count}.");

            string creaseLabel = GetCreaseLabel(executedStep);
            _savedCreases[creaseLabel] = new SavedCrease {
                Tag = creaseLabel,
                P1 = Controller.FoldPoint1,
                P2 = Controller.FoldPoint2,
                PlaneNormal = executedStep.DragPlaneNormal
            };

            ApplyLockFoldAxisIfNeeded(executedStep);
        } else {
            Debug.LogWarning($"FoldInstructionRunner: Crease step {_currentStepIndex + 1} failed — topology was not cut.");
        }

        if (!animateFoldInFirst) {
            float startDegrees = Controller.FoldDegrees;
            if (creaseValid && Mathf.Abs(startDegrees) > 0.01f)
                yield return AnimateFoldDegreesRoutine(startDegrees, 0f, FoldAnimationSpeed);
        }

        Controller.FoldDegrees = 0f;
        Controller.ClearPreview();

        if (creaseValid)
            yield return AnimateVertexRotationIfNeeded(executedStep);

        if (advanceStep)
            AdvanceToNextStep();

        _isCreaseAnimating = false;
        RefreshDragHandleVisibility();
    }

    private bool TrySetupAccordionStep(FoldStep step) {
        Controller.EndAccordionDragStep();

        string tagA = step.AccordionCreaseTagA;
        string tagB = step.AccordionCreaseTagB;
        if (string.IsNullOrEmpty(tagA) || string.IsNullOrEmpty(tagB)) {
            Debug.LogWarning($"FoldInstructionRunner: Accordion step requires AccordionCreaseTagA and AccordionCreaseTagB.");
            return false;
        }

        if (!_savedCreases.TryGetValue(tagA, out SavedCrease creaseA)
            || !_savedCreases.TryGetValue(tagB, out SavedCrease creaseB)) {
            Debug.LogWarning($"FoldInstructionRunner: Accordion crease tags not found (\"{tagA}\", \"{tagB}\").");
            return false;
        }

        bool prepared = Controller.PrepareAccordionAction(
            tagA, tagB,
            creaseA.P1, creaseA.P2,
            creaseB.P1, creaseB.P2,
            step.FoldDegrees, step.DragPlaneNormal, step.FoldOffset);
        if (!prepared) {
            Debug.LogWarning("FoldInstructionRunner: Accordion prepare failed.");
            return false;
        }

        if (!_paperGraph.HasAccordionData) {
            Debug.LogWarning("FoldInstructionRunner: Accordion step is not prepared.");
            return false;
        }

        if (!AccordionCollapse.TryComputeDragPath(
                _paperGraph,
                _paperGraph.GetAccordionData(),
                out AccordionDragPath dragPath,
                out string pathError)) {
            Debug.LogWarning($"FoldInstructionRunner: Accordion drag path failed — {pathError}");
            return false;
        }

        Controller.BeginAccordionDragStep(dragPath.DragStart, dragPath.DragEnd);

        if (DragHandle != null)
            DragHandle.transform.position = Controller.transform.TransformPoint(dragPath.DragStart);

        return true;
    }

    private System.Collections.IEnumerator AnimateAccordionDragRoutine(float targetT) {
        float elapsed = 0f;
        Controller.SetAccordionPreviewProgress(0f);

        while (elapsed < AccordionCollapseDuration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / AccordionCollapseDuration) * targetT;
            Controller.SetAccordionPreviewProgress(t);

            if (DragHandle != null)
                DragHandle.transform.position = Controller.transform.TransformPoint(Controller.DragHandlePosition);

            yield return null;
        }

        Controller.SetAccordionPreviewProgress(targetT);

        if (DragHandle != null)
            DragHandle.transform.position = Controller.transform.TransformPoint(Controller.DragHandlePosition);
    }

    /// <summary>
    /// Finds a previously committed crease whose axis crosses the given fold axis.
    /// </summary>
    private bool TryFindCrossingCreaseTag(
        Vector3 foldP1, Vector3 foldP2, Vector3 planeNormal, string excludeTag, out string crossingTag) {
        crossingTag = null;

        if (_savedCreases.Count == 0)
            return false;

        foreach (var kvp in _savedCreases) {
            if (!string.IsNullOrEmpty(excludeTag) && kvp.Key == excludeTag)
                continue;

            SavedCrease other = kvp.Value;
            Vector3 n = planeNormal.sqrMagnitude > 0.0001f ? planeNormal : other.PlaneNormal;
            if (AccordionCollapse.CreaseAxesCross(foldP1, foldP2, other.P1, other.P2, n)) {
                crossingTag = kvp.Key;
                return true;
            }
        }

        return false;
    }

    private void ApplyLockFoldAxisIfNeeded(FoldStep step) {
        if (!step.LockFoldAxis) return;

        _hasSavedFoldAxis = true;
        _savedFoldAxisP1 = SnapToNearestVertex(Controller.FoldPoint1);
        _savedFoldAxisP2 = SnapToNearestVertex(Controller.FoldPoint2);
    }

    private void AdvanceToNextStep() {
        _currentStepIndex++;

        if (_currentStepIndex < Instruction.Steps.Count) {
            ApplyStepToController(Instruction.Steps[_currentStepIndex]);
            Debug.Log($"FoldInstructionRunner: Loaded step {_currentStepIndex + 1}/{Instruction.Steps.Count}. Press ExecuteFold to execute.");
        } else {
            Controller.ClearPreview();
            HideGuideLine();
            RefreshDragHandleVisibility();

            if (HUDCanvas.Instance != null)
                HUDCanvas.Instance.StopFoldingTimer();

            EnterStickerPhase();
            Debug.Log("FoldInstructionRunner: All steps completed!");
        }
    }

    private string GetCreaseLabel(FoldStep step) {
        return GetCreaseLabelForStepIndex(_currentStepIndex, step);
    }

    private string GetCreaseLabelForStepIndex(int stepIndex, FoldStep step) {
        if (!string.IsNullOrEmpty(step.ApplyTag))
            return step.ApplyTag;

        string fallback = $"crease_{stepIndex}";
        Debug.LogWarning($"FoldInstructionRunner: Crease step {stepIndex + 1} has no ApplyTag — using \"{fallback}\".");
        return fallback;
    }

    private void ClearSavedCreases() {
        _savedCreases.Clear();
    }

    private void UpdateGuideLine(FoldStep step) {
        if (step.IsAccordionFold) {
            UpdateAccordionGuideLine(step);
            return;
        }

        UpdateFoldGuideLine(step);
    }

    private void UpdateAccordionGuideLine(FoldStep step) {
        if (Controller == null || !Controller.IsAccordionDragStep) {
            HideGuideLine();
            return;
        }

        Vector3 dragStart = Controller.AccordionDragStart;
        Vector3 dragEnd = Controller.AccordionDragEnd;
        if ((dragEnd - dragStart).sqrMagnitude < 0.00001f) {
            HideGuideLine();
            return;
        }

        ShowGuideLine(step, dragStart, dragEnd);
    }

    private void UpdateFoldGuideLine(FoldStep step) {
        Vector3 dragStart = GetStepDragHandlePosition(step);
        Vector3 dragEnd = GetStepIdealDragPosition(step);
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

        if (_paperGraph != null && _paperGraph.Edges.Count > 0) {
            Vector3 clippedP1, clippedP2;
            float clippedMaxHeight;
            if (ClipLineToPaperEdges(lineP1, lineP2, foldAxisDir, idealMidpoint, planeNormal, _paperGraph, out clippedP1, out clippedP2, out clippedMaxHeight)) {
                lineP1 = clippedP1;
                lineP2 = clippedP2;
            } else {
                HideGuideLine();
                return;
            }
        }

        ShowGuideLine(step, lineP1, lineP2);
    }

    private void ShowGuideLine(FoldStep step, Vector3 p1, Vector3 p2) {
        if (Controller?.DecalManager == null || _paperGraph == null || step == null)
            return;

        Controller.DecalManager.UpdateFoldGuide(
            p1,
            p2,
            step.DragPlaneNormal.normalized,
            ResolveFilterTagsForStep(step),
            _paperGraph);
    }

    private void HideGuideLine() {
        Controller?.DecalManager?.HideFoldGuide();
    }

    /// <summary>
    /// Restores folding-mode UI when returning from flight. Re-enters sticker phase if all folds are done.
    /// </summary>
    public void OnEnterFoldingMode() {
        if (IsFoldingComplete)
            ReenterStickerPhaseFromFlight();
        else if (_phase == FoldingRunPhase.Stickers)
            ExitStickerPhase(clearStickers: false);

        RefreshDragHandleVisibility();
    }

    private void EnterStickerPhase() {
        _phase = FoldingRunPhase.Stickers;
        if (HUDCanvas.Instance != null) {
            HUDCanvas.Instance.ShowStickerUI(true);
            HUDCanvas.Instance.SetFlyCurrentVisible(true);
        }

        if (Controller != null) {
            Controller.ClearPreview();
            Controller.DecalManager?.PreparePlacement();
        }

        RefreshDragHandleVisibility();

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

        RefreshDragHandleVisibility();

        HideGuideLine();

        StickerUIController stickerUi = FindFirstObjectByType<StickerUIController>();
        if (stickerUi != null)
            stickerUi.PopulateDropdown();
    }

    private void ExitStickerPhase(bool clearStickers, bool includeDamageDecals = true) {
        _phase = FoldingRunPhase.Folding;
        if (clearStickers)
            ClearStickersOnPaper(includeDamageDecals);
        if (HUDCanvas.Instance != null)
            HUDCanvas.Instance.ShowStickerUI(false);
    }

    private void ClearStickersOnPaper(bool includeDamageDecals = true) {
        if (Controller == null || Controller.DecalManager == null) return;
        if (includeDamageDecals)
            Controller.DecalManager.ClearDecals();
        else
            Controller.DecalManager.ClearUserStickers();
    }

    /// <summary>
    /// Instantly resets paper, stickers, and fold progress to step 0.
    /// </summary>
    public void InstantResetPaper() {
        if (_isUnfolding || Instruction == null) return;
        LoadInstruction(Instruction);
    }

    /// <summary>
    /// Instantly puts the paper into the flat, non-interactive state used for the
    /// level-end reveal: stops any fold animation, flattens the sheet, orients it to the
    /// base (unfolded) world rotation, and hides the drag handle and fold guide line.
    /// Unlike <see cref="InstantResetPaper"/> this does NOT re-arm fold step 0, so the
    /// handle and guide line stay hidden (no fold interaction behind the end screen).
    /// </summary>
    public void EnterLevelEndState() {
        StopAllCoroutines();
        _isUnfolding = false;
        _isAutoFolding = false;
        _isCreaseAnimating = false;
        _isExecutingStepAnimation = false;

        ExitStickerPhase(clearStickers: false);
        ClearSavedCreases();

        if (Controller != null) {
            Controller.ResetSheet();
            Controller.ClearPreview();
        }

        SettleIntoLevelEndState();
        RefreshDragHandleVisibility();
    }

    /// <summary>
    /// Folds the paper up to its fully folded shape without any visible animation. Used at
    /// level end for the default plane (which the player never folded by hand) so there is a
    /// folded plane to unfold; folds happen off-screen during the camera pan. A no-op when
    /// the plane is already fully folded (the hand-folded case).
    /// </summary>
    public void PrepareLevelEndFold() {
        if (Instruction == null || Controller == null) return;
        if (_isUnfolding || _isAutoFolding) return;
        if (_currentStepIndex >= Instruction.Steps.Count) return; // already fully folded

        ExitStickerPhase(clearStickers: false);
        StartCoroutine(AutoFoldAllRoutine(fast: true, enterStickerPhase: false));
    }

    /// <summary>
    /// Animated level-end reveal: plays the same fold-by-fold unfold as <see cref="Unfold"/>,
    /// then finishes in the flat, non-interactive level-end state (rather than re-arming
    /// fold step 0). Falls back to an instant flatten when there is nothing to animate.
    /// Waits for any in-progress silent fold-up (see <see cref="PrepareLevelEndFold"/>) so
    /// the default plane unfolds just like a hand-folded one.
    /// </summary>
    public void UnfoldForLevelEnd() {
        if (Instruction == null || Controller == null) {
            EnterLevelEndState();
            return;
        }
        if (_isUnfolding) return;

        ExitStickerPhase(clearStickers: false);
        StartCoroutine(UnfoldForLevelEndRoutine());
    }

    private System.Collections.IEnumerator UnfoldForLevelEndRoutine() {
        // If the plane is still folding up silently, let it finish first.
        while (_isAutoFolding)
            yield return null;

        yield return UnfoldAllRoutine(preserveStickers: true, levelEnd: true);
    }

    /// <summary>
    /// Shared terminal state for the level-end reveal: orients the paper to the base
    /// (unfolded) world rotation and hides the drag handle and fold guide line so no fold
    /// interaction remains behind the end screen.
    /// </summary>
    private void SettleIntoLevelEndState() {
        _hasSavedFoldAxis = false;
        _totalAccuracy = 0f;
        _foldCount = 0;

        // The unfolded base orientation is world identity (Unfold() returns here), which
        // is the flat pose the folding camera frames. Stop the rotation lerp so LateUpdate
        // leaves the paper at this orientation.
        CurrentPaperRotation = Vector3.zero;
        _isPaperLerping = false;
        if (Controller != null) {
            Controller.HasFoldAxisLock = false;
            Controller.transform.rotation = Quaternion.identity;
        }

        // Terminal state: no active fold step, so LateUpdate stops redrawing the fold
        // guide line and re-arming the handle.
        _currentStepIndex = -1;
        HideGuideLine();

        RefreshDragHandleVisibility();
    }

    bool ShouldShowDragHandle() {
        if (DragHandle == null)
            return false;

        if (HUDCanvas.Instance != null && !HUDCanvas.Instance.HasStartedFolding)
            return false;

        if (_phase != FoldingRunPhase.Folding)
            return false;

        if (_isUnfolding || _isAutoFolding || _isCreaseAnimating || _isExecutingStepAnimation)
            return false;

        if (Instruction == null || _currentStepIndex < 0 || _currentStepIndex >= Instruction.Steps.Count)
            return false;

        return true;
    }

    void RefreshDragHandleVisibility() {
        if (DragHandle == null)
            return;

        DragHandle.gameObject.SetActive(ShouldShowDragHandle());
    }

    /// <summary>
    /// Compares the actual drag handle position (where the player dragged) against
    /// the ideal drag position defined in the step. Returns a 0–100 accuracy score.
    /// </summary>
    private float CalculateFoldAccuracy(FoldStep step) {
        bool useAccordionPath = step.IsAccordionFold && Controller != null && Controller.IsAccordionDragStep;
        Vector3 dragHandlePosition = useAccordionPath
            ? Controller.AccordionDragStart
            : GetStepDragHandlePosition(step);
        Vector3 idealDragPosition = useAccordionPath
            ? Controller.AccordionDragEnd
            : GetStepIdealDragPosition(step);

        // Get the actual drag position: the drag handle's current position in local space
        Vector3 actualDragPos;
        if (DragHandle != null && Controller != null) {
            actualDragPos = Controller.transform.InverseTransformPoint(DragHandle.transform.position);
        } else {
            // Fallback: use the controller's current drag handle position
            actualDragPos = Controller != null ? Controller.DragHandlePosition : dragHandlePosition;
        }

        float distance = Vector3.Distance(actualDragPos, idealDragPosition);
        float idealFoldDistance = Vector3.Distance(dragHandlePosition, idealDragPosition);
        float normalizedError = idealFoldDistance > 0.0001f ? distance / idealFoldDistance : 0f;
        float accuracy = Mathf.Exp(-AccuracyFalloff * normalizedError) * 100f;
        return accuracy;
    }

    private Vector3 GetStepDragHandlePosition(FoldStep step) {
        return Instruction != null ? Instruction.ApplyOffset(step.DragHandlePosition) : step.DragHandlePosition;
    }

    private Vector3 GetStepIdealDragPosition(FoldStep step) {
        return Instruction != null ? Instruction.ApplyOffset(step.IdealDragPosition) : step.IdealDragPosition;
    }

    private Vector3 GetStepVertexRotationPivot(FoldStep step) {
        return Instruction != null ? Instruction.ApplyOffset(step.VertexRotationPivot) : step.VertexRotationPivot;
    }

    private void RecordStepAccuracy(float foldAccuracy) {
        _foldCount++;
        _totalAccuracy += foldAccuracy;
        float overallAccuracy = _totalAccuracy / _foldCount;

        if (HUDCanvas.Instance != null) {
            HUDCanvas.Instance.UpdateFoldAccuracy(foldAccuracy);
            HUDCanvas.Instance.UpdateOverallAccuracy(overallAccuracy);
        }
    }

    private System.Collections.IEnumerator AnimateVertexRotationIfNeeded(FoldStep step) {
        if (!step.RotateVertices || Controller == null)
            yield break;

        Vector3 pivot = GetStepVertexRotationPivot(step);
        if (!Controller.BeginVertexRotationAnimation(pivot, step.VertexRotationAxis, step.VertexRotationDegrees)) {
            Debug.LogWarning($"FoldInstructionRunner: Vertex rotation on step {_currentStepIndex + 1} failed — axis is zero or invalid.");
            yield break;
        }

        float targetDegrees = step.VertexRotationDegrees;
        float duration = Mathf.Abs(targetDegrees) / Mathf.Max(VertexRotationSpeed, 0.01f);
        float elapsed = 0f;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            Controller.ApplyVertexRotationProgress(t);
            yield return null;
        }

        Controller.CommitVertexRotationAnimation();
    }

    /// <summary>
    /// Writes a FoldStep's values into the PaperGraphController and positions the drag handle.
    /// </summary>
    private void ApplyStepToController(FoldStep step) {
        Controller.EndAccordionDragStep();

        Controller.DragPlaneNormal = step.DragPlaneNormal;
        Controller.FoldDegrees = step.FoldDegrees;
        Controller.FoldOffset = step.FoldOffset;
        Controller.FoldTagName = string.IsNullOrEmpty(step.ApplyTag) ? "" : step.ApplyTag;

        Controller.SelectedFilterTags = ResolveFilterTagsForStep(step);

        if (_hasSavedFoldAxis) {
            Controller.HasFoldAxisLock = true;
            Controller.FoldAxisLockP1 = _savedFoldAxisP1;
            Controller.FoldAxisLockP2 = _savedFoldAxisP2;
        } else {
            Controller.HasFoldAxisLock = false;
        }

        if (step.RotatePaper && Controller != null) {
            CurrentPaperRotation = step.PaperRotation;
            RecalculatePaperTarget();
            _isPaperLerping = true;
        }

        if (step.IsAccordionFold) {
            if (!TrySetupAccordionStep(step))
                Controller.ClearPreview();

            UpdateGuideLine(step);
            return;
        }

        Vector3 dragHandlePosition = GetStepDragHandlePosition(step);
        Vector3 idealDragPosition = GetStepIdealDragPosition(step);

        Controller.DragHandlePosition = dragHandlePosition;
        Controller.IdealDragPosition = idealDragPosition;

        if (AutoSnapDragHandle) {
            Controller.SnapDragHandleToOutside();
        }

        if (DragHandle != null) {
            DragHandle.transform.position = Controller.transform.TransformPoint(Controller.DragHandlePosition);
        }

        Controller.RecalculateFoldAxis();
        Controller.LockedFoldPoint1 = Controller.FoldPoint1;
        Controller.LockedFoldPoint2 = Controller.FoldPoint2;

        Controller.ClearPreview();

        UpdateGuideLine(step);
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
        bool preserveStickers = _phase == FoldingRunPhase.Stickers;
        StartCoroutine(UnfoldAllRoutine(preserveStickers));
    }

    /// <summary>
    /// Unfolds the paper for refold/plane re-selection without restarting fold step 0.
    /// </summary>
    public void UnfoldForRefold(Action onComplete = null) {
        if (_isUnfolding || Instruction == null || Controller == null) {
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(UnfoldAllRoutine(preserveStickers: false, levelEnd: false, refold: true, onComplete));
    }

    private void PrepareForAnimation() {
        HideGuideLine();
        RefreshDragHandleVisibility();
        if (Controller != null) {
            Controller.ClearPreview();
            Controller.DecalManager?.InvalidatePreviewCaches();
        }
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

    private System.Collections.IEnumerator UnfoldAllRoutine(
        bool preserveStickers = false,
        bool levelEnd = false,
        bool refold = false,
        Action onComplete = null) {
        _isUnfolding = true;
        if (HUDCanvas.Instance != null)
            HUDCanvas.Instance.ShowStickerUI(false);
        PrepareForAnimation();

        int executedCount = _currentStepIndex >= 0 ? 
                            Mathf.Min(_currentStepIndex, Instruction.Steps.Count) : 
                            Instruction.Steps.Count;

        for (int i = executedCount - 1; i >= 0; i--) {
            FoldStep step = Instruction.Steps[i];

            // Revert authoring topology without snapping decals to the flat mesh.
            Controller.UndoAuthoringFold();
            Controller.DecalManager?.ReanchorPlacementDataOnly();

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
            Controller.DecalManager?.InvalidatePreviewCaches();

            // Short pause between folds
            yield return new WaitForSeconds(0.2f);
        }

        if (levelEnd)
            SettleIntoLevelEndState();
        else if (refold)
            SettleForRefoldSelection();
        else
            RestartFoldInstructionsAfterUnfold(preserveStickers);
        _isUnfolding = false;
        RefreshDragHandleVisibility();
        onComplete?.Invoke();
        HUDCanvas.Instance?.RefreshStickerUiVisibility();
    }

    private void SettleForRefoldSelection() {
        ExitStickerPhase(clearStickers: false);

        _currentStepIndex = -1;
        _hasSavedFoldAxis = false;
        if (Controller != null)
            Controller.HasFoldAxisLock = false;

        HideGuideLine();
        RefreshDragHandleVisibility();

        Controller?.ClearPreview();

        if (Controller?.DecalManager != null) {
            Controller.DecalManager.InvalidatePreviewCaches();
            Controller.DecalManager.RefreshAfterMeshUpdate(reanchorAuthoring: true);
        }
    }

    private void RestartFoldInstructionsAfterUnfold(bool preserveStickers) {
        if (preserveStickers)
            ExitStickerPhase(clearStickers: false);

        _totalAccuracy = 0f;
        _foldCount = 0;
        if (HUDCanvas.Instance != null) {
            HUDCanvas.Instance.ResetAccuracyDisplay();
            HUDCanvas.Instance.StartFoldingTimer();
        }

        _hasSavedFoldAxis = false;
        if (Controller != null)
            Controller.HasFoldAxisLock = false;

        Controller?.ClearPreview();

        _currentStepIndex = 0;
        if (Instruction != null && Instruction.Steps.Count > 0) {
            ApplyStepToController(Instruction.Steps[0]);
        }
        RefreshDragHandleVisibility();

        if (preserveStickers && Controller?.DecalManager != null) {
            Controller.DecalManager.InvalidatePreviewCaches();
            Controller.DecalManager.RefreshAfterMeshUpdate(reanchorAuthoring: true);
        }

        if (HUDCanvas.Instance != null)
            HUDCanvas.Instance.SetFlyCurrentVisible(false);
    }

    private void ConfigureControllerForStep(FoldStep step) {
        Vector3 dragHandlePosition = GetStepDragHandlePosition(step);
        Vector3 idealDragPosition = GetStepIdealDragPosition(step);

        // Set up fold parameters reflecting the ideal fold for this step
        Controller.DragHandlePosition = dragHandlePosition;
        Controller.IdealDragPosition = idealDragPosition;
        Controller.DragPlaneNormal = step.DragPlaneNormal;
        Controller.FoldDegrees = step.FoldDegrees;
        Controller.FoldTagName = string.IsNullOrEmpty(step.ApplyTag) ? "" : step.ApplyTag;
        Controller.FoldOffset = step.FoldOffset;

        // Infer fold point 1 and 2 from ideal drag positions, like UpdateFoldFromDrag does
        Vector3 dragDelta = idealDragPosition - dragHandlePosition;

        if (dragDelta.sqrMagnitude >= 0.00001f) {
            Vector3 midpoint = (dragHandlePosition + idealDragPosition) * 0.5f;
            // Note: user drags from dragStart to dragEnd. The fold axis is cross(normal, dragDir)
            Vector3 dragDir = dragDelta.normalized;
            Vector3 foldAxisDir = Vector3.Cross(step.DragPlaneNormal, dragDir).normalized;

            if (foldAxisDir.sqrMagnitude >= 0.0001f) {
                Controller.FoldPoint1 = midpoint + foldAxisDir * Controller.FoldLineHalfLength;
                Controller.FoldPoint2 = midpoint - foldAxisDir * Controller.FoldLineHalfLength;
                Controller.FoldPlaneVector = step.DragPlaneNormal;
                Controller.LockedFoldPoint1 = Controller.FoldPoint1;
                Controller.LockedFoldPoint2 = Controller.FoldPoint2;
            }
        }

        Controller.SelectedFilterTags = ResolveFilterTagsForStep(step);
    }

        private List<string> ResolveFilterTagsForStep(FoldStep step) {
            IReadOnlyList<string> filterTags = step.FilterTags?.Tags;
            if (filterTags == null || filterTags.Count == 0)
                return new List<string>();

            List<string> resolved = new List<string>();
            foreach (string tag in filterTags) {
            if (string.IsNullOrEmpty(tag)) continue;
            if (_paperGraph != null && _paperGraph.Tags != null && !_paperGraph.Tags.ContainsKey(tag)) {
                Debug.LogWarning($"FoldInstructionRunner: Filter tag \"{tag}\" not found on graph.");
            }
            resolved.Add(tag);
        }

        return resolved;
    }

    /// <summary>
    /// Automatically performs the remaining unfold instructions.
    /// </summary>
    public void AutoFold() {
        if (_isUnfolding || _isAutoFolding || _isCreaseAnimating || Instruction == null || Controller == null) return;
        StartCoroutine(AutoFoldAllRoutine());
    }

    /// <summary>
    /// Folds every remaining instruction step onto the paper.
    /// When <paramref name="fast"/> is true the fold animations, paper rotations and
    /// inter-step pauses are collapsed to near-instant so the paper snaps to its fully
    /// folded shape in a handful of frames (used to fold the default plane up off-screen
    /// during the level-end camera pan, before the unfold reveal). When
    /// <paramref name="enterStickerPhase"/> is false the sticker/timer wrap-up is skipped.
    /// </summary>
    private System.Collections.IEnumerator AutoFoldAllRoutine(bool fast = false, bool enterStickerPhase = true) {
        _isAutoFolding = true;
        PrepareForAnimation();

        // Temporarily crank the animation speeds so every sub-routine (fold, crease,
        // accordion, vertex rotation, paper lerp) resolves in ~1 frame.
        float origFoldSpeed = FoldAnimationSpeed;
        float origUnfoldSpeed = UnfoldAnimationSpeed;
        float origVertexSpeed = VertexRotationSpeed;
        float origPaperLerpSpeed = PaperLerpSpeed;
        float origAccordionDuration = AccordionCollapseDuration;
        if (fast) {
            FoldAnimationSpeed = UnfoldAnimationSpeed = VertexRotationSpeed = 100000f;
            PaperLerpSpeed = 100000f;
            AccordionCollapseDuration = 0f;
        }

        int startIdx = Mathf.Max(0, _currentStepIndex);

        for (int i = startIdx; i < Instruction.Steps.Count; i++) {
            FoldStep step = Instruction.Steps[i];

            // Smoothly rotate the paper if the step specified an orientation
            if (step.RotatePaper) {
                yield return RotatePaperRoutine(step.PaperRotation);
            }

            ConfigureControllerForStep(step);

            // Mark accuracy for auto fold as 100%
            RecordStepAccuracy(100f);

            if (step.IsCrease) {
                RecordStepAccuracy(100f);
                yield return ExecuteCreaseStepRoutine(step, animateFoldInFirst: true, advanceStep: false);
            } else if (step.IsAccordionFold) {
                ApplyStepToController(step);

                if (_paperGraph != null && _paperGraph.HasAccordionData) {
                    yield return AnimateAccordionDragRoutine(1f);
                    Controller.CommitAccordionAction();
                    Controller.EndAccordionDragStep();
                    ApplyLockFoldAxisIfNeeded(step);
                }

                Controller.ClearPreview();

                if (!fast && AudioManager.Instance != null)
                    AudioManager.Instance.Play("fold");
            } else {
                yield return AnimateFoldDegreesRoutine(0f, step.FoldDegrees, FoldAnimationSpeed);

                Controller.ExecuteFoldAction();
                Controller.ClearPreview();
                ApplyLockFoldAxisIfNeeded(step);

                if (!fast && AudioManager.Instance != null)
                    AudioManager.Instance.Play("fold");
            }

            yield return AnimateVertexRotationIfNeeded(step);

            _currentStepIndex = i + 1;

            // Short pause between folds (skipped when folding up silently).
            if (!fast)
                yield return new WaitForSeconds(0.2f);
        }

        if (fast) {
            FoldAnimationSpeed = origFoldSpeed;
            UnfoldAnimationSpeed = origUnfoldSpeed;
            VertexRotationSpeed = origVertexSpeed;
            PaperLerpSpeed = origPaperLerpSpeed;
            AccordionCollapseDuration = origAccordionDuration;
        }

        // All steps done
        PrepareForAnimation();

        if (enterStickerPhase) {
            if (HUDCanvas.Instance != null)
                HUDCanvas.Instance.StopFoldingTimer();

            EnterStickerPhase();
        }

        _isAutoFolding = false;
        RefreshDragHandleVisibility();
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
