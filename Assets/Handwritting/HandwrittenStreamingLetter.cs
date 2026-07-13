using System.Collections;
using UnityEngine;

namespace Crease.Handwritting
{
    public class HandwrittenStreamingLetter : MonoBehaviour
    {
        Transform _target;
        float _duration;
        Vector3 _startPosition;
        Vector3 _startLossyScale;
        Material _materialInstance;

        public void Begin(Transform target, float duration, Material materialInstance, Vector3 startLossyScale)
        {
            _target = target;
            _duration = Mathf.Max(0.01f, duration);
            _materialInstance = materialInstance;
            _startLossyScale = startLossyScale;
            _startPosition = transform.position;
            SetLossyScale(_startLossyScale);
            StartCoroutine(FlyRoutine());
        }

        void OnDestroy()
        {
            if (_materialInstance != null)
                Destroy(_materialInstance);
        }

        IEnumerator FlyRoutine()
        {
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);
                float eased = t * t;

                if (_target != null)
                    transform.position = Vector3.Lerp(_startPosition, _target.position, eased);

                SetLossyScale(Vector3.Lerp(_startLossyScale, Vector3.zero, eased));
                yield return null;
            }

            Destroy(gameObject);
        }

        void SetLossyScale(Vector3 desiredLossyScale)
        {
            Transform parent = transform.parent;
            if (parent == null)
            {
                transform.localScale = desiredLossyScale;
                return;
            }

            Vector3 parentLossyScale = parent.lossyScale;
            transform.localScale = new Vector3(
                DivideScale(desiredLossyScale.x, parentLossyScale.x),
                DivideScale(desiredLossyScale.y, parentLossyScale.y),
                DivideScale(desiredLossyScale.z, parentLossyScale.z));
        }

        static float DivideScale(float desired, float parent)
        {
            if (Mathf.Abs(parent) < 0.0001f)
                return desired;

            return desired / parent;
        }
    }
}
