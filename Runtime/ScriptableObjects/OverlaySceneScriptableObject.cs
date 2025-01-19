using System.Linq;
using UnityEngine;

namespace _3DConnections.Runtime.ScriptableObjects
{
    /// <summary>
    /// Kind of Singleton reference to the overlay scene - almost a SceneHandler
    /// </summary>
    [CreateAssetMenu(fileName = "OverlaySceneData", menuName = "3DConnections/ScriptableObjects/OverlaySceneData", order = 1)]
    public class OverlaySceneScriptableObject : ScriptableObject
    {
        public SceneReference overlayScene;
        public Camera camera;

        public Camera GetCameraOfScene()
        {
            if (overlayScene)
            {
                if (camera) return camera;
                var rootObjects = overlayScene.scene.GetRootGameObjects();
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
            if (overlayScene is null || !overlayScene.scene.isLoaded) return;
            var overlayCamera = GetCameraOfScene();
            overlayCamera.enabled = !overlayCamera.enabled;
        }

        public GameObject GetNodeGraph()
        {
            if (overlayScene && overlayScene.scene.IsValid())
            {
                var rootObjects = overlayScene.scene.GetRootGameObjects();
                return rootObjects.FirstOrDefault(obj => obj.name == "node_graph");
            }

            Debug.Log("overlay scene is not properly configured");
            return null;
        }
    }
}