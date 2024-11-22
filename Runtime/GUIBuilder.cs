using com.florian_mehnert._3d_connections.Editor;
using UnityEngine;

namespace com.florian_mehnert._3d_connections.Runtime
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
        private void Start()
        {
            // Create an empty GameObject
            GameObject empty = new GameObject("EmptyObject");

            // Add the script from the package as a component
            MyComponent component = empty.AddComponent<MyComponent>();

            // Optionally, call a method on the added component
            component.SayHello();
        }
    }
}