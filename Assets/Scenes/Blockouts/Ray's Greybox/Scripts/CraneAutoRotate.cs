using UnityEngine;

namespace Crease.Scenes.Blockouts.RaysGreybox
{
    public class CraneAutoRotate : MonoBehaviour
    {
        [SerializeField] private float _rotationSpeed = 1f;
        [SerializeField] private float _maxAngle = 45f;
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;
        [SerializeField] private int _direction = 1;

        private Quaternion _initialRotation;

        private void Start()
        {
            _initialRotation = transform.localRotation;
        }

        private void Update()
        {
            float angle = Mathf.PingPong(Time.time * _rotationSpeed, _maxAngle * 2f) - _maxAngle;

            transform.localRotation =
                _initialRotation *
                Quaternion.AngleAxis(angle * _direction, _rotationAxis.normalized);
        }
    }
}
