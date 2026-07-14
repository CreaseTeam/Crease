using UnityEngine;

/// <summary>
/// How the wave displaces the arc.
/// RotationWave — FK joint bending around a local axis (left/right sweep).
/// VerticalWave — FK position displacement in arc-local Y (pure up/down, no horizontal drift).
/// FluidWave    — Smooth LineRenderer tube; segment MeshRenderers are hidden and a single
///                tube is drawn by displacing the pivot positions directly. No rotation math,
///                no connectivity issues.
/// </summary>
public enum ArcWaveType
{
    RotationWave,
    VerticalWave,
    FluidWave,
}

/// <summary>
/// Animates the Arc's child segments as a traveling sine wave.
/// In FluidWave mode the segment MeshRenderers are hidden and a LineRenderer
/// draws a smooth tube driven purely by positional displacement.
/// Place on the Arc GameObject (parent of all ArcSeg children).
/// </summary>
public class ArcWave : MonoBehaviour
{
    [Header("Wave Settings")]
    [Tooltip("RotationWave: FK joint bends for sweep. VerticalWave: FK Y-position displacement. FluidWave: smooth LineRenderer tube.")]
    [SerializeField] private ArcWaveType waveType = ArcWaveType.RotationWave;

    [Tooltip("Amplitude — degrees per joint for RotationWave; arc-local units for VerticalWave/FluidWave.")]
    [SerializeField] private float amplitude = 5f;

    [Tooltip("Multiplier applied at the tip vs the base (whip-crack taper).")]
    [SerializeField] private float tipAmplitudeBoost = 2.2f;

    [Tooltip("Frequency in Hz — wave cycles per second.")]
    [SerializeField] private float frequency = 0.75f;

    [Tooltip("Phase spacing in radians between adjacent segments.")]
    [SerializeField] private float phaseSpacing = 0.42f;

    [Tooltip("+1 = wave travels base→tip, −1 = tip→base.")]
    [SerializeField] private float waveDirection = 1f;

    [Tooltip("Bend axis in joint-local space (RotationWave only). Default (0,0,1) = Z gives left/right.")]
    [SerializeField] private Vector3 bendAxis = Vector3.forward;

    [Tooltip("If true, TriggerWave() is called automatically on Start.")]
    [SerializeField] private bool triggerOnStart = true;

    [Header("Fluid Wave")]
    [Tooltip("Axis of displacement in arc-local space. (0,1,0) = up/down. (1,0,0) = left/right.")]
    [SerializeField] private Vector3 fluidDisplacementAxis = Vector3.up;

    [Tooltip("LineRenderer tube width at the base of the arc, in world units. Set to 0 to auto-derive from segment scale.")]
    [SerializeField] private float lineWidthBase = 0f;

    [Tooltip("LineRenderer tube width at the tip of the arc, in world units. Set to 0 to auto-derive from segment scale.")]
    [SerializeField] private float lineWidthTip = 0f;

    [Tooltip("Material applied to the LineRenderer. Leave empty to keep the default material.")]
    [SerializeField] private Material lineMaterial;

    // ── Shared ────────────────────────────────────────────────────────────────

    private Transform[]  _segments;
    private Quaternion[] _baseRotations;
    private Vector3[]    _basePositions;
    private bool         _isPlaying;

    // ── RotationWave ──────────────────────────────────────────────────────────

    private Quaternion[] _relativeBaseRotations;
    private Vector3[]    _localBoneVectors;

    // ── VerticalWave ──────────────────────────────────────────────────────────

    private float[]   _boneLengths;
    private Vector3[] _baseChainDirs;
    private Vector3[] _segBaseRights;

    // ── FluidWave ─────────────────────────────────────────────────────────────

    private LineRenderer _lineRenderer;
    private Vector3[]    _fluidPoints; // reused each frame to avoid per-frame allocation

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        CacheSegments();
    }

    private void Start()
    {
        if (triggerOnStart)
            TriggerWave();
    }

    private void Update()
    {
        if (!_isPlaying || _segments == null) return;

        switch (waveType)
        {
            case ArcWaveType.RotationWave: UpdateRotationWave(); break;
            case ArcWaveType.VerticalWave: UpdateVerticalWave(); break;
            case ArcWaveType.FluidWave:    UpdateFluidWave();    break;
        }
    }

    private void OnDestroy()
    {
        StopWave();
    }

    // ── RotationWave ─────────────────────────────────────────────────────────

    private void UpdateRotationWave()
    {
        int count = _segments.Length;
        float time = Time.time * frequency * Mathf.PI * 2f;

        Quaternion chainRot = Quaternion.identity;
        Vector3 pos = _basePositions[0];

        for (int i = 0; i < count; i++)
        {
            float phase = time + i * phaseSpacing * waveDirection;
            float norm = count > 1 ? i / (float)(count - 1) : 0f;
            float segAmp = amplitude * Mathf.Lerp(1f, tipAmplitudeBoost, norm);

            Quaternion jointRot = _relativeBaseRotations[i] * Quaternion.AngleAxis(Mathf.Sin(phase) * segAmp, bendAxis);
            chainRot = (i == 0) ? jointRot : chainRot * jointRot;

            _segments[i].localPosition = pos;
            _segments[i].localRotation = chainRot;

            if (i < count - 1)
                pos += chainRot * _localBoneVectors[i];
        }
    }

    // ── VerticalWave ──────────────────────────────────────────────────────────
    // Displaces each segment in arc-local Y (= world Y) via a sine wave.
    // Arc-local Y is always world Y regardless of the WaterSprout's global Y
    // rotation, so there is zero left/right (world X/Z) drift.

    private void UpdateVerticalWave()
    {
        int count = _segments.Length;
        float time = Time.time * frequency * Mathf.PI * 2f;

        // 1. Compute desired arc-local positions (base + Y offset, zero at base).
        Vector3[] desired = new Vector3[count];
        desired[0] = _basePositions[0]; // anchor — never moves

        for (int i = 1; i < count; i++)
        {
            float phase = time + i * phaseSpacing * waveDirection;
            float norm = i / (float)(count - 1);
            // Amplitude grows from 0 at base to amplitude*tipAmplitudeBoost at tip.
            float amp = amplitude * norm * Mathf.Lerp(1f, tipAmplitudeBoost, norm);
            desired[i] = _basePositions[i] + new Vector3(0f, Mathf.Sin(phase) * amp, 0f);
        }

        // 2. FK constraint: place each segment exactly bone-length from the previous,
        //    aimed at its desired position. This maintains full connectivity.
        Vector3[] positions = new Vector3[count];
        positions[0] = desired[0];

        for (int i = 1; i < count; i++)
        {
            Vector3 delta = desired[i] - positions[i - 1];
            float len = delta.magnitude;
            positions[i] = (len > 0.0001f)
                ? positions[i - 1] + delta * (_boneLengths[i - 1] / len)
                : positions[i - 1] + Vector3.up * _boneLengths[i - 1];
        }

        // 3. Apply positions and align each segment so its local Y points toward the next joint.
        //    The roll (rotation around local Y) is locked to the rest-pose local X of the segment,
        //    so no twist can accumulate regardless of displacement magnitude.
        for (int i = 0; i < count; i++)
        {
            _segments[i].localPosition = positions[i];

            Vector3 chainDir = (i < count - 1)
                ? (positions[i + 1] - positions[i]).normalized
                : (positions[count - 1] - positions[count - 2]).normalized;

            // Project the rest-pose local X onto the plane perpendicular to chainDir.
            // This keeps the segment's roll unchanged while letting Y track the chain.
            Vector3 sideAxis = _segBaseRights[i] - Vector3.Dot(_segBaseRights[i], chainDir) * chainDir;
            if (sideAxis.sqrMagnitude < 0.0001f)
            {
                sideAxis = Vector3.Cross(chainDir, Vector3.up);
                if (sideAxis.sqrMagnitude < 0.0001f)
                    sideAxis = Vector3.Cross(chainDir, Vector3.forward);
            }
            sideAxis.Normalize();

            // LookRotation(Z, Y): local Z = cross(sideAxis, chainDir), local Y = chainDir exactly.
            _segments[i].localRotation = Quaternion.LookRotation(Vector3.Cross(sideAxis, chainDir), chainDir);
        }
    }

    // ── FluidWave ─────────────────────────────────────────────────────────────
    // Uses a LineRenderer tube — no rotation math, no connectivity issues.
    // Segment MeshRenderers are hidden; the tube is driven purely by arc-local Y displacement.

    private void UpdateFluidWave()
    {
        if (_lineRenderer == null) return;

        int count = _segments.Length;
        float time = Time.time * frequency * Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float phase = time + i * phaseSpacing * waveDirection;
            float norm  = count > 1 ? i / (float)(count - 1) : 0f;
            // Base is anchored (amp = 0 at i = 0); amplitude grows toward the tip.
            float amp   = (i == 0) ? 0f : amplitude * norm * Mathf.Lerp(1f, tipAmplitudeBoost, norm);
            _fluidPoints[i] = _basePositions[i] + fluidDisplacementAxis.normalized * (Mathf.Sin(phase) * amp);
        }

        _lineRenderer.SetPositions(_fluidPoints);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Starts the sine wave animation.</summary>
    public void TriggerWave()
    {
        _isPlaying = true;
    }

    /// <summary>Stops the wave and resets everything to the rest state.</summary>
    public void StopWave()
    {
        _isPlaying = false;

        if (_segments == null) return;

        if (waveType == ArcWaveType.FluidWave)
        {
            if (_lineRenderer != null && _basePositions != null)
                _lineRenderer.SetPositions(_basePositions);
            return;
        }

        for (int i = 0; i < _segments.Length; i++)
        {
            if (_segments[i] != null)
            {
                _segments[i].localPosition = _basePositions[i];
                _segments[i].localRotation = _baseRotations[i];
            }
        }
    }

    /// <summary>Pauses the wave without resetting.</summary>
    public void PauseWave() => _isPlaying = false;

    /// <summary>Resumes the wave from its current state.</summary>
    public void ResumeWave() => _isPlaying = true;

    // ── Cache ─────────────────────────────────────────────────────────────────

    private void CacheSegments()
    {
        int count = transform.childCount;
        _segments              = new Transform[count];
        _baseRotations         = new Quaternion[count];
        _basePositions         = new Vector3[count];
        _relativeBaseRotations = new Quaternion[count];
        _localBoneVectors      = new Vector3[count];
        _boneLengths           = new float[count];
        _baseChainDirs         = new Vector3[count];
        _segBaseRights         = new Vector3[count];
        _fluidPoints           = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            _segments[i]      = transform.GetChild(i);
            _baseRotations[i] = _segments[i].localRotation;
            _basePositions[i] = _segments[i].localPosition;
            _segBaseRights[i] = _baseRotations[i] * Vector3.right;
            _fluidPoints[i]   = _basePositions[i];
        }

        for (int i = 0; i < count; i++)
        {
            _relativeBaseRotations[i] = (i == 0)
                ? _baseRotations[0]
                : Quaternion.Inverse(_baseRotations[i - 1]) * _baseRotations[i];

            if (i < count - 1)
            {
                Vector3 boneInParent = _basePositions[i + 1] - _basePositions[i];
                _localBoneVectors[i] = Quaternion.Inverse(_baseRotations[i]) * boneInParent;
                _boneLengths[i]      = boneInParent.magnitude;
                _baseChainDirs[i]    = boneInParent.normalized;
            }
            else
            {
                _localBoneVectors[i] = count > 1 ? _localBoneVectors[i - 1] : Vector3.forward;
                _boneLengths[i]      = count > 1 ? _boneLengths[i - 1] : 1f;
                _baseChainDirs[i]    = count > 1 ? _baseChainDirs[i - 1] : Vector3.up;
            }
        }

        if (waveType == ArcWaveType.FluidWave)
            SetupLineRenderer();
        else
        {
            // Disable any pre-existing LineRenderer so it doesn't render on RotationWave/VerticalWave sprouts.
            LineRenderer lr = GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = false;
        }
    }

    private void SetupLineRenderer()
    {
        // Hide segment mesh renderers — the LineRenderer replaces them visually.
        foreach (Transform seg in _segments)
        {
            MeshRenderer mr = seg.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
        }

        // Use a pre-existing LineRenderer (added in the prefab) or create one at runtime.
        if (_lineRenderer == null)
            _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
            _lineRenderer = gameObject.AddComponent<LineRenderer>();

        _lineRenderer.enabled = true;

        // Auto-derive width from the segment's world-space diameter.
        // Segments use a Unity capsule mesh whose model-space radius = 0.5,
        // so world diameter = lossyScale.x (= lossyScale.x * 2 * 0.5).
        float autoBase = _segments.Length > 0 ? _segments[0].lossyScale.x : 0.2f;
        float autoTip  = _segments.Length > 0 ? _segments[_segments.Length - 1].lossyScale.x : 0.2f;
        float baseW = lineWidthBase > 0f ? lineWidthBase : autoBase;
        float tipW  = lineWidthTip  > 0f ? lineWidthTip  : autoTip;

        _lineRenderer.useWorldSpace     = false;
        _lineRenderer.positionCount     = _segments.Length;
        _lineRenderer.numCornerVertices = 5;
        _lineRenderer.numCapVertices    = 5;
        _lineRenderer.widthCurve = new AnimationCurve(
            new Keyframe(0f, baseW),
            new Keyframe(1f, tipW));

        if (lineMaterial != null)
            _lineRenderer.material = lineMaterial;

        _lineRenderer.SetPositions(_basePositions);
    }
}

