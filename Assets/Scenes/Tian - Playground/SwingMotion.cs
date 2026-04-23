using UnityEngine;

public class SwingMotion : MonoBehaviour
{
    [Header("摆动设置")]
    [Tooltip("单侧最大摆动角度。例如设为60，秋千会在 +60度 到 -60度 之间来回摆动。")]
    public float maxAngle = 60f;

    [Tooltip("摆动速度。数值越大，摆动越快。")]
    public float swingSpeed = 2f;

    [Tooltip("摆动轴向。默认 (1, 0, 0) 为沿X轴摆动。如果想改Z轴，设为 (0, 0, 1)。")]
    public Vector3 swingAxis = Vector3.right; // Vector3.right 等同于 (1, 0, 0)

    // 用于记录物体最开始的旋转状态
    private Quaternion startRotation;

    void Start()
    {
        // 游戏开始时，记下秋千的初始旋转角度，作为摆动的“中心基准点”
        startRotation = transform.localRotation;
    }

    void Update()
    {
        // 核心原理：Mathf.Sin 会根据时间生成一个 -1 到 1 之间平滑波动的数值
        // 将这个波动值乘以 maxAngle，就能得到当前帧应该处于的角度（比如 -60 到 60 之间）
        float currentAngle = maxAngle * Mathf.Sin(Time.time * swingSpeed);

        // 根据计算出的角度和设置的轴向，生成一个“摆动旋转量”
        Quaternion swingRotation = Quaternion.AngleAxis(currentAngle, swingAxis.normalized);

        // 将秋千的初始状态叠加当前的摆动旋转量，应用给秋千
        transform.localRotation = startRotation * swingRotation;
    }
}