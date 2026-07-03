using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Scenes.Blockouts.TianPlayground
{
    public class SwingMotion : MonoBehaviour
    {
        [Header("摆动设置")]
        [Tooltip("单侧最大摆动角度。例如设为60，秋千会在 +60度 到 -60度 之间来回摆动。")]
        [FormerlySerializedAs("maxAngle")]
        public float MaxAngle = 60f;

        [Tooltip("摆动速度。数值越大，摆动越快。")]
        [FormerlySerializedAs("swingSpeed")]
        public float SwingSpeed = 2f;

        [Tooltip("摆动轴向。默认 (1, 0, 0) 为沿X轴摆动。如果想改Z轴，设为 (0, 0, 1)。")]
        [FormerlySerializedAs("swingAxis")]
        public Vector3 SwingAxis = Vector3.right;

        private Quaternion _startRotation;

        private void Start()
        {
            _startRotation = transform.localRotation;
        }

        private void Update()
        {
            float currentAngle = MaxAngle * Mathf.Sin(Time.time * SwingSpeed);
            Quaternion swingRotation = Quaternion.AngleAxis(currentAngle, SwingAxis.normalized);
            transform.localRotation = _startRotation * swingRotation;
        }
    }
}
