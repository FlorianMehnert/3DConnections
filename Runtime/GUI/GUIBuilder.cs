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
    private TMP_Dropdown _sceneDropdownInstance;
    private TMP_Dropdown _nodeGraphDropdownInstance;
    private GameObject _clearButton;
    private GameObject _removePhysicsButton;
    private GameObject _executeNodeSpawnButton;
    private UnityAction _toExecute;
    [SerializeField] private OverlaySceneScriptableObject overlaySceneConfig;
    [SerializeField] private NodeGraphScriptableObject nodeGraph;
    [SerializeField] private RemovePhysicsEvent removePhysicsEvent;
    private UnityAction[] _actions;

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
        CreateNodeGraphDropdown();
        CreateButtons();

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
        _sceneDropdownInstance = dropdownInstance;
        _sceneDropdownInstance.gameObject.SetActive(true);

        // Get the dropdown component (works with both standard and TMP dropdowns)
        var tmpDropdown = dropdownInstance.GetComponent<TMP_Dropdown>();

        // Clear existing options
        if (tmpDropdown != null)
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


        if (tmpDropdown != null)
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
        rectTransform.anchoredPosition = GetButtonPosition();
        ScriptableObject.CreateInstance<SceneReference>();
        dropdownInstance.onValueChanged.AddListener(OnSceneDropdownValueChanged);
    }

    private void CreateNodeGraphDropdown()
    {
        var dropdownInstance = Instantiate(dropdownPrefab, uiCanvas.transform);
        _nodeGraphDropdownInstance = dropdownInstance;
        _nodeGraphDropdownInstance.gameObject.SetActive(true);
        var tmpDropdown = dropdownInstance.GetComponent<TMP_Dropdown>();
        if (tmpDropdown != null)
        {
            tmpDropdown.ClearOptions();
        }

        if (tmpDropdown != null)
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
        rectTransform.anchoredPosition = GetButtonPosition();
        ScriptableObject.CreateInstance<SceneReference>();
        dropdownInstance.onValueChanged.AddListener(OnNodeGraphDropdownChanged);
    }

    private void StaticLayout()
    {
        OnSceneDropdownValueChanged(_sceneDropdownInstance.value);
        _sceneAnalyzer.AnalyzeScene();
        NodeLayoutManagerV2.LayoutForest();
    }


    private void CreateButtons()
    {
        nodeGraph.Initialize();
        _executeNodeSpawnButton = CreateButton("Execute Action", 14, (() =>
        {
            StaticLayout();
            ChangeButtonEnabled(_removePhysicsButton, true);
            ChangeButtonEnabled(_clearButton, true);
        }), disableAfterClick: true);

        _removePhysicsButton = CreateButton("Remove Physics", 14, () =>
        {
            removePhysicsEvent.TriggerEvent();
            var types = new List<Type>
            {
                typeof(SpringJoint2D),
                typeof(Rigidbody2D)
            };
            var parentObject = SceneHandler.GetParentObject();

            if (parentObject == null)
                return;
            nodeGraph.NodesRemoveComponents(types, SceneHandler.GetNodesByTransform());
        }, isEnabled: false, disableAfterClick: true);

        var sceneAnalyzer = GetComponent<SceneAnalyzer>();
        if (sceneAnalyzer != null)
        {
            _clearButton = CreateButton("Clear", 14, () =>
            {
                sceneAnalyzer.ClearNodes();
                nodeGraph.Initialize();
                ChangeButtonEnabled(_executeNodeSpawnButton.gameObject, true);
                ChangeButtonEnabled(_removePhysicsButton.gameObject, false);
            }, isEnabled: false, disableAfterClick: true);
        }
    }

    private static void ChangeButtonEnabled(GameObject buttonGameObject, bool isEnabled)
    {
        var image = buttonGameObject.GetComponent<Image>();
        var button = buttonGameObject.GetComponent<Button>();
        if (buttonGameObject == null) return;

        image.color = isEnabled ? Color.white : Color.gray;
        button.enabled = isEnabled;
    }

    private GameObject CreateButton(string text, int fontSize, UnityAction onClick, Vector2 anchoredPosition = default, bool isEnabled = true, bool disableAfterClick = false)
    {
        if (anchoredPosition == default)
        {
            anchoredPosition = GetButtonPosition();
        }

        var browserButtonInstance = Instantiate(buttonPrefab, uiCanvas.transform);
        var buttonComponent = browserButtonInstance.GetComponent<Button>();
        buttonComponent.enabled = isEnabled;
        buttonComponent.image.color = isEnabled ? Color.white : Color.gray;
        var rectTransform = browserButtonInstance.GetComponent<RectTransform>();

        rectTransform.anchoredPosition = anchoredPosition;
        var buttonText = browserButtonInstance.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = text;
            buttonText.fontSize = fontSize;
        }

        if (buttonComponent == null) return browserButtonInstance;
        buttonComponent.onClick.AddListener(onClick);
        buttonComponent.onClick.AddListener(() =>
        {
            onClick.Invoke();
            if (disableAfterClick)
                ChangeButtonEnabled(browserButtonInstance, false);
        });

        return browserButtonInstance;
    }

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
}