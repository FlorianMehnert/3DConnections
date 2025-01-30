using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Entrypoint3DConnections : MonoBehaviour
{
    [SerializeField] private OverlaySceneScriptableObject overlay;
    [SerializeField] private ToggleOverlayEvent overlayEvent;
    [SerializeField] private bool disableSceneOnOverlay = true;
    [SerializeField] private bool loadOnStart = true;
    private string _sceneName;

    private void Start()
    {
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
        if (overlayEvent != null)
            overlayEvent.OnEventTriggered += HandleEvent;
    }

    private void OnDisable()
    {
        if (overlayEvent != null)
            overlayEvent.OnEventTriggered -= HandleEvent;
    }

    private void HandleEvent()
    {
        if (disableSceneOnOverlay)
            ToggleRootObjectsInSceneWhileOverlay(!overlay.OverlayIsActive());
    }
}