using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

    /// <summary>
    /// Manager for SceneSwitching and interacting with the second display
    /// </summary>
    public class SceneHandler : MonoBehaviour
    {
        private readonly Camera _overlayCamera;
        private Camera _mainCamera;

        private TMP_Dropdown _sceneDropdown;
        private SceneManager _sceneManager;
        [SerializeField] public Scene analyzeScene;


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

        private static IEnumerator<AsyncOperation> LoadSceneCoroutine(string sceneName, Action onComplete)
        {
            var asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    
            if (asyncLoad == null)
            {
                Debug.LogError($"Failed to start loading scene: {sceneName}");
                yield break;
            }

            while (!asyncLoad.isDone)
            {
                if (asyncLoad.progress >= 0.9f)
                {
                    asyncLoad.allowSceneActivation = true;  // Activate the scene
                }
        
                yield return null;
            }
            
            Debug.Log($"Scene {sceneName} loaded successfully.");
            onComplete?.Invoke();
        }
        public void LoadSceneWithCallback(string sceneName, Action onComplete)
        {
            StartCoroutine(LoadSceneCoroutine(sceneName, onComplete));
        }

        public static GameObject GetParentObject()
        {
            return GameObject.Find("ParentNodesObject");
        }

        public static List<GameObject> GetNodesByTransform()
        {
            var parentObject = GetParentObject();
            
            if (parentObject == null)
                return new List<GameObject>();
            Debug.Log("parent object is:"  + parentObject);
            return parentObject.transform.Cast<Transform>()
                .Select(child => child.gameObject)
                .ToList();
        }
    }
