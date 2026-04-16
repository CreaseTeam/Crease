using UnityEngine;

public class CarouselRotation : MonoBehaviour
{
    [Header("旋转设置")]
    [Tooltip("旋转速度（度/秒）。设置正数即可。")]
    public float rotationSpeed = 30f;

    [Tooltip("勾选时顺时针旋转，取消勾选时逆时针旋转。")]
    public bool isClockwise = true;

    [Tooltip("旋转的中心轴向，旋转木马通常绕Y轴旋转")]
    public Vector3 rotationAxis = Vector3.up;

    void Update()
    {
        // 根据是否勾选顺时针，决定方向系数（1代表正向，-1代表反向）
        float direction = isClockwise ? 1f : -1f;

        // 将方向系数乘进公式里
        transform.Rotate(rotationAxis * rotationSpeed * direction * Time.deltaTime);
    }
}