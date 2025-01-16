using System;
using System.Linq;
using _3DConnections.Runtime.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _3DConnections.Runtime.Managers
{
    /// <summary>
    /// Manager for SceneSwitching and interacting with the second display
    /// </summary>
    public class SceneHandler : MonoBehaviour
    {
        private readonly Camera _overlayCamera;
        private Camera _mainCamera;

        private TMP_Dropdown _sceneDropdown;
        private SceneManager _sceneManager;
        [SerializeField] private ToAnalyzeSceneScriptableObject analyzeSceneConfig;
        [SerializeField] private OverlaySceneScriptableObject overlay;


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

        /// <summary>
        /// Retrieve the scene in which the new nodes will be spawned
        /// </summary>
        /// <returns></returns>
        public static Scene? GetOverlayedScene()
        {
            const string sceneNameToCheck = "OverlayScene";
            if (!IsSceneLoaded(sceneNameToCheck)) return null;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                // Check if the scene name matches
                if (scene is { name: sceneNameToCheck })
                {
                    return scene;
                }
            }

            return null;
        }


        /// <summary>
        /// Required for the tree spanning where all root transforms form the basis of the tree
        /// </summary>
        /// <returns></returns>
        public static Transform[] GetSceneRootObjects()
        {
            var scene = GetOverlayedScene();
            if (scene != null)
            {
                return ((Scene)scene).GetRootGameObjects()
                    .Select(go => go.transform)
                    .ToArray();
            }

            return Array.Empty<Transform>();
        }
    }
}