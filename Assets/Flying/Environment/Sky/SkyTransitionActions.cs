using UnityEngine;

namespace Crease.Flying.Environment
{
    /// <summary>
    /// Inspector-friendly wrappers that delegate to SkyTransition.Instance.
    /// Wire these onto Button OnClick events, Timeline signals, etc.
    /// </summary>
    public class SkyTransitionActions : MonoBehaviour
    {
        public void TransitionToDay()
        {
            if (SkyTransition.Instance == null)
            {
                Debug.LogError("SkyTransitionActions: No SkyTransition instance.");
                return;
            }

            SkyTransition.Instance.TransitionToDay();
        }

        public void TransitionToNight()
        {
            if (SkyTransition.Instance == null)
            {
                Debug.LogError("SkyTransitionActions: No SkyTransition instance.");
                return;
            }

            SkyTransition.Instance.TransitionToNight();
        }

        public void Toggle()
        {
            if (SkyTransition.Instance == null)
            {
                Debug.LogError("SkyTransitionActions: No SkyTransition instance.");
                return;
            }

            SkyTransition.Instance.Toggle();
        }

        public void Stop()
        {
            if (SkyTransition.Instance == null)
            {
                Debug.LogError("SkyTransitionActions: No SkyTransition instance.");
                return;
            }

            SkyTransition.Instance.Stop();
        }
    }
}
