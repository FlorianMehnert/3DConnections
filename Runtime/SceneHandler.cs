using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Runtime
{
    public class SceneHandler : MonoBehaviour
    {
        public Camera mainCamera;
        public Camera overlayCamera { get; set; }
        public Scene overlayScene { get; set; }

        private static bool IsSceneLoaded(string sceneName)
        {
            // Iterate through all loaded scenes
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                // Check if the scene name matches
                if (scene.name != sceneName || !scene.isLoaded) continue;
                return true;
            }

            // Return false if no matching scene is found
            return false;
        }

        /// <summary>
        /// Retrieve the scene in which the new nodes will be spawned
        /// </summary>
        /// <returns></returns>
        internal static Scene? GetOverlayedScene()
        {
            const string sceneNameToCheck = "NewScene";
            if (!IsSceneLoaded(sceneNameToCheck)) return null;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                
                // Check if the scene name matches
                if (scene is { name: sceneNameToCheck, isLoaded: true })
                {
                    return scene;
                }
            }
            return null;
        }

        public void ConfigureCamera()
        {
            
        }

        /// <summary>
        /// Iterating through all cameras and returns the first one rendering to the second display (display 1 since starting with 0)
        /// </summary>
        /// <param name="display">The display to which the found camera should render to where 0 is display 1 and 1 is display 2</param>
        /// <returns>The first camera rendering to display 2</returns>
        private Camera GetOverlayCamera(int display)
        {
            return Camera.allCameras.FirstOrDefault(cam => cam.targetDisplay == display); 
        }

        /// <summary>
        /// Place a button in the GUI at the desired position that will load the predefined "NewScene" Scene in Additive Mode
        /// </summary>
        /// <param name="x">x coordinate on the screen</param>
        /// <param name="y">y coordinate on the screen</param>
        public void Execute(int x=20, int y=30)
        {
            // 1. load scene if not already loaded additively
            // 2. set camera to render correct stuffs
            
            const string sceneNameToCheck = "NewScene";
            
            // ACTION_REQUIRED: Add OverlayScene layer to project
            const string layerOverlay = "OverlayScene";
            if (!GUI.Button(new Rect(x, y, 150, 30), "Other Scene Additive")) return;
            if (!IsSceneLoaded(sceneNameToCheck))
            {
                // Load new Scene in overlapping mode (additive)
                SceneManager.LoadScene("NewScene", LoadSceneMode.Additive);
            }

            var scene = GetOverlayedScene();
            if (scene != null)
            {
                overlayScene = (Scene)scene;
            }
            else
            {
                Debug.LogError("GetOverlayedScene failed to get a scene in SceneHandler");
            }

            var overlayLayerMask = LayerMask.NameToLayer(layerOverlay);
            
            // find a camera that is rendering to the second display (display 1) in a multi display else set to null
            mainCamera = GetOverlayCamera(0);
            overlayCamera = GetOverlayCamera(1);
            
        }
    }
}