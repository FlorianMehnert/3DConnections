using UnityEngine;
using UnityEngine.SceneManagement;

public class Entrypoint3DConnections : MonoBehaviour
{
    [SerializeField] private OverlaySceneScriptableObject overlay;
    [SerializeField] private ToggleOverlayEvent overlayEvent;
    [SerializeField] private bool disableSceneOnOverlay = true;

    private void Start()
    {
        var sceneName = overlay.overlayScene.Name;
        if (SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            return;
        }

        SceneManager.LoadScene(sceneName: sceneName, mode: LoadSceneMode.Additive);
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