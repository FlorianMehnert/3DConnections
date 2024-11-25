using Runtime;
using UnityEngine;
using SFB;

public class GUIBuilder : MonoBehaviour
{
    private NodeBuilder _nodeBuilder;

    private void Start()
    {
        _nodeBuilder = gameObject.AddComponent<NodeBuilder>();
    }
    private void OnGUI()
    {
        SceneHandler.Execute(20, 30);
        if (GUI.Button(new Rect(20, 60, 150, 30), "Open File"))
        {
            var path = StandaloneFileBrowser.OpenFolderPanel("Open File", "/home/florian/Bilder", false);
            Debug.Log(path[0]);
        }
        _nodeBuilder.Execute(20,90);
    }
}