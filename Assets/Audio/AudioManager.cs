using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Audio
{
    /// <summary>
    /// Extremely simple singleton AudioManager.
    /// Assign clips + key names in the Inspector, then call
    /// AudioManager.Instance.Play("key") from anywhere.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ───────── Singleton ─────────
        public static AudioManager Instance { get; private set; }

        // ───────── Inspector data ─────────
        [Serializable]
        public class SoundEntry
        {
            [FormerlySerializedAs("key")]
            public string Key;
            [FormerlySerializedAs("clip")]
            public AudioClip Clip;
            [Range(0f, 1f)]
            [FormerlySerializedAs("defaultVolume")]
            public float DefaultVolume = 1f;

            [Header("3D Spatial (only used with PlayAtPosition)")]
            [FormerlySerializedAs("minDistance")]
            public float MinDistance = 1f;
            [FormerlySerializedAs("maxDistance")]
            public float MaxDistance = 500f;
        }

        [SerializeField]
        [FormerlySerializedAs("sounds")]
        private List<SoundEntry> _sounds = new List<SoundEntry>();

        // Runtime lookup built from the list above
        private Dictionary<string, SoundEntry> _lookup;
        // Scene-local parent for spawned one-shot AudioSources. This GameObject is
        // intentionally NOT marked DontDestroyOnLoad and is not a child of the manager.
        private Transform _spawnParent;

        // ───────── Optional settings passed to Play ─────────
        /// <summary>
        /// Optional bag of overrides you can pass when playing a sound.
        /// Every field has a sensible default so you only set what you need.
        /// </summary>
        public class PlaySettings
        {
            public float Volume = 1f;   // multiplied with the entry's DefaultVolume
            public float Pitch = 1f;
            public bool Loop = false;
            public float? MinDistance = null;  // null = use the entry's Inspector default
            public float? MaxDistance = null;  // null = use the entry's Inspector default
        }

        // ───────── Unity lifecycle ─────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null); // detach from any parent to avoid unintended destruction
            DontDestroyOnLoad(gameObject);

            // Build the dictionary
            _lookup = new Dictionary<string, SoundEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _sounds)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Clip == null) continue;
                _lookup[entry.Key] = entry;
            }
        }

        // ───────── Public API ─────────

        /// <summary>
        /// Play a sound as a 2-D oneshot (heard equally everywhere, no spatial falloff).
        /// Returns the spawned AudioSource so the caller can stop a looping sound later.
        /// </summary>
        public AudioSource Play(string key, PlaySettings settings = null)
        {
            return PlayInternal(key, Vector3.zero, spatialBlend: 0f, settings);
        }

        /// <summary>
        /// Play a sound at a world position with full 3-D spatial audio.
        /// Returns the spawned AudioSource so the caller can stop a looping sound later.
        /// </summary>
        public AudioSource PlayAtPosition(string key, Vector3 position, PlaySettings settings = null)
        {
            return PlayInternal(key, position, spatialBlend: 1f, settings);
        }

        /// <summary>
        /// Convenience overload: stop a looping sound that was previously started.
        /// </summary>
        public void Stop(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            Destroy(source.gameObject);
        }

        // ───────── Internals ─────────
        private AudioSource PlayInternal(string key, Vector3 position, float spatialBlend, PlaySettings settings)
        {
            if (!_lookup.TryGetValue(key, out var entry))
            {
                Debug.LogWarning($"[AudioManager] No sound registered with key \"{key}\".");
                return null;
            }

            settings ??= new PlaySettings();

            // Spawn a temporary GameObject with an AudioSource
            var go = new GameObject($"OneShot_{key}");
            go.transform.position = position;

            // If this spawned object is not parented, attach it under a single
            // designated scene-local parent. Create the parent if it doesn't exist.
            if (go.transform.parent == null)
            {
                if (_spawnParent == null || _spawnParent.gameObject == null)
                {
                    var parentGo = GameObject.Find("AudioManager_Sources");
                    if (parentGo == null)
                    {
                        parentGo = new GameObject("AudioManager_Sources");
                        parentGo.transform.SetParent(null); // ensure it's at root
                    }
                    else
                    {
                        parentGo.transform.SetParent(null); // ensure not childed to manager
                    }

                    // Do NOT call DontDestroyOnLoad on this parent; it should be
                    // scene-local and destroyed during scene unload.
                    _spawnParent = parentGo.transform;
                }

                go.transform.SetParent(_spawnParent, true);
            }

            var src = go.AddComponent<AudioSource>();
            src.clip = entry.Clip;
            src.volume = entry.DefaultVolume * settings.Volume;
            src.pitch = settings.Pitch;
            src.spatialBlend = spatialBlend;
            src.minDistance = settings.MinDistance ?? entry.MinDistance;
            src.maxDistance = settings.MaxDistance ?? entry.MaxDistance;
            src.loop = settings.Loop;
            src.Play();

            // Auto-destroy non-looping sounds when they finish
            if (!settings.Loop)
                Destroy(go, entry.Clip.length / Mathf.Abs(settings.Pitch));

            return src;
        }
    }
}
