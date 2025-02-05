using System;
using System.Collections.Generic;
using System.Linq;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [SerializeField] private GameObject sliderColorPresetPrefab;
    private TMP_Dropdown _sceneDropdownInstance;
    private TMP_Dropdown _nodeGraphDropdownInstance;
    private GameObject _clearButton;
    private GameObject _removePhysicsButton;
    private GameObject _executeNodeSpawnButton;
    private UnityAction _toExecute;
    [SerializeField] private OverlaySceneScriptableObject overlaySceneConfig;
    [SerializeField] private NodeGraphScriptableObject nodeGraph;
    [SerializeField] private RemovePhysicsEvent removePhysicsEvent;
    [SerializeField] private LayoutParameters layoutParameters;
    private UnityAction[] _actions;
    private const float TopMargin = 60f;
    private const float RightMargin = 0;
    private const float VerticalSpacing = 35f;
    private float _currentYCoordinate;

    private void OnEnable()
    {
        Init();
        Clear();
        nodeGraph.Clear();
    }

    public void Init()
    {
        // Ensure a clean canvas
        foreach (Transform tf in uiCanvas.transform)
        {
            if (tf.gameObject.activeSelf)
            {
                Destroy(tf.gameObject);
            }
        }
        
        // set to button-width plus margin from the top right corner 
        _currentYCoordinate = -(30f + TopMargin);

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
        CreateNodeGraphDropdown();
        CreateButtons();

        // possible actions that can be executed using the execute-button
        _actions = new UnityAction[]
        {
            StaticLayout,
            () =>
            {
                StaticLayout();
                nodeGraph.NodesAddComponent(typeof(Rigidbody2D));
                NodeConnectionManager.Instance.AddSpringsToConnections();
            },
            () =>
            {
                StaticLayout();
                NodeConnectionManager.Instance.ConvertToNativeArray(); // convert connections to bursrt array
                nodeGraph.NodesAddComponent(typeof(Rigidbody2D));
                NodeConnectionManager.Instance.AddSpringsToConnections();
                var springSimulation = GetComponent<SpringSimulation>();
                if (springSimulation != null)
                {
                    springSimulation.Simulate();
                }
                else
                {
                    Debug.Log("missing springSimulation Script on the Manager");
                }
            },
            () =>
            {
                var gpuSpringSim = gameObject.GetComponent<ComputeSpringSimulation>();
                if (gpuSpringSim == null) return;
                StaticLayout();
                NodeConnectionManager.Instance.ConvertToNativeArray();
                nodeGraph.NodesAddComponent(typeof(Rigidbody2D));
                NodeConnectionManager.Instance.AddSpringsToConnections();
                gpuSpringSim.Initialize();
            }
        };
    }

    private float NextYPosition()
    {
        var currentY = _currentYCoordinate;
        _currentYCoordinate -= VerticalSpacing;
        return currentY;
    }

    private Vector2 GetButtonPosition(GameObject buttonGameObject)
    {
        var rectTransform = buttonGameObject.GetComponent<RectTransform>();
        if (!rectTransform)
            return Vector2.zero;
        var buttonWidth = rectTransform.rect.width;
        var xPosition = -(buttonWidth + RightMargin);
        return new Vector2(xPosition, NextYPosition());
    }

    private void CreateSceneDropdown()
    {
        // Instantiate the dropdown prefab as child of canvas
        var dropdownInstance = Instantiate(dropdownPrefab, uiCanvas.transform);
        _sceneDropdownInstance = dropdownInstance;
        _sceneDropdownInstance.gameObject.SetActive(true);

        // Get the dropdown component (works with both standard and TMP dropdowns)
        var tmpDropdown = dropdownInstance.GetComponent<TMP_Dropdown>();

        // Clear existing options
        if (tmpDropdown)
        {
            tmpDropdown.ClearOptions();
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


        if (tmpDropdown)
        {
            tmpDropdown.enabled = true;
            tmpDropdown.AddOptions(sceneOptions);
        }

        if (sceneOptions.Count > 0)
        {
            var scene = SceneManager.GetSceneByName(sceneOptions.First());
            Debug.Log(scene.name + " is set to be analyzed");
            var sceneHandler = GetComponent<SceneHandler>();
            if (sceneHandler)
                sceneHandler.analyzeScene = scene;
        }

        var rectTransform = dropdownInstance.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = GetButtonPosition(dropdownInstance.gameObject);
        ScriptableObject.CreateInstance<SceneReference>();
        dropdownInstance.onValueChanged.AddListener(OnSceneDropdownValueChanged);
    }

    private void CreateNodeGraphDropdown()
    {
        var dropdownInstance = Instantiate(dropdownPrefab, uiCanvas.transform);
        _nodeGraphDropdownInstance = dropdownInstance;
        _nodeGraphDropdownInstance.gameObject.SetActive(true);
        var tmpDropdown = dropdownInstance.GetComponent<TMP_Dropdown>();
        if (tmpDropdown)
        {
            tmpDropdown.ClearOptions();
        }

        if (tmpDropdown)
        {
            tmpDropdown.enabled = true;
            var dropdownOptions = new List<string>()
            {
                "Static Layout",
                "Native Physics Sim",
                "Burst Physics Sim",
                "GPU Physics Sim"
            };
            tmpDropdown.AddOptions(dropdownOptions);
        }

        var rectTransform = dropdownInstance.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = GetButtonPosition(tmpDropdown.gameObject);
        ScriptableObject.CreateInstance<SceneReference>();
        dropdownInstance.onValueChanged.AddListener(OnNodeGraphDropdownChanged);
    }

    public void StaticLayout()
    {
        OnSceneDropdownValueChanged(_sceneDropdownInstance.value);
        _sceneAnalyzer.AnalyzeScene();
        NodeLayoutManagerV2.LayoutForest(layoutParameters);
    }


    private void CreateButtons()
    {
        nodeGraph.Initialize();
        _executeNodeSpawnButton = CreateButton("Execute Action", 14, (() =>
        {
            OnNodeGraphDropdownChanged(_nodeGraphDropdownInstance.value);
            _sceneAnalyzer.AnalyzeScene();
            NodeLayoutManagerV2.LayoutForest(layoutParameters);
            ChangeButtonEnabled(_removePhysicsButton, true);
            ChangeButtonEnabled(_clearButton, true);
        }), disableAfterClick: true);

        _removePhysicsButton = CreateButton("Remove Physics", 14, () =>
        {
            removePhysicsEvent.TriggerEvent();
            RemovePhysics();
        }, isEnabled: false, disableAfterClick: true);

        var sceneAnalyzer = GetComponent<SceneAnalyzer>();
        if (sceneAnalyzer != null)
        {
            _clearButton = CreateButton("Clear", 14, Clear, isEnabled: false, disableAfterClick: true);
        }

        CreatePresetSlider();
        if (!SceneManager.GetSceneAt(0).isLoaded)
            SceneManager.LoadSceneAsync(0, LoadSceneMode.Additive);
    }

    private void RemovePhysics()
    {
        var types = new List<Type>
        {
            typeof(SpringJoint2D),
            typeof(Rigidbody2D)
        };
        var parentObject = SceneHandler.GetParentObject();

        if (parentObject == null)
            return;
        nodeGraph.NodesRemoveComponents(types, SceneHandler.GetNodesUsingTheNodegraphParentObject());
    }

    public void Clear()
    {
        var sceneAnalyzer = GetComponent<SceneAnalyzer>();
        if (!sceneAnalyzer) return;
        sceneAnalyzer.ClearNodes();
        
        var gpuSimulation = GetComponent<ComputeSpringSimulation>();
        if (!gpuSimulation) return;
        gpuSimulation.CleanupBuffers();
        
        nodeGraph.Initialize();
        ChangeButtonEnabled(_executeNodeSpawnButton.gameObject, true);
        ChangeButtonEnabled(_removePhysicsButton.gameObject, false);
    }

    private static void ChangeButtonEnabled(GameObject buttonGameObject, bool isEnabled)
    {
        var image = buttonGameObject.GetComponent<Image>();
        var button = buttonGameObject.GetComponent<Button>();
        if (!buttonGameObject) return;

        image.color = isEnabled ? Color.white : Color.gray;
        button.enabled = isEnabled;
    }


    private GameObject CreateButton(string text, int fontSize, UnityAction onClick, Vector2 anchoredPosition = default, bool isEnabled = true, bool disableAfterClick = false)
    {
        var browserButtonInstance = Instantiate(buttonPrefab, uiCanvas.transform);
        var buttonTransform = browserButtonInstance.GetComponent<RectTransform>();
        buttonTransform.anchorMin = buttonTransform.anchorMax = Vector2.one;

        if (anchoredPosition == default)
        {
            anchoredPosition = GetButtonPosition(browserButtonInstance);
        }

        var buttonComponent = browserButtonInstance.GetComponent<Button>();
        buttonComponent.enabled = isEnabled;
        buttonComponent.image.color = isEnabled ? Color.white : Color.gray;
        var rectTransform = browserButtonInstance.GetComponent<RectTransform>();

        rectTransform.anchoredPosition = anchoredPosition;
        var buttonText = browserButtonInstance.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText)
        {
            buttonText.text = text;
            buttonText.fontSize = fontSize;
        }

        if (!buttonComponent) return browserButtonInstance;
        buttonComponent.onClick.AddListener(onClick);
        buttonComponent.onClick.AddListener(() =>
        {
            if (disableAfterClick)
                ChangeButtonEnabled(browserButtonInstance, false);
        });

        return browserButtonInstance;
    }

    private GameObject CreatePresetSlider()
    {
        var colorSlider = Instantiate(sliderColorPresetPrefab, uiCanvas.transform);
        var rectTransform = colorSlider.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = GetButtonPosition(colorSlider.gameObject);
        return colorSlider;
        
    }

    /// <summary>
    /// Changes the listeners of the execute-button to invoke the currently selected dropdown option
    /// </summary>
    /// <param name="index"></param>
    private void OnNodeGraphDropdownChanged(int index)
    {
        var button = _executeNodeSpawnButton.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            ChangeButtonEnabled(_removePhysicsButton, true);
            ChangeButtonEnabled(_clearButton, true);
            _actions[index].Invoke();
            ChangeButtonEnabled(_executeNodeSpawnButton, false);
        });
    }

    private void OnSceneDropdownValueChanged(int index)
    {
        var selectedScene = _sceneDropdownInstance.options[index].text;
        var scene = SceneManager.GetSceneByName(selectedScene);

        var sceneHandler = GetComponent<SceneHandler>();
        if (sceneHandler != null)
        {
            sceneHandler.analyzeScene = scene;
        }

        SetButtonsEnabled(sceneHandler.analyzeScene.IsValid());
    }

    private void SetButtonsEnabled(bool buttonEnabled = false)
    {
        foreach (Transform uiElement in uiCanvas.transform)
        {
            if (uiElement.gameObject.ToString() != "GUIButton(Clone)") continue;
            var button = uiElement.gameObject.GetComponent<Button>();
            if (button != null)
            {
                button.enabled = buttonEnabled;
            }
        }
    }

    private void OnFileBrowserOpen()
    {
        path = StandaloneFileBrowser.OpenFolderPanel("Open File", "/home/florian/Bilder", false);
    }

    public void ApplyComponentPhysics()
    {
        if (nodeGraph.AllNodes.Count <= 0) return;
        removePhysicsEvent.TriggerEvent();
        var springSimulation = gameObject.GetComponent<SpringSimulation>();
        if (springSimulation)
            springSimulation.CleanupNativeArrays();

        ChangeButtonEnabled(_executeNodeSpawnButton.gameObject, false);
        ChangeButtonEnabled(_removePhysicsButton.gameObject, true);
        ChangeButtonEnabled(_clearButton.gameObject, true);

        nodeGraph.NodesAddComponent(typeof(Rigidbody2D));

        // required to avoid intersections when using components
        foreach (var collider in nodeGraph.AllNodes.Select(node => node.GetComponent<BoxCollider2D>()).Where(collider => collider))
        {
            collider.isTrigger = false;
            collider.size = Vector2.one * 5;
        }

        NodeConnectionManager.Instance.ConvertToNativeArray();
        NodeLayoutManagerV2.LayoutForest(layoutParameters);
        NodeConnectionManager.Instance.AddSpringsToConnections();
    }

    public void ApplyBurstPhysics()
    {
        Debug.Log("apply burst physics");
        var springSimulation = GetComponent<SpringSimulation>();
        if (springSimulation)
        {
            if (nodeGraph.AllNodes.Count <= 0) return;
            RemovePhysics();
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
        var gpuSpringSim = gameObject.GetComponent<ComputeSpringSimulation>();
        if (gpuSpringSim)
        {
            if (nodeGraph.AllNodes.Count <= 0) return;
            RemovePhysics();
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
}