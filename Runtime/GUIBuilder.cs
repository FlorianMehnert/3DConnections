using Runtime;
using UnityEngine;
using SFB;

public class GUIBuilder : MonoBehaviour
{
    private NodeBuilder _nodeBuilder;
    private SceneHandler _sceneHandler;

    private void Start()
    {
        _nodeBuilder = gameObject.AddComponent<NodeBuilder>();
        _sceneHandler = gameObject.AddComponent<SceneHandler>();
    }
    private void OnGUI()
    {
        _sceneHandler.Execute(20, 30);
        if (GUI.Button(new Rect(20, 60, 150, 30), "Open File"))
        {
            var path = StandaloneFileBrowser.OpenFolderPanel("Open File", "/home/florian/Bilder", false);
            Debug.Log(path[0]);
        }
        _nodeBuilder.Execute(20,90);
    }
}