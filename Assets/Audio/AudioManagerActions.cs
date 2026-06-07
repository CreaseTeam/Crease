using UnityEngine;

namespace Crease.Audio
{
    /// <summary>
    /// Drop this on any GameObject to expose AudioManager calls as simple
    /// public methods that can be wired up in the Inspector via UnityEvents
    /// (Buttons, Animators, Timeline signals, etc.).
    /// </summary>
    public class AudioManagerActions : MonoBehaviour
    {
        // ───────── Inspector-friendly wrappers ─────────

        /// <summary>Play a 2D sound (heard equally everywhere).</summary>
        public void Play(string key)
        {
            AudioManager.Instance?.Play(key);
        }

        /// <summary>Play a 3D sound at this GameObject's position.</summary>
        public void PlayAtSelf(string key)
        {
            AudioManager.Instance?.PlayAtPosition(key, transform.position);
        }

        /// <summary>Play a 2D looping sound. Store the source to stop it later.</summary>
        public void PlayLooping(string key)
        {
            _loopingSource = AudioManager.Instance?.Play(key, new AudioManager.PlaySettings { Loop = true });
        }

        /// <summary>Play a 3D looping sound at this object's position.</summary>
        public void PlayLoopingAtSelf(string key)
        {
            _loopingSource = AudioManager.Instance?.PlayAtPosition(key, transform.position,
                new AudioManager.PlaySettings { Loop = true });
        }

        /// <summary>Stop the most recently started looping sound from this component.</summary>
        public void StopLooping()
        {
            AudioManager.Instance?.Stop(_loopingSource);
            _loopingSource = null;
        }

        // Keeps a reference to the last looping source so we can stop it
        private AudioSource _loopingSource;
    }
}
