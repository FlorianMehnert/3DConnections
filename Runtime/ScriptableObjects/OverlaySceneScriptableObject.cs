using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Kind of Singleton reference to the overlay scene - almost a SceneHandler
/// </summary>
[CreateAssetMenu(fileName = "OverlaySceneData", menuName = "3DConnections/ScriptableObjects/OverlaySceneData",
    order = 1)]
public class OverlaySceneScriptableObject : ScriptableObject
{
    public SceneReference overlayScene;
    public Camera camera;
    [SerializeField] private GameObject parentNodeObject;

    public Camera GetCameraOfScene()
    {
        if (overlayScene)
        {
            if (camera) return camera;
            var scene = overlayScene.scene;
            var rootObjects = scene.HasValue ? scene.Value.GetRootGameObjects() : Array.Empty<GameObject>();
            camera = rootObjects.Select(obj => obj.GetComponentInChildren<Camera>())
                .FirstOrDefault(overlayCamera => overlayCamera);
            if (!camera) Debug.Log("You are missing a camera in the overlay scene");
            return camera;
        }

        Debug.Log("Overlay scene is not properly configured");
        return null;
    }

    public bool OverlayIsActive()
    {
        return overlayScene && camera.enabled;
    }

    public void ToggleOverlay()
    {
        if (overlayScene?.scene is not { isLoaded: true }) return;
        var overlayCamera = GetCameraOfScene();
        overlayCamera.enabled = !overlayCamera.enabled;
    }

    public GameObject GetNodeGraph()
    {
        if (overlayScene && overlayScene.scene.HasValue)
        {
            if (parentNodeObject != null)
                return parentNodeObject;
            var rootObjects = overlayScene.scene.Value.GetRootGameObjects();
            return rootObjects.FirstOrDefault(obj => obj.name == "ParentNodesObject");
        }

        Debug.Log("overlay scene is not properly configured");
        return null;
    }
}