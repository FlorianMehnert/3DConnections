using _3DConnections.Runtime.ScriptableObjects;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _3DConnections.Runtime.Managers
{
    public class Entrypoint3DConnections : MonoBehaviour
    {
        [SerializeField] private OverlaySceneScriptableObject overlay;

        private void Start()
        {
            Debug.Log(overlay.overlayScene.Name);
            Debug.Log(overlay.overlayScene.Path);
            Debug.Log(overlay.overlayScene.sceneName);
            Debug.Log(overlay.overlayScene.scenePath);
            SceneManager.LoadScene(sceneName:overlay.overlayScene.Name, mode:LoadSceneMode.Additive);
        }
    }
}