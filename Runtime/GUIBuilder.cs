using Runtime;
using UnityEngine;
using SFB;

/// <summary>
/// Manager class responsible for the Layout of all Buttons in scene1/2
/// </summary>
public class GUIBuilder : MonoBehaviour
{
    private NodeBuilder _nodeBuilder;
    private SceneHandler _sceneHandler;
    public string[] path;

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
            path = StandaloneFileBrowser.OpenFolderPanel("Open File", "/home/florian/Bilder", false);
            Debug.Log(path[0]);
        }
        
        _nodeBuilder.Execute(20,90, path);
    }
}