namespace _3DConnections.Runtime.Managers
{
    using Events;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    
    using ScriptableObjects;

    public class Entrypoint3DConnections : MonoBehaviour
    {
        
        [SerializeField] private OverlaySceneScriptableObject overlay;
        [SerializeField] private ToggleOverlayEvent overlayEvent;
        [SerializeField] private bool disableSceneOnOverlay = true;
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private UnityEngine.SceneManagement.Scene sceneToLoad;
        private string _sceneName;

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Return)) return;
            if (SceneManager.GetSceneByName(_sceneName).isLoaded) return;
            SceneManager.LoadScene(sceneName: _sceneName, mode: LoadSceneMode.Additive);
            if (disableSceneOnOverlay)
                ToggleRootObjectsInSceneWhileOverlay();
        }

        /// <summary>
        /// Enable/Disable root objects of all gameObjects in the scene except this one.
        /// Makes an exception for ui menus since they are handled using the GUIManger
        /// </summary>
        /// <param name="value">true for enable/false for disabling all gameObjects</param>
        private void ToggleRootObjectsInSceneWhileOverlay(bool value = false)
        {
            foreach (var go in gameObject.scene.GetRootGameObjects())
            {
                if (go == gameObject)
                {
                    continue;
                }

                go.SetActive(value);
            }
        }

        private void OnEnable()
        {
            if (!overlayEvent) return;
            overlayEvent.OnEventTriggered += HandleEvent;
            if (sceneToLoad != default)
            {
                var sceneReference = ScriptableObject.CreateInstance<SceneReference>();
                sceneReference.scene = sceneToLoad;
                overlay.overlayScene = sceneReference;
            }

            _sceneName = overlay.overlayScene.Name;
            if (SceneManager.GetSceneByName(_sceneName).isLoaded)
            {
                return;
            }

            if (!loadOnStart) return;
            SceneManager.LoadScene(sceneName: _sceneName, mode: LoadSceneMode.Additive);
            if (disableSceneOnOverlay)
                ToggleRootObjectsInSceneWhileOverlay();
        }

        private void OnDisable()
        {
            if (!overlayEvent) return;
            overlayEvent.OnEventTriggered -= HandleEvent;
        }

        private void HandleEvent()
        {
            if (disableSceneOnOverlay)
                ToggleRootObjectsInSceneWhileOverlay(!overlay.OverlayIsActive());
        }
    }
}