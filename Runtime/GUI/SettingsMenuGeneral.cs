using _3DConnections.Runtime.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _3DConnections.Runtime.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UIElements;
    using Button = UnityEngine.UIElements.Button;
    using ScriptableObjectInventory;
    using Scene;
    using Nodes;
    using ScriptableObjects;
    using Utils;


    /// <summary>
    /// Settings Menu which allows the user to adjust the scene to analyze, layout and parameters
    /// </summary>
    public class SettingsMenuGeneral : MonoBehaviour
    {
        // General UI
        public UIDocument uiDocument;
        public UIDocument uiDocumentLeft;

        // Buttons
        private Button _clearButton;
        private Button _removePhysicsButton;
        private Button _startButton;

        private Button _hrButton;
        private Button _crButton;
        private Button _srButton;
        private Button _drButton;

        private Button _goButton;
        private Button _coButton;
        private Button _soButton;
        private Button _voButton;

        private VisualElement _goButtonElement;
        private VisualElement _coButtonElement;
        private VisualElement _soButtonElement;
        private VisualElement _voButtonElement;

        // Dropdowns
        private DropdownField _sceneDropdown;
        private DropdownField _simDropdown;

        // Sliders
        private SliderInt _colorSlider;

        // Toggle
        private Toggle _alternativeColorsButton;
        private int _sliderValue;
        private Toggle _levelOfDetailToggle;

        public GameObject clusterNodePrefab;

        // Handlers
        private Action _clearButtonHandler;
        private Action _removePhysicsHandler;
        private Action _hrButtonHandler;
        private Action _crButtonHandler;
        private Action _srButtonHandler;
        private Action _drButtonHandler;

        // Store value change callbacks
        private EventCallback<ChangeEvent<string>> _simDropdownCallback;
        private EventCallback<ChangeEvent<int>> _colorSliderCallback;
        private EventCallback<ChangeEvent<bool>> _alternativeColorsCallback;
        private EventCallback<ChangeEvent<bool>> _levelOfDetailCallback;

        // Events to invoke
        [SerializeField] private AnalyzeEventChannel analyzeEventChannel;

        private void Start()
        {
            if (!SceneManager.GetSceneAt(0).isLoaded)
                SceneManager.LoadSceneAsync(0, LoadSceneMode.Additive);

            if (ScriptableObjectInventory.Instance && ScriptableObjectInventory.Instance.graph)
                ScriptableObjectInventory.Instance.graph.Initialize();
        }

        private void OnEnable()
        {
            if (!uiDocument)
            {
                uiDocument = GetComponent<UIDocument>();
                if (!uiDocument)
                {
                    Debug.LogError("UIDocument component is missing!");
                    return;
                }
            }

            GrabUIElementsSettingsMenu(uiDocument.rootVisualElement);
            GrabUIElementsInfoPanel(uiDocumentLeft.rootVisualElement);

            SetupUICallbacks();
            InitializeAnalyzeButton();

            // since this is a scriptable object, in edit mode this will not reset
            if (ScriptableObjectInventory.Instance && ScriptableObjectInventory.Instance.applicationState)
                ScriptableObjectInventory.Instance.applicationState.spawnedNodes = false;

            // menu is closed on start of the program
            ScriptableObjectInventory.Instance.menuState.menuOpen =
                uiDocument.rootVisualElement.style.display == DisplayStyle.Flex;

            // update node counts 
            UpdateText();
            ScriptableObjectInventory.Instance.graph.OnGoCountChanged += UpdateText;

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        private void OnDisable()
        {
            if (ScriptableObjectInventory.Instance == null) return;
            ScriptableObjectInventory.Instance.graph.OnGoCountChanged -= UpdateText;
            RemoveUICallbacks();
        }

        [ContextMenu("Update Text")]
        private void UpdateText()
        {
            try
            {
                var goCount = ScriptableObjectInventory.Instance.graph.AllNodes.Count(n =>
                    n.GetComponent<NodeType>().nodeTypeName == NodeTypeName.GameObject);
                var coCount = ScriptableObjectInventory.Instance.graph.AllNodes.Count(n =>
                    n.GetComponent<NodeType>().nodeTypeName == NodeTypeName.Component);
                var soCount = ScriptableObjectInventory.Instance.graph.AllNodes.Count(n =>
                    n.GetComponent<NodeType>().nodeTypeName == NodeTypeName.ScriptableObject);
                var allNodes = goCount + coCount + soCount;
                ScriptableObjectInventory.Instance.graph.goCount = goCount;
                ScriptableObjectInventory.Instance.graph.coCount = coCount;
                ScriptableObjectInventory.Instance.graph.soCount = soCount;
                ScriptableObjectInventory.Instance.graph.voCount = allNodes;
            }
            catch (Exception)
            {
            }
        }

        private void SetupUICallbacks()
        {
            // Clear Button
            if (_clearButton != null)
            {
                _clearButtonHandler = () =>
                {
                    if (ScriptableObjectInventory.Instance && ScriptableObjectInventory.Instance.clearEvent)
                        ScriptableObjectInventory.Instance.clearEvent.TriggerEvent();

                    ScriptableObjectInventory.Instance.graph.InvokeOnAllCountChanged();
                };
                _clearButton.clicked += _clearButtonHandler;
            }

            // Remove Physics Button
            if (_removePhysicsButton != null)
            {
                _removePhysicsHandler = () =>
                {
                    if (ScriptableObjectInventory.Instance && ScriptableObjectInventory.Instance.removePhysicsEvent)
                        ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
                };
                _removePhysicsButton.clicked += _removePhysicsHandler;
            }

            // Scene Dropdown
            if (_sceneDropdown != null)
                PopulateSceneDropdown();

            // Simulation Dropdown
            if (_simDropdown != null)
            {
                _simDropdownCallback = evt => OnSimulationTypeChanged(evt.newValue);
                _simDropdown.RegisterValueChangedCallback(_simDropdownCallback);
            }

            // Color Slider
            if (_colorSlider != null)
            {
                _colorSliderCallback = evt => UpdateColor(evt.newValue);
                _colorSlider.RegisterValueChangedCallback(_colorSliderCallback);
            }

            // Alternative Colors Button
            if (_alternativeColorsButton != null)
            {
                _alternativeColorsCallback = _ => UpdateColor(_sliderValue);
                _alternativeColorsButton.RegisterValueChangedCallback(_alternativeColorsCallback);
            }

            // Level of Detail Toggle
            if (_levelOfDetailToggle != null)
            {
                _levelOfDetailCallback = evt => HandleLevelOfDetailChange(evt.newValue);
                _levelOfDetailToggle.RegisterValueChangedCallback(_levelOfDetailCallback);
            }

            // Initialize connection settings
            ScriptableObjectInventory.Instance.conSo.parentChildReferencesActive = true;
            ScriptableObjectInventory.Instance.conSo.componentReferencesActive = true;
            ScriptableObjectInventory.Instance.conSo.fieldReferencesActive = true;
            ScriptableObjectInventory.Instance.conSo.dynamicReferencesActive = true;

            // HR Button
            if (_hrButton != null)
            {
                _hrButtonHandler = () =>
                {
                    var pcra = ScriptableObjectInventory.Instance.conSo.parentChildReferencesActive;
                    NodeConnectionManager.SetConnectionType("parentChildConnection", !pcra);
                    ScriptableObjectInventory.Instance.conSo.parentChildReferencesActive = !pcra;
                };
                _hrButton.clicked += _hrButtonHandler;
            }

            // CR Button
            if (_crButton != null)
            {
                _crButtonHandler = () =>
                {
                    var cra = ScriptableObjectInventory.Instance.conSo.componentReferencesActive;
                    NodeConnectionManager.SetConnectionType("componentConnection", !cra);
                    ScriptableObjectInventory.Instance.conSo.componentReferencesActive = !cra;
                };
                _crButton.clicked += _crButtonHandler;
            }

            // SR Button
            if (_srButton != null)
            {
                _srButtonHandler = () =>
                {
                    var fra = ScriptableObjectInventory.Instance.conSo.fieldReferencesActive;
                    NodeConnectionManager.SetConnectionType("referenceConnection", !fra);
                    ScriptableObjectInventory.Instance.conSo.fieldReferencesActive = !fra;
                };
                _srButton.clicked += _srButtonHandler;
            }

            // DR Button
            if (_drButton == null) return;
            _drButtonHandler = () =>
            {
                var dra = ScriptableObjectInventory.Instance.conSo.dynamicReferencesActive;
                NodeConnectionManager.SetConnectionType("dynamicComponentConnection", !dra);
                ScriptableObjectInventory.Instance.conSo.dynamicReferencesActive = !dra;
            };
            _drButton.clicked += _drButtonHandler;
        }

        private void RemoveUICallbacks()
        {
            // Remove button click handlers
            if (_clearButton != null && _clearButtonHandler != null)
                _clearButton.clicked -= _clearButtonHandler;

            if (_removePhysicsButton != null && _removePhysicsHandler != null)
                _removePhysicsButton.clicked -= _removePhysicsHandler;

            if (_hrButton != null && _hrButtonHandler != null)
                _hrButton.clicked -= _hrButtonHandler;

            if (_crButton != null && _crButtonHandler != null)
                _crButton.clicked -= _crButtonHandler;

            if (_srButton != null && _srButtonHandler != null)
                _srButton.clicked -= _srButtonHandler;

            if (_drButton != null && _drButtonHandler != null)
                _drButton.clicked -= _drButtonHandler;

            // Remove value changed callbacks
            if (_simDropdown != null && _simDropdownCallback != null)
                _simDropdown.UnregisterValueChangedCallback(_simDropdownCallback);

            if (_colorSlider != null && _colorSliderCallback != null)
                _colorSlider.UnregisterValueChangedCallback(_colorSliderCallback);

            if (_alternativeColorsButton != null && _alternativeColorsCallback != null)
                _alternativeColorsButton.UnregisterValueChangedCallback(_alternativeColorsCallback);

            if (_levelOfDetailToggle != null && _levelOfDetailCallback != null)
                _levelOfDetailToggle.UnregisterValueChangedCallback(_levelOfDetailCallback);

            if (_startButton != null)
                _startButton.clicked -= OnAnalyzeSceneButtonClicked;

            // Clear references
            _clearButtonHandler = null;
            _removePhysicsHandler = null;
            _hrButtonHandler = null;
            _crButtonHandler = null;
            _srButtonHandler = null;
            _drButtonHandler = null;
            _simDropdownCallback = null;
            _colorSliderCallback = null;
            _alternativeColorsCallback = null;
            _levelOfDetailCallback = null;
        }

        private void GrabUIElementsInfoPanel(VisualElement root)
        {
            if (root == null)
            {
                Debug.LogError("Root visual element for info panel is null!");
                return;
            }

            _goButton = root.Q<Button>("GO");
            _coButton = root.Q<Button>("CO");
            _soButton = root.Q<Button>("SO");
            _voButton = root.Q<Button>("VO");
            _goButtonElement = root.Q<Button>("GOButtonElement");
            _coButtonElement = root.Q<Button>("COButtonElement");
            _soButtonElement = root.Q<Button>("SOButtonElement");
            _voButtonElement = root.Q<Button>("VOButtonElement");
        }

        private void GrabUIElementsSettingsMenu(VisualElement root)
        {
            if (root == null)
            {
                Debug.LogError("Root visual element is null!");
                return;
            }

            _clearButton = root.Q<Button>("Clear");
            _removePhysicsButton = root.Q<Button>("RemovePhysics");
            _sceneDropdown = root.Q<DropdownField>("DropdownScene");
            _simDropdown = root.Q<DropdownField>("DropdownSimType");
            _startButton = root.Q<Button>("AnalyzeScene");
            _colorSlider = root.Q<SliderInt>("ColorSlider");
            _alternativeColorsButton = root.Q<Toggle>("AlternativeColorsToggle");
            _levelOfDetailToggle = root.Q<Toggle>("LOD");

            _hrButton = root.Q<Button>("HR");
            _crButton = root.Q<Button>("CR");
            _srButton = root.Q<Button>("FR");
            _drButton = root.Q<Button>("DR");
        }

        private void HandleLevelOfDetailChange(bool value)
        {
            var cam = SceneHandler.GetCameraOfOverlayedScene();
            var component = cam?.GetComponent<GraphLODManager>();

            if (component != null)
            {
                if (!value)
                {
                    component.enabled = true;
                    component.enabled = false;
                }
                else
                {
                    component.enabled = false;
                    component.enabled = true;
                }
            }
            else
            {
                var lodManager = cam?.gameObject.AddComponent<GraphLODManager>();
                if (lodManager != null)
                    lodManager.clusterNodePrefab = clusterNodePrefab;
            }
        }

        private void InitializeAnalyzeButton()
        {
            if (_startButton == null)
            {
                Debug.LogError("Failed to initialize Analyze button: button or actions are null");
                return;
            }

            _startButton.clicked += OnAnalyzeSceneButtonClicked;
        }

        /// <summary>
        /// raises the analyzeScene ScriptableObject event when clicking on the analyze button
        /// <see cref="analyzeEventChannel"/>
        /// </summary>
        private void OnAnalyzeSceneButtonClicked()
        {
            analyzeEventChannel.TriggerEvent(ScriptableObjectInventory.Instance.simulationParameters.simulationType,
                ScriptableObjectInventory.Instance.layout.layoutType);
            ;
        }

        /// <summary>
        /// dynamically populate scenes
        /// </summary>
        private void PopulateSceneDropdown()
        {
            // Get all scenes from build settings
            var sceneNames = new List<string>();
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                sceneNames.Add(sceneName);
            }

            // Assign to the dropdown
            if (_sceneDropdown == null)
            {
                Debug.LogError("Scene dropdown is null");
                return;
            }

            _sceneDropdown.choices = sceneNames;
            if (sceneNames.Count > 0)
                _sceneDropdown.value = sceneNames[0];
        }

        private void UpdateColor(int sliderValue)
        {
            _sliderValue = sliderValue;
            if (!ScriptableObjectInventory.Instance ||
                !ScriptableObjectInventory.Instance.graph ||
                _alternativeColorsButton == null)
            {
                Debug.LogError("Cannot update color: required references are null");
                return;
            }

            var baseColor = new Color(0.2f, 0.6f, 1f);
            Color.RGBToHSV(baseColor, out var h, out var s, out var v);

            h = (float)((h + .25) * (float)sliderValue % 1f);
            s = Mathf.Max(0.5f, s);
            v = Mathf.Max(0.5f, v);

            baseColor = Color.HSVToRGB(h, s, v);

            var colors = Colorpalette.GeneratePaletteFromBaseColor(
                baseColor,
                sliderValue,
                _alternativeColorsButton.value
            );

            if (!NodeConnectionManager.Instance ||
                !ScriptableObjectInventory.Instance.conSo ||
                ScriptableObjectInventory.Instance.conSo.connections == null)
            {
                Debug.LogWarning("Cannot apply connection colors: required references are null");
                return;
            }

            var connections = ScriptableObjectInventory.Instance.conSo.connections;

            foreach (var connection in connections)
            {
                connection.connectionColor = connection.connectionType switch
                {
                    "parentChildConnection" => colors[4],
                    "componentConnection" => colors[5],
                    "referenceConnection" => colors[6],
                    "dynamicComponentConnection" => colors[7],
                    _ => Color.white
                };
                connection.ApplyConnection();
            }

            // Apply colors to nodes
            if (ScriptableObjectInventory.Instance.graph.AllNodes.Count > 0 &&
                !ScriptableObjectInventory.Instance.graph.AllNodes[0])
                ScriptableObjectInventory.Instance.graph.AllNodes =
                    SceneHandler.GetNodesUsingTheNodegraphParentObject();

            foreach (var node in ScriptableObjectInventory.Instance.graph.AllNodes)
            {
                if (!node) continue;

                var coloredObject = node.GetComponent<ColoredObject>();
                var nodeType = node.GetComponent<NodeType>();

                if (!coloredObject || !nodeType) continue;

                coloredObject.SetOriginalColor(nodeType.nodeTypeName switch
                {
                    NodeTypeName.GameObject => colors[0],
                    NodeTypeName.Component => colors[1],
                    NodeTypeName.ScriptableObject => colors[2],
                    _ => Color.white
                });

                coloredObject.SetToOriginalColor();
            }
        }

        private void OnSimulationTypeChanged(string newValue)
        {
            if (Enum.TryParse(newValue, out SimulationType simType))
            {
                var index = (int)simType;
                if (index >= 0)
                {
                    ScriptableObjectInventory.Instance.simulationParameters.simulationType = simType;
                }
                else
                {
                    Debug.LogError($"Invalid simulation type index: {index}");
                }
            }
            else
            {
                Debug.LogError($"Failed to parse simulation type: {newValue}");
            }
        }

        public void DebugSelf()
        {
            Debug.Log(this);
        }
    }
}