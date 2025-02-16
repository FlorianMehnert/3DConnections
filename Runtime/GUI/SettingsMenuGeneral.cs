using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

public class SettingsMenuGeneral : MonoBehaviour
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
    private DropdownField _layoutDropdown;
    private DropdownField _simDropdown;
    
    // Sliders
    private SliderInt _colorSlider;
    
    // Toggle
    private Toggle _alternativeColorsButton;
    
    // Event
    [SerializeField] private RemovePhysicsEvent removePhysicsEvent;
    [SerializeField] private ClearEvent clearEvent;
    
    // Config - ScriptableObjects
    [SerializeField] private LayoutParameters layoutParameters;
    [SerializeField] private ToAnalyzeScene toAnalyzeScene;
    
    // Data - ScriptableObjects
    [SerializeField] private NodeGraphScriptableObject nodeGraph;
    
    // Internal
    private System.Action[] _actions;
    private System.Action _currentAction;
    
    private void OnEnable()
    {
        if (!SceneManager.GetSceneAt(0).isLoaded)
            SceneManager.LoadSceneAsync(0, LoadSceneMode.Additive);
        
        nodeGraph.Initialize();
        
        // grab all ui elements
        GrabUIElements(uiDocument.rootVisualElement);
        
        if (_clearButton != null)
            _clearButton.clicked += () => clearEvent.TriggerEvent();
        if (_removePhysicsButton != null)
            _removePhysicsButton.clicked += () => removePhysicsEvent.TriggerEvent();
        if (_sceneDropdown != null)
            PopulateSceneDropdown();
        
        if (removePhysicsEvent != null)
            removePhysicsEvent.OnEventTriggered += HandleEvent;
        
        _simDropdown?.RegisterValueChangedCallback(evt =>
        {
            OnSimulationTypeChanged(evt.newValue);
        });
        _colorSlider?.RegisterValueChangedCallback(evt => UpdateColor(evt.newValue));

        PopulateActions();
        InitializeAnalyzeButton();
        
    }

    private void GrabUIElements(VisualElement root)
    {
        _panel = root.Q<VisualElement>("Panel");
        _clearButton = root.Q<Button>("Clear");
        _removePhysicsButton = root.Q<Button>("RemovePhysics");
        _sceneDropdown = root.Q<DropdownField>("DropdownScene");
        _layoutDropdown = root.Q<DropdownField>("DropdownLayout");
        _simDropdown = root.Q<DropdownField>("DropdownSimType");
        _startButton = root.Q<Button>("AnalyzeScene");
        _colorSlider = root.Q<SliderInt>("ColorSlider");
        _alternativeColorsButton = root.Q<Toggle>("AlternativeColorsToggle");
    }

    private void PopulateActions()
    {
        var springSimulation = FindFirstObjectByType<SpringSimulation>();
        var gpuSpringSim = FindFirstObjectByType<ComputeSpringSimulation>();
        _actions = new System.Action[]
        {
            () =>
            {
                StaticLayout();
                Debug.Log("Execute 0 static");
            },
            () =>
            {
                StaticLayout();
                nodeGraph.NodesAddComponent(typeof(Rigidbody2D));
                NodeConnectionManager.Instance.AddSpringsToConnections();
                Debug.Log("Execute 1 component");
            },
            () =>
            {
                StaticLayout();
                NodeConnectionManager.Instance.ConvertToNativeArray(); // convert connections to a burst array
                nodeGraph.NodesAddComponent(typeof(Rigidbody2D));
                NodeConnectionManager.Instance.AddSpringsToConnections();
                if (springSimulation != null)
                    springSimulation.Simulate();
                else
                    Debug.Log("missing springSimulation Script on the Manager");
                Debug.Log("Execute 2 burst");
            },
            () =>
            {
                if (gpuSpringSim == null) return;
                StaticLayout();
                NodeConnectionManager.Instance.ConvertToNativeArray();
                nodeGraph.NodesAddComponent(typeof(Rigidbody2D));
                NodeConnectionManager.Instance.AddSpringsToConnections();
                gpuSpringSim.Initialize();
                Debug.Log("Execute 3 gpu");
            }
        };
    }

    private void InitializeAnalyzeButton()
    {
        _currentAction = _actions[0];
        _startButton.clicked += _currentAction;
    }
    
    private void OnDisable()
    {
        if (removePhysicsEvent != null)
            removePhysicsEvent.OnEventTriggered -= HandleEvent;
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
        if (_sceneDropdown == null) return;
        _sceneDropdown.choices = sceneNames;
        if (sceneNames.Count > 0)
            _sceneDropdown.value = sceneNames[0];
    }
    
    public void StaticLayout()
    {
        // TODO: add analyzeScene Event
        var sceneAnalyzer = FindFirstObjectByType<SceneAnalyzer>();
        if (sceneAnalyzer)
        {
            sceneAnalyzer.AnalyzeScene();
            NodeLayoutManagerV2.LayoutForest(layoutParameters);
        }
        else
            Debug.Log("did not find");
    }
    
    public void ApplyComponentPhysics()
    {
        if (nodeGraph.AllNodes.Count <= 0) return;
        removePhysicsEvent.TriggerEvent();
        var springSimulation = FindFirstObjectByType<SpringSimulation>();
        if (springSimulation)
            springSimulation.CleanupNativeArrays();

        nodeGraph.NodesAddComponent(typeof(Rigidbody2D));

        // required to avoid intersections when using components
        foreach (var boxCollider2D in nodeGraph.AllNodes.Select(node => node.GetComponent<BoxCollider2D>()).Where(boxCollider2D => boxCollider2D))
        {
            boxCollider2D.isTrigger = false;
            boxCollider2D.size = Vector2.one * 5;
        }

        NodeLayoutManagerV2.LayoutForest(layoutParameters);
        NodeConnectionManager.Instance.AddSpringsToConnections();
    }
    
    public void ApplyBurstPhysics()
    {
        Debug.Log("apply burst physics");
        var springSimulation = FindFirstObjectByType<SpringSimulation>();
        if (springSimulation)
        {
            if (nodeGraph.AllNodes.Count <= 0) return;
            removePhysicsEvent.TriggerEvent();
            NodeLayoutManagerV2.LayoutForest(layoutParameters);
            NodeConnectionManager.Instance.UseNativeArray();
            nodeGraph.NodesAddComponent(typeof(Rigidbody2D));
            NodeConnectionManager.Instance.AddSpringsToConnections();
            NodeConnectionManager.Instance.ResizeNativeArray();
            NodeConnectionManager.Instance.ConvertToNativeArray();
            springSimulation.Simulate();
        }
        else
        {
            Debug.Log("missing springSimulation Script on the Manager");
        }
    }
    
    public void ApplyGPUPhysics()
    {
        Debug.Log("apply gpu physics");
        var gpuSpringSim = FindFirstObjectByType<ComputeSpringSimulation>();
        if (gpuSpringSim)
        {
            if (nodeGraph.AllNodes.Count <= 0) return;
            removePhysicsEvent.TriggerEvent();
            NodeLayoutManagerV2.LayoutForest(layoutParameters);
            NodeConnectionManager.Instance.UseNativeArray();
            nodeGraph.NodesAddComponent(typeof(Rigidbody2D));
            NodeConnectionManager.Instance.AddSpringsToConnections();
            NodeConnectionManager.Instance.ResizeNativeArray();
            NodeConnectionManager.Instance.ConvertToNativeArray(); // convert connections to a burst array
            Debug.Log("initializing gpu physics");
            var springSimulation = GetComponent<SpringSimulation>();
            if (springSimulation)
                springSimulation.Disable();
            gpuSpringSim.Initialize();
        }
        else
        {
            Debug.Log("missing ComputeSpringSimulation Script on the Manager");
        }
    }
    
    private void UpdateColor(int sliderValue)
    {
        Debug.Log("in update Colors");
        // Apply color to all connections
        var baseColor = new Color(0.2f, 0.6f, 1f);
        Color.RGBToHSV(baseColor, out var h, out var s, out var v);

        // Proper hue shifting without clamping incorrectly
        h = (float)((h + .25) * (float)sliderValue % 1f);
        s = Mathf.Max(0.5f, s); // Ensure some saturation
        v = Mathf.Max(0.5f, v); // Ensure some brightness

        baseColor = Color.HSVToRGB(h, s, v);

        // Generate color palette
        var colors = Colorpalette.GeneratePaletteFromBaseColor(
            baseColor: baseColor,
            prebuiltChannels: sliderValue,
            generateColors: false,
            alternativeColors: _alternativeColorsButton.value
        );

        if (NodeConnectionManager.Instance == null || !NodeConnectionManager.Instance.conSo || NodeConnectionManager.Instance.conSo.connections == null)
        {
            return;
        }
        var connections = NodeConnectionManager.Instance.conSo.connections;

        // Assign colors based on connection types
        foreach (var connection in connections)
        {
            connection.connectionColor = connection.connectionType switch
            {
                "parentChildConnection" => colors[4],
                "componentConnection" => colors[5],
                "referenceConnection" => colors[6],
                _ => Color.white,
            };
            connection.ApplyConnection();
        }

        // Apply colors to nodes
        if (nodeGraph.AllNodes.Count > 0 && nodeGraph.AllNodes[0] == null)
        {
            nodeGraph.AllNodes = SceneHandler.GetNodesUsingTheNodegraphParentObject();
        }
        foreach (var node in nodeGraph.AllNodes)
        {
            var coloredObject = node.GetComponent<ColoredObject>();
            var nodeType = node.GetComponent<NodeType>();

            coloredObject.SetOriginalColor(nodeType.nodeTypeName switch
            {
                "GameObject" => colors[0],
                "Component" => colors[1],
                "ScriptableObject" => colors[2],
                _ => Color.white,
            });

            coloredObject.SetToOriginalColor();
        }
    }
    
    private void OnSimulationTypeChanged(string newValue)
    {
        _startButton.clicked -= _currentAction;
        var dropdownValue = (int)System.Enum.Parse(typeof(SimulationType), newValue);
        _currentAction = _actions[dropdownValue];
        _startButton.clicked += _currentAction;
        Debug.Log($"Simulation type changed to {newValue} ({dropdownValue})");
    }
    

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    private void ShowMenu()
    {
        _panel.RemoveFromClassList("hidden");
    }

    private void HideMenu()
    {
        _panel.AddToClassList("hidden");
    }

    private void ToggleMenu()
    {
        if (_panel.ClassListContains("hidden"))
            ShowMenu();
        else
            HideMenu();
    }
    
    private void HandleEvent()
    {
        // ChangeButtonEnabled(_executeNodeSpawnButton.gameObject, true);
        // ChangeButtonEnabled(_removePhysicsButton.gameObject, false);
    }
}