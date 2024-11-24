using UnityEngine;
using UnityEngine.SceneManagement;

namespace Runtime
{
    public class SceneHandler : MonoBehaviour
    {
        private static bool IsSceneLoaded(string sceneName)
        {
            // Iterate through all loaded scenes
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                // Check if the scene name matches
                if (scene.name == sceneName && scene.isLoaded)
                {
                    return true;
                }
            }

            // Return false if no matching scene is found
            return false;
        }

        /// <summary>
        /// Retrieve the Overlayed Scene if already loaded 
        /// </summary>
        /// <returns></returns>
        internal static Scene? GetOverlayedScene()
        {
            const string sceneNameToCheck = "NewScene";
            if (IsSceneLoaded(sceneNameToCheck)) return null;
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

        /// <summary>
        /// Place a button in the GUI at the desired position that will load the predefined "NewScene" Scene in Additive Mode
        /// </summary>
        /// <param name="x">x coordinate on the screen</param>
        /// <param name="y">y coordinate on the screen</param>
        public static void Execute(int x=20, int y=30)
        {
            const string sceneNameToCheck = "NewScene";
            //Whereas pressing this Button loads the Additive Scene.
            if (!GUI.Button(new Rect(x, y, 150, 30), "Other Scene Additive")) return;
            if (!IsSceneLoaded(sceneNameToCheck))
            {
                // Load new Scene in overlapping mode (additive)
                SceneManager.LoadScene("NewScene", LoadSceneMode.Additive);
            }
        }
    }
}