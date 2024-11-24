using com.florian_mehnert._3d_connections.Editor;
using UnityEngine;

namespace Runtime
{
    public class GUIBuilder : MonoBehaviour
    {
        // public void OpenFileChooser()
        // {
        //     string[] paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", "", false);
        //
        //     if (paths.Length > 0)
        //     {
        //         string selectedFile = paths[0];
        //         Debug.Log($"Selected File: {selectedFile}");
        //     }
        // }
        private void OnGUI()
        { 
            SceneHandler.Execute(20,30);
            // ReferenceFinder.Execute(20,60);
        }
    }
}