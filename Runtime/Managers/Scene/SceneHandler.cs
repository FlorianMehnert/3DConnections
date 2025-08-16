namespace _3DConnections.Runtime.Managers.Scene
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using TMPro;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Manager for SceneSwitching and interacting with the second display
    /// </summary>
    public static class SceneHandler
    {
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
        /// Ensure clean loading of a scene after which an action will be executed
        /// </summary>
        /// <param name="sceneName">Name of the scene that will be loaded</param>
        /// <param name="onComplete">Action that will be executed on scene loading</param>
        /// <returns></returns>
        public static IEnumerator LoadSceneAndInvokeAfterCoroutine(string sceneName, Action onComplete)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            // Wait one extra frame so everything initializes
            yield return null;
            onComplete();
        }

        public static GameObject GetParentObject()
        {
            var parent = GameObject.Find("ParentNodesObject");
            if (parent && parent.activeInHierarchy) return parent;
            Debug.LogWarning("ParentNodesObject is null or inactive.");
            return null;

        }

        public static List<GameObject> GetNodesUsingTheNodegraphParentObject()
        {
            var parentObject = GetParentObject();

            if (!parentObject)
                return new List<GameObject>();
            return parentObject.transform.Cast<Transform>()
                .Select(child => child.gameObject)
                .ToList();
        }

        public static Camera GetCameraOfOverlayedScene()
        {
            var scene = GetOverlayedScene();
            if (scene == null || !scene.Value.IsValid()) return null;
            var rootObjects = scene.Value.GetRootGameObjects();
            var camera = rootObjects.Select(obj => obj.GetComponentInChildren<Camera>())
                .FirstOrDefault(overlayCamera => overlayCamera);
            if (!camera) Debug.Log("You are missing a camera in the overlay scene");
            return camera;
        }
    }
}