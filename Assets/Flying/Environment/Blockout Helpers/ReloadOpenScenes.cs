using Crease.Managers.Input;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

namespace Crease.Flying.Environment.BlockoutHelpers
{
    /// <summary>
    /// Utility script that reloads all open scenes.
    /// </summary>
    public class ReloadOpenScenes : MonoBehaviour
    {
        private static ReloadOpenScenes _instance;
        private static bool _isReloadingScenes;

        private void Awake()
        {
            if (_instance == null || _instance == this || !_instance)
            {
                _instance = this;
                _isReloadingScenes = false;
            }
            else if (_instance != this)
            {
                DestroyImmediate(this);
            }
        }

        private void Update()
        {
            if (_isReloadingScenes)
            {
                return;
            }

            if (InputManager.Instance.ResetTriggered)
            {
                _isReloadingScenes = true;
                StartCoroutine(ReloadAllOpenScenes());
            }
        }

        private IEnumerator ReloadAllOpenScenes()
        {
            Debug.Log("Reloading scenes...");

            bool playerWasEnabled = InputManager.Instance.Actions.Player.enabled;
            bool debugWasEnabled = InputManager.Instance.Actions.Debug.enabled;
            InputManager.Instance.Actions.Player.Disable();
            InputManager.Instance.Actions.Debug.Disable();

            string activeScenePath = SceneManager.GetActiveScene().path;
            List<string> nonActiveScenePaths = new();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; ++sceneIndex)
            {
                string scenePath = SceneManager.GetSceneAt(sceneIndex).path;
                if (scenePath != activeScenePath)
                {
                    nonActiveScenePaths.Add(scenePath);
                }
            }

            Debug.Log("Unloadng scenes...");
            foreach (string scenePath in nonActiveScenePaths)
            {
                Debug.Log($"\tUnloading scene {scenePath}...");
                yield return SceneManager.UnloadSceneAsync(scenePath);
            }

            Debug.Log("Loading scenes...");
            Debug.Log($"\tReloading active scene {activeScenePath}...");
            yield return SceneManager.LoadSceneAsync(activeScenePath, LoadSceneMode.Single);
            foreach (string scenePath in nonActiveScenePaths)
            {
                Debug.Log($"\tLoading scene {scenePath}...");
                yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            }

            Debug.Log("Finished reloading scenes!");

            if (playerWasEnabled)
                InputManager.Instance.Actions.Player.Enable();
            if (debugWasEnabled)
                InputManager.Instance.Actions.Debug.Enable();

            _isReloadingScenes = false;
        }
    }
}
