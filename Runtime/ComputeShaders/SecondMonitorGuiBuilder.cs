using _3DConnections.Runtime;
using _3DConnections.Runtime.Managers;
using Runtime;
using Runtime.ComputeShaders;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SecondMonitorGuiBuilder : MonoBehaviour
{
    [SerializeField]
    private GameObject nodePrefab;
    private NodeSpawner _nodeSpawner;

    private void Start()
    {
        _nodeSpawner = FindFirstObjectByType<NodeSpawner>(FindObjectsInactive.Exclude);
        _nodeSpawner.cubePrefab = nodePrefab;
    }
    private void OnGUI()
    {
        var overlayedScene = SceneHandler.GetOverlayedScene();
        if (overlayedScene != null) SceneManager.SetActiveScene((Scene)overlayedScene);
    }
}