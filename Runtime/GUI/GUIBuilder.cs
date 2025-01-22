using System.Collections.Generic;
using System.Linq;
using _3DConnections.Runtime.BurstPhysics;
using _3DConnections.Runtime.Managers;
using _3DConnections.Runtime.ScriptableObjects;
using _3DConnections.Runtime.Scripts;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _3DConnections.Runtime.GUI
{
    /// <summary>
    /// Manager class responsible for the Layout of all Buttons in scene1/2
    /// </summary>
    public class GUIBuilder : MonoBehaviour
    {
        private NodeBuilder _nodeBuilder;
        private SceneAnalyzer _sceneAnalyzer;
        public string[] path;
        [SerializeField] private TMP_Dropdown dropdownPrefab;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private Canvas uiCanvas;
        private TMP_Dropdown _dropdownInstance;
        [SerializeField] private ToAnalyzeSceneScriptableObject analyzeSceneConfig;
        [SerializeField] private OverlaySceneScriptableObject overlaySceneConfig;
        [SerializeField] private NodeGraphScriptableObject nodeGraph;

        private int _currentYCoordinate = 300;

        private void Start()
        {
            _nodeBuilder = GetComponent<NodeBuilder>();
            if (_nodeBuilder == null)
            {
                Debug.Log("The NodeBuilder component is missing on the manager");
            }

            _sceneAnalyzer = GetComponent<SceneAnalyzer>();
            if (_sceneAnalyzer == null)
            {
                Debug.Log("The SceneAnalyzer component is missing on the manager");
            }

            CreateSceneDropdown();
            CreateButtons();
        }

        private int NextYPosition()
        {
            var currentY = _currentYCoordinate;
            _currentYCoordinate -= 35;
            return currentY;
        }

        private Vector2 GetButtonPosition()
        {
            return new Vector2(300, NextYPosition());
        }

        private void CreateSceneDropdown()
        {
            // Instantiate the dropdown prefab as child of canvas
            var dropdownInstance = Instantiate(dropdownPrefab, uiCanvas.transform);
            _dropdownInstance = dropdownInstance;

            // Get the dropdown component (works with both standard and TMP dropdowns)
            var tmpDropdown = dropdownInstance.GetComponent<TMP_Dropdown>();
            var standardDropdown = dropdownInstance.GetComponent<Dropdown>();

            // Clear existing options
            if (tmpDropdown != null)
            {
                tmpDropdown.ClearOptions();
            }
            else if (standardDropdown != null)
            {
                standardDropdown.ClearOptions();
            }

            // Get all scenes in build settings
            var sceneCount = SceneManager.sceneCountInBuildSettings;
            var sceneOptions = new List<string>();

            for (var i = 0; i < sceneCount; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                sceneOptions.Add(sceneName);
            }


            if (tmpDropdown != null)
            {
                tmpDropdown.enabled = true;
                tmpDropdown.AddOptions(sceneOptions);
            }
            else if (standardDropdown != null)
            {
                standardDropdown.enabled = true;
                standardDropdown.AddOptions(sceneOptions.Select(option => new Dropdown.OptionData(option)).ToList());
            }

            if (sceneOptions.Count > 0)
            {
                var initialSceneRef = ScriptableObject.CreateInstance<SceneReference>();
                initialSceneRef.useStaticValues = false;
                var scene = SceneManager.GetSceneByName(sceneOptions.First());
                initialSceneRef.scene = scene;
                initialSceneRef.sceneName = scene.name;
                initialSceneRef.scenePath = scene.path;

                analyzeSceneConfig.reference = initialSceneRef;
            }


            var rectTransform = dropdownInstance.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = GetButtonPosition();
            ScriptableObject.CreateInstance<SceneReference>();
            dropdownInstance.onValueChanged.AddListener(OnDropdownValueChanged);
        }


        private void CreateButtons()
        {
            //CreateButton("File Browser for Grid", 10, OnFileBrowserOpen);
            //CreateButton("Draw Grid from Path", 14, () => _nodeBuilder.DrawGrid(path));

            // CreateButton("Analyze Scene and create node connections", 7, _sceneAnalyzer.AnalyzeScene);
            // CreateButton("Layout based on Connections", 10, NodeLayoutManagerV2.LayoutForest);
            CreateButton("Native Physics Sim", 14, () =>
            {
                _sceneAnalyzer.AnalyzeScene();
                NodeLayoutManagerV2.LayoutForest();
                nodeGraph.NodesAddComponent(typeof(Rigidbody2D));
                NodeConnectionManager.Instance.AddSpringsToConnections();
            });
            var types = new List<System.Type>
            {
                typeof(SpringJoint2D),
                typeof(Rigidbody2D)
            };
            var springSimulation = GetComponent<SpringSimulation>();
            
            var physicsConverter = gameObject.GetComponent<PhysicsEcsConverter>();
            if (physicsConverter != null)
                CreateButton("Convert to ECS", 14, physicsConverter.ConvertNodesToEcs);
            
            if (springSimulation != null)
            {
                
                CreateButton("Burst Sim", 14, () =>
                {
                    _sceneAnalyzer.AnalyzeScene();
                    NodeLayoutManagerV2.LayoutForest();
                    nodeGraph.NodesAddComponent(typeof(Rigidbody2D));
                    NodeConnectionManager.Instance.AddSpringsToConnections();
                    springSimulation.Simulate();
                });
            }
            
            CreateButton("Remove Physics", 14, () =>
            {
                nodeGraph.NodesRemoveComponents(types);
                if (springSimulation != null)
                    springSimulation.CleanupNativeArrays();
            });
            
            var sceneAnalyzer = GetComponent<SceneAnalyzer>();
            if (sceneAnalyzer != null)
            {
                CreateButton("Clear", 14, sceneAnalyzer.ClearNodes);
            }
        }

        private void CreateButton(string text, int fontSize, UnityAction onClick, Vector2 anchoredPosition=default)
        {
            if (anchoredPosition == default)
            {
                anchoredPosition = GetButtonPosition();
            }
            var browserButtonInstance = Instantiate(buttonPrefab, uiCanvas.transform);
            var buttonComponent = browserButtonInstance.GetComponent<Button>();
            var rectTransform = browserButtonInstance.GetComponent<RectTransform>();

            rectTransform.anchoredPosition = anchoredPosition;
            var buttonText = browserButtonInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = text;
                buttonText.fontSize = fontSize;
            }

            if (buttonComponent != null) buttonComponent.onClick.AddListener(onClick);
        }

        private void OnDropdownValueChanged(int index)
        {
            var selectedScene = _dropdownInstance.options[index].text;
            var scene = SceneManager.GetSceneByName(selectedScene);

            var overlayScene = ScriptableObject.CreateInstance<SceneReference>();
            overlayScene.useStaticValues = false;
            overlayScene.scene = scene;
            overlayScene.sceneName = scene.name;
            overlayScene.scenePath = scene.path;

            analyzeSceneConfig.reference = overlayScene;
            Debug.Log("the new config scene is " + analyzeSceneConfig.reference.Name + " " + analyzeSceneConfig.reference.Path);
        }

        private void OnFileBrowserOpen()
        {
            path = StandaloneFileBrowser.OpenFolderPanel("Open File", "/home/florian/Bilder", false);
        }
    }
}