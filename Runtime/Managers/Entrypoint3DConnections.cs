using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Entrypoint3DConnections : MonoBehaviour
{
    [SerializeField] private bool disableSceneOnOverlay = true;
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private Scene sceneToLoad;
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
    /// Enable/Disable root objects of all gameObjects in the scene except this one
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
        if (ScriptableObjectInventory.Instance.toggleOverlayEvent)
            ScriptableObjectInventory.Instance.toggleOverlayEvent.OnEventTriggered += HandleEvent;
        if (sceneToLoad != default)
        {
            var sceneReference = ScriptableObject.CreateInstance<SceneReference>();
            sceneReference.scene = sceneToLoad;
            ScriptableObjectInventory.Instance.overlay.overlayScene = sceneReference;
        }
        _sceneName = ScriptableObjectInventory.Instance.overlay.overlayScene.Name;
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
        if (ScriptableObjectInventory.Instance.toggleOverlayEvent)
            ScriptableObjectInventory.Instance.toggleOverlayEvent.OnEventTriggered -= HandleEvent;
    }

    private void HandleEvent()
    {
        if (disableSceneOnOverlay)
            ToggleRootObjectsInSceneWhileOverlay(!ScriptableObjectInventory.Instance.overlay.OverlayIsActive());
    }
}