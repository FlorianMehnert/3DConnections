using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Runtime
{
    public class SceneHandler : MonoBehaviour
    {
        public Camera mainCamera;
        private Camera OverlayCamera { get; set; }
        public Scene overlayScene;
        private const string LayerOverlay = "OverlayScene";

        private static bool IsSceneLoaded(string sceneName)
        {
            // Iterate through all loaded scenes
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                // Check if the scene name matches
                if (scene.name != sceneName) continue;
                return true;
            }

            // Return false if no matching scene is found
            return false;
        }

        private void LoadSceneAndWait(string sceneName)
        {
            StartCoroutine(LoadSceneCoroutine(sceneName));
        }

        private IEnumerator LoadSceneCoroutine(string sceneName)
        {
            // Start loading the scene asynchronously
            var asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            // Wait until the scene is fully loaded
            while (!asyncLoad.isDone)
            {
                // Optionally, you can use asyncLoad.progress to display loading progress (0.0 to 0.9).
                yield return null;
            }

            // Scene is fully loaded
            OnSceneLoaded();
        }

        private void OnSceneLoaded()
        {
            Debug.Log("Scene fully loaded!");
            Debug.Log("the overlayed scene is: " + overlayScene.name);

            var scene = GetOverlayedScene();
            Debug.Log("the overlayed scene is: " + overlayScene.name);
            Debug.Log(scene);
            if (scene != null)
            {
                overlayScene = (Scene)scene;
            }
            else
            {
                Debug.LogWarning("GetOverlayedScene failed to get a scene in SceneHandler");
            }

            var overlayLayerMask = LayerMask.NameToLayer(LayerOverlay);

            // find a camera rendering to the second display (display 1) in a multi display else set to null
            mainCamera = GetOverlayCamera(0);
            OverlayCamera = GetOverlayCamera(1);
            
            // disable culling Mask for the main camera and enable for overlay camera
            if (mainCamera)
            {
                mainCamera.cullingMask &= ~(1 << overlayLayerMask);
            }
            else
            {
                Debug.LogWarning("MainCamera not available");
            }

            if (OverlayCamera)
            {
                OverlayCamera.cullingMask = 1 << overlayLayerMask;
            }
            else
            {
                Debug.LogWarning("OverlayCamera not available");
            }
        }

        /// <summary>
        /// Retrieve the scene in which the new nodes will be spawned
        /// </summary>
        /// <returns></returns>
        internal static Scene? GetOverlayedScene()
        {
            const string sceneNameToCheck = "NewScene";
            if (!IsSceneLoaded(sceneNameToCheck)) return null;
            Debug.Log("after not loaded");
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                Debug.Log("scene name: " + scene.name);

                // Check if the scene name matches
                if (scene is { name: sceneNameToCheck })
                {
                    return scene;
                }
            }

            return null;
        }

        /// <summary>
        /// Iterating through all cameras and returns the first one rendering to the second display (display 1 since starting with 0)
        /// </summary>
        /// <param name="display">The display to which the found camera should render to where 0 is display 1 and 1 is display 2</param>
        /// <returns>The first camera rendering to display 2</returns>
        public static Camera GetOverlayCamera(int display)
        {
            return Camera.allCameras.FirstOrDefault(cam => cam.targetDisplay == display);
        }

        /// <summary>
        /// Place a button in the GUI at the desired position that will load the predefined "NewScene" Scene in Additive Mode
        /// </summary>
        /// <param name="x">x coordinate on the screen</param>
        /// <param name="y">y coordinate on the screen</param>
        public void Execute(int x = 20, int y = 30)
        {
            // 1. load scene if not already loaded additively
            // 2. set the camera to render correct stuffs

            const string sceneNameToCheck = "NewScene";

            // ACTION_REQUIRED: Add OverlayScene layer to project
            if (!GUI.Button(new Rect(x, y, 150, 30), "Other Scene Additive")) return;
            if (!IsSceneLoaded(sceneNameToCheck))
            {
                // Load new Scene in overlapping mode (additive)
                LoadSceneAndWait("NewScene");
            }
        }
    }
}