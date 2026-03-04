using UnityEngine;

public class WingTrailController : MonoBehaviour
{
    [Header("Wing Tip Transforms")]
    public Transform wingTipLeft;
    public Transform wingTipRight;

    [Header("Trail Settings")]
    public float trailTime = 0.5f;
    public float startWidth = 0.3f;
    public float endWidth = 0f;
    public Color startColor = new Color(1f, 1f, 1f, 0.8f);
    public Color endColor = new Color(1f, 1f, 1f, 0f);
    public Material trailMaterial;

    private TrailRenderer trailLeft;
    private TrailRenderer trailRight;

    void Start()
    {
        trailLeft = CreateTrail(wingTipLeft);
        trailRight = CreateTrail(wingTipRight);
    }

    TrailRenderer CreateTrail(Transform wingTip)
    {
        GameObject trailObj = new GameObject("WingTrail");
        trailObj.transform.SetParent(wingTip);
        trailObj.transform.localPosition = Vector3.zero;

        TrailRenderer trail = trailObj.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = startWidth;
        trail.endWidth = endWidth;
        trail.material = trailMaterial != null ? trailMaterial
            : new Material(Shader.Find("Sprites/Default"));

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;

        return trail;
    }

    // customizable inputs 
    public void SetTrailTime(float time)
    {
        trailTime = time;
        if (trailLeft) trailLeft.time = time;
        if (trailRight) trailRight.time = time;
    }

    public void SetTrailEnabled(bool enabled)
    {
        if (trailLeft) trailLeft.emitting = enabled;
        if (trailRight) trailRight.emitting = enabled;
    }
}