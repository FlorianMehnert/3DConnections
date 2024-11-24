using Runtime;
using UnityEngine;
using SFB;

public class GUIBuilder : MonoBehaviour
{
    private void OnGUI()
    {
        SceneHandler.Execute(20, 30);
        if (!GUI.Button(new Rect(20, 60, 150, 30), "Open File")) return;
        var path = StandaloneFileBrowser.OpenFolderPanel("Open File", "/home/florian/Bilder", false);
        Debug.Log(path[0]);
    }
}