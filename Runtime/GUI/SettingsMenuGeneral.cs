using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

/// <summary>
/// Settings Menu which allows the user to adjust the scene to analyze, layout and parameters
/// </summary>
public class SettingsMenuGeneral : MonoBehaviour, IMenu
{
    // General UI
    public UIDocument uiDocument;
    private VisualElement _panel;

    // Buttons
    private Button _clearButton;
    private Button _removePhysicsButton;
    private Button _startButton;

    // Dropdowns
    private DropdownField _sceneDropdown;
    private DropdownField _simDropdown;

    // Sliders
    private SliderInt _colorSlider;

    // Toggle
    private Toggle _alternativeColorsButton;

    // Text input
    private TextField _textField;

    // Internal
    private System.Action[] _actions;
    private System.Action _currentAction;

    public KeyCode menuKeybind = KeyCode.Escape;

    private void Start()
    {
        if (!SceneManager.GetSceneAt(0).isLoaded)
            SceneManager.LoadSceneAsync(0, LoadSceneMode.Additive);

        if (ScriptableObjectInventory.Instance && ScriptableObjectInventory.Instance.graph) ScriptableObjectInventory.Instance.graph.Initialize();

        // Ensure the UIDocument is available
        if (!uiDocument)
        {
            uiDocument = GetComponent<UIDocument>();
            if (!uiDocument)
            {
                Debug.LogError("UIDocument component is missing!");
                return;
            }
        }

        GrabUIElements(uiDocument.rootVisualElement);

        SetupUICallbacks();
        PopulateActions();
        InitializeAnalyzeButton();

        // since this is a scriptable object, in edit mode this will not reset
        if (ScriptableObjectInventory.Instance && ScriptableObjectInventory.Instance.applicationState) ScriptableObjectInventory.Instance.applicationState.spawnedNodes = false;

        // Register with menu manager
        MenuManager.Instance.RegisterMenu(menuKeybind, this);

        Debug.Log($"SettingsMenuGeneral registered with keybind {menuKeybind}");
    }

    private void SetupUICallbacks()
    {
        if (_clearButton != null)
            _clearButton.clicked += () =>
            {
                if (ScriptableObjectInventory.Instance && ScriptableObjectInventory.Instance.clearEvent)
                    ScriptableObjectInventory.Instance.clearEvent.TriggerEvent();
            };

        if (_removePhysicsButton != null)
            _removePhysicsButton.clicked += () =>
            {
                if (ScriptableObjectInventory.Instance && ScriptableObjectInventory.Instance.removePhysicsEvent)
                    ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
            };

        if (_sceneDropdown != null)
            PopulateSceneDropdown();

        _simDropdown?.RegisterValueChangedCallback(evt => { OnSimulationTypeChanged(evt.newValue); });

        _colorSlider?.RegisterValueChangedCallback(evt => UpdateColor(evt.newValue));

        _textField?.RegisterValueChangedCallback(evt =>
        {
            if (ScriptableObjectInventory.Instance &&
                ScriptableObjectInventory.Instance.menuState &&
                ScriptableObjectInventory.Instance.menuState.menuOpen &&
                ScriptableObjectInventory.Instance.graph)
                ScriptableObjectInventory.Instance.graph.SearchNodes(evt.newValue);
        });
    }

    private void GrabUIElements(VisualElement root)
    {
        if (root == null)
        {
            Debug.LogError("Root visual element is null!");
            return;
        }

        _panel = root.Q<VisualElement>("Panel");
        _clearButton = root.Q<Button>("Clear");
        _removePhysicsButton = root.Q<Button>("RemovePhysics");
        _sceneDropdown = root.Q<DropdownField>("DropdownScene");
        _simDropdown = root.Q<DropdownField>("DropdownSimType");
        _startButton = root.Q<Button>("AnalyzeScene");
        _colorSlider = root.Q<SliderInt>("ColorSlider");
        _alternativeColorsButton = root.Q<Toggle>("AlternativeColorsToggle");
        _textField = root.Q<TextField>("SearchField");
    }

    private void PopulateActions()
    {
        var springSimulation = FindFirstObjectByType<SpringSimulation>();
        var gpuSpringSim = FindFirstObjectByType<ComputeSpringSimulation>();
        _actions = new System.Action[]
        {
            () =>
            {
                // Static 
                var layout = FindFirstObjectByType<StaticNodeLayoutManager>();
                layout.StaticLayout();
                if (ScriptableObjectInventory.Instance && ScriptableObjectInventory.Instance.applicationState)
                    ScriptableObjectInventory.Instance.applicationState.spawnedNodes = true;
            },
            () =>
            {
                // default
                var layout = FindFirstObjectByType<StaticNodeLayoutManager>();
                layout.StaticLayout();
                if (ScriptableObjectInventory.Instance)
                {
                    if (ScriptableObjectInventory.Instance.applicationState)
                        ScriptableObjectInventory.Instance.applicationState.spawnedNodes = true;
                    if (ScriptableObjectInventory.Instance.graph)
                        ScriptableObjectInventory.Instance.graph.NodesAddComponent(typeof(Rigidbody2D));
                }

                if (NodeConnectionManager.Instance)
                    NodeConnectionManager.Instance.AddSpringsToConnections();
            },
            () =>
            {
                // burst
                var layout = FindFirstObjectByType<StaticNodeLayoutManager>();
                layout.StaticLayout();

                if (ScriptableObjectInventory.Instance)
                {
                    if (ScriptableObjectInventory.Instance.applicationState)
                        ScriptableObjectInventory.Instance.applicationState.spawnedNodes = true;
                    if (ScriptableObjectInventory.Instance.graph)
                        ScriptableObjectInventory.Instance.graph.NodesAddComponent(typeof(Rigidbody2D));
                }

                if (NodeConnectionManager.Instance)
                {
                    NodeConnectionManager.Instance.ConvertToNativeArray(); // convert connections to a burst array
                    NodeConnectionManager.Instance.AddSpringsToConnections();
                }

                if (springSimulation)
                    springSimulation.Simulate();
                else
                    Debug.Log("missing springSimulation Script on the Manager");
            },
            () =>
            {
                // gpu
                if (!gpuSpringSim) return;
                var layout = FindFirstObjectByType<StaticNodeLayoutManager>();
                layout.StaticLayout();

                if (ScriptableObjectInventory.Instance)
                {
                    if (ScriptableObjectInventory.Instance.applicationState)
                        ScriptableObjectInventory.Instance.applicationState.spawnedNodes = true;
                    if (ScriptableObjectInventory.Instance.graph)
                        ScriptableObjectInventory.Instance.graph.NodesAddComponent(typeof(Rigidbody2D));
                }

                if (NodeConnectionManager.Instance)
                {
                    NodeConnectionManager.Instance.ConvertToNativeArray();
                    NodeConnectionManager.Instance.AddSpringsToConnections();
                }

                gpuSpringSim.Initialize();
            },
            () =>
            {
                // ComponentV2 (check if the simulation component is already added and add if not)
                var forceDirected = ScriptableObjectInventory.Instance.simulationRoot.gameObject.GetComponent<ForceDirectedSimulationV2>();
                if (!forceDirected) forceDirected = ScriptableObjectInventory.Instance.simulationRoot.gameObject.AddComponent<ForceDirectedSimulationV2>();

                // static layout using manager if found
                var layout = FindFirstObjectByType<StaticNodeLayoutManager>();
                Debug.Log(layout ? "layout manager found" : "static layout manager not found");
                layout.StaticLayout();

                // set state to spawnedNodes
                if (ScriptableObjectInventory.Instance && ScriptableObjectInventory.Instance.applicationState)
                    ScriptableObjectInventory.Instance.applicationState.spawnedNodes = true;

                // start simulation
                forceDirected.Initialize();
            },
            () =>
            {
                // minimalGPU
                var forceDirectedSim = FindFirstObjectByType<MinimalForceDirectedSimulation>();
                if (!forceDirectedSim) return;
                if (ScriptableObjectInventory.Instance.graph.AllNodes.Count <= 0) return;
                ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
                NodeConnectionManager.Instance.UseNativeArray();
                ScriptableObjectInventory.Instance.graph.NodesAddComponent(typeof(Rigidbody2D));
                NodeConnectionManager.Instance.AddSpringsToConnections();
                NodeConnectionManager.Instance.ResizeNativeArray();
                NodeConnectionManager.Instance.ConvertToNativeArray(); // convert connections to a burst array
                Debug.Log("initializing gpu physics");
                var springSimulation = GetComponent<SpringSimulation>();
                if (springSimulation)
                    springSimulation.Disable();
                forceDirectedSim.nodeTransforms = ScriptableObjectInventory.Instance.graph.AllNodes.Select(node => node.transform).ToArray();
                forceDirectedSim.Initialize();
            }
        };
    }

    private void InitializeAnalyzeButton()
    {
        if (_startButton == null || _actions == null || _actions.Length == 0)
        {
            Debug.LogError("Failed to initialize Analyze button: button or actions are null");
            return;
        }

        _currentAction = _actions[0];
        _startButton.clicked += _currentAction;
    }

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
            _alternativeColorsButton.value,
            false
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
                _ => Color.white
            };
            connection.ApplyConnection();
        }

        // Apply colors to nodes
        if (ScriptableObjectInventory.Instance.graph.AllNodes.Count > 0 && !ScriptableObjectInventory.Instance.graph.AllNodes[0]) ScriptableObjectInventory.Instance.graph.AllNodes = SceneHandler.GetNodesUsingTheNodegraphParentObject();

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
        if (_startButton == null || _actions == null)
        {
            Debug.LogError("Cannot change simulation type: button or actions are null");
            return;
        }

        _startButton.clicked -= _currentAction;

        if (System.Enum.TryParse(newValue, out SimulationType simType))
        {
            var index = (int)simType;
            if (index >= 0 && index < _actions.Length)
            {
                _currentAction = _actions[index];
                _startButton.clicked += _currentAction;
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

    public void OnMenuOpen()
    {
        if (_panel == null) return;
        _panel.RemoveFromClassList("hidden");
        ScriptableObjectInventory.Instance.menuState.menuOpen = true;
    }

    public void OnMenuClose()
    {
        if (_panel == null) return;
        _panel.AddToClassList("hidden");
        ScriptableObjectInventory.Instance.menuState.menuOpen = false;
    }
}