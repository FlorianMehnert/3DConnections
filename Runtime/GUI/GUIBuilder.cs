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
    private GameObject _executeNodeSpawnButton;
    private UnityAction _toExecute;
    [SerializeField] private OverlaySceneScriptableObject overlaySceneConfig;
    [SerializeField] private NodeGraphScriptableObject nodeGraph;
    [SerializeField] private RemovePhysicsEvent removePhysicsEvent;
    [SerializeField] private ClearEvent clearEvent;
    [SerializeField] private LayoutParameters layoutParameters;
    private UnityAction[] _actions;
    private float _currentYCoordinate;

    private void OnEnable()
    {
        clearEvent.TriggerEvent();
        nodeGraph.Clear();
    }
    private void OnFileBrowserOpen()
    {
        path = StandaloneFileBrowser.OpenFolderPanel("Open File", "/home/florian/Bilder", false);
    }
}