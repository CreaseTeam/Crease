using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Scenes.Blockouts.TianPlayground
{
    public class CarouselRotation : MonoBehaviour
    {
        [Header("旋转设置")]
        [Tooltip("旋转速度（度/秒）。设置正数即可。")]
        [FormerlySerializedAs("rotationSpeed")]
        public float RotationSpeed = 30f;

        [Tooltip("勾选时顺时针旋转，取消勾选时逆时针旋转。")]
        [FormerlySerializedAs("isClockwise")]
        public bool IsClockwise = true;

        [Tooltip("旋转的中心轴向，旋转木马通常绕Y轴旋转")]
        [FormerlySerializedAs("rotationAxis")]
        public Vector3 RotationAxis = Vector3.up;

        private void Update()
        {
            float direction = IsClockwise ? 1f : -1f;
            transform.Rotate(RotationAxis * RotationSpeed * direction * Time.deltaTime);
        }
    }
}
