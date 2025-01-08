using System;
using System.Collections;
using System.Collections.Generic;
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
        private Camera _mainCamera;
        private Camera OverlayCamera { get; set; }
        private GameObject NodeGraph { get; set; }
        private const string LayerOverlay = "OverlayScene";
   
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
            var overlayLayerMask = LayerMask.NameToLayer(LayerOverlay);
            var uiLayer = LayerMask.NameToLayer("UI");

            // find a camera rendering to the second display (display 1) in a multi display else set to null
            _mainCamera = GetCameraOfSpecificDisplay(0);
            OverlayCamera = overlay.GetCameraOfScene();

            // disable culling Mask for the main camera and enable for overlay camera
            if (_mainCamera)
            {
                _mainCamera.cullingMask &= ~(1 << overlayLayerMask);
                if (uiLayer != -1) // Ensure the UI layer exists
                {
                    _mainCamera.cullingMask |= (1 << uiLayer);
                }
                
                // update dropdown on scene Load
                
                PopulateDropdown();
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

        private static Scene? GetOverlayedScene(string sceneName)
        {
            if (!IsSceneLoaded(sceneName)) return null;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                // Check if the scene name matches
                if (scene.name == sceneName)
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
        /// <returns>The first camera rendering to display 2. Might be null</returns>
        private static Camera GetCameraOfSpecificDisplay(int display)
        {
            return Camera.allCameras.FirstOrDefault(cam => cam.targetDisplay == display);
        }

        

        public static GameObject GetCanvas(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid()) return null;
            var rootObjects = scene.GetRootGameObjects();
            return rootObjects.FirstOrDefault(obj => obj.name == "Canvas");
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

        public static Transform[] GetSceneRootObjects(string sceneName)
        {
            var scene = GetOverlayedScene(sceneName);
            if (scene != null)
            {
                return ((Scene)scene).GetRootGameObjects()
                    .Select(go => go.transform)
                    .ToArray();
            }

            return Array.Empty<Transform>();
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
            
            PopulateDropdown();
        }

        private void CreateDropdown()
        {
            // Instantiate the dropdown prefab
            // GameObject dropdownObj = Instantiate(dropdownPrefab, canvasTransform);
            // _sceneDropdown = dropdownObj.GetComponent<TMP_Dropdown>();
        
            // Add listener for value changes
            _sceneDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        private void PopulateDropdown()
        {
            if (!_sceneDropdown)
            {
                CreateDropdown();
            }
            _sceneDropdown.ClearOptions();
            var sceneNames = new List<string>();

            // Get all loaded scenes
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    sceneNames.Add(scene.name);
                }
            }

            // Add options to dropdown
            _sceneDropdown.AddOptions(sceneNames);
        }

        private void OnDropdownValueChanged(int index)
        {
            var selectedScene = _sceneDropdown.options[index].text;
            var scene = SceneManager.GetSceneByName(selectedScene);
            
            var newSceneRef = ScriptableObject.CreateInstance<SceneReference>();
            newSceneRef.useStaticValues = false;
            newSceneRef.scene = scene;
            newSceneRef.sceneName = scene.name;
            newSceneRef.scenePath = scene.path;
            
            analyzeSceneConfig.reference = newSceneRef;
        }
    }
}