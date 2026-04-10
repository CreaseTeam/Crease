using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FoldStep
{
    [Tooltip("Position for the drag handle at this step (start of drag).")]
    public Vector3 dragHandlePosition;

    [Tooltip("Ideal end position the player should drag toward. Used to show the guide line.")]
    public Vector3 idealDragPosition;

    [Tooltip("Normal of the plane the drag handle moves on.")]
    public Vector3 dragPlaneNormal = Vector3.up;

    [Tooltip("Fold angle in degrees.")]
    public float foldDegrees = 180f;

    [Tooltip("Offset for flat folds (hinge thickness). 0 for no offset.")]
    public float foldOffset = 0f;

    [Tooltip("Tag to apply to vertices affected by this fold (leave empty for none).")]
    public string applyTag = "";

    [Tooltip("Tag to filter by — only vertices with this tag are folded (leave empty for none).")]
    public string filterTag = "";

    [Tooltip("If true, the fold axis produced by this step is locked. Any subsequent fold whose proposed axis would cross this segment will be held at the last valid value until it no longer crosses.")]
    public bool lockFoldAxis = false;

    [Header("Paper Rotation (Optional)")]
    [Tooltip("If true, the paper will lerp to the specified rotation when this step is loaded.")]
    public bool rotatePaper = false;

    [Tooltip("Euler angles defining the paper's target rotation.")]
    public Vector3 paperRotation;
}

[CreateAssetMenu(fileName = "NewFoldInstruction", menuName = "Crease/Fold Instruction")]
public class FoldInstruction : ScriptableObject
{
    public List<FoldStep> steps = new List<FoldStep>();
}
