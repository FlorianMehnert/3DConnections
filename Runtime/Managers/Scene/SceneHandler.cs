using System;
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
                asyncLoad.allowSceneActivation = true; // Activate the scene
            }

            yield return null;
        }

        Debug.Log($"Scene {sceneName} loaded successfully.");
        onComplete?.Invoke();
    }

    public static GameObject GetParentObject()
    {
        return GameObject.Find("ParentNodesObject");
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