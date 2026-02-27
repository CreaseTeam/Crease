using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FoldStep
{
    [Tooltip("Position for the drag handle at this step.")]
    public Vector3 dragHandlePosition;

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

    [Header("Camera (Optional)")]
    [Tooltip("If true, the camera will lerp to the specified orbit when this step is loaded.")]
    public bool moveCamera = false;

    [Tooltip("Euler angles defining the camera's orbital rotation around the paper graph.")]
    public Vector3 cameraOrbitRotation;

    [Tooltip("Distance from the paper graph origin.")]
    public float cameraDistance = 3f;
}

[CreateAssetMenu(fileName = "NewFoldInstruction", menuName = "Crease/Fold Instruction")]
public class FoldInstruction : ScriptableObject
{
    public List<FoldStep> steps = new List<FoldStep>();
}
