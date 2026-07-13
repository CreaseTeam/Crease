using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Crease.Flying.Environment.BlockoutHelpers
{
    public class CriteriaTriggerHandler : MonoBehaviour
    {
        [FormerlySerializedAs("initialValue")]
        public float InitialValue = 50;
        [FormerlySerializedAs("upperThreshold")]
        public float UpperThreshold = 100;
        [FormerlySerializedAs("lowerThreshold")]
        public float LowerThreshold = 25;
        
        private float _progress;
        private bool _lowerThresholdReached = false;
        private bool _upperThresholdReached = false;
        
        [FormerlySerializedAs("onUpperThreshold")]
        public UnityEvent OnUpperThreshold;
        [FormerlySerializedAs("onLowerThreshold")]
        public UnityEvent OnLowerThreshold;

        private void Start()
        {
            _progress = InitialValue;
        }

        public void SetProgressValue(float value)
        {
            if (!_upperThresholdReached && !_lowerThresholdReached)
            {
                _progress = value;
                CheckThresholds();
            }
        }

        public void AddToProgress(float value)
        {
            if (!_upperThresholdReached && !_lowerThresholdReached)
            {
                _progress += value;
                CheckThresholds();
            }
        }

        private void CheckThresholds()
        {
            Debug.Log($"Progress: {_progress}");
            if (_progress >= UpperThreshold && !_upperThresholdReached)
            {
                OnUpperThreshold.Invoke();
                _upperThresholdReached = true;
                Debug.Log("Upper threshold reached!");
            }

            if (_progress <= LowerThreshold && !_lowerThresholdReached) 
            {
                OnLowerThreshold.Invoke();
                _lowerThresholdReached = true;
                Debug.Log("Lower threshold reached!");
            }
        }
    }
}