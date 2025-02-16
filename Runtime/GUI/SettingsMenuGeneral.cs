using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

public class SettingsMenuGeneral : MonoBehaviour
{
    public UIDocument uiDocument;
    private VisualElement _panel;
    private Button _clearButton;
    private Button _removePhysicsButton;
    
    private DropdownField _sceneDropdown;
    private DropdownField _layoutDropdown;
    
    [SerializeField] private RemovePhysicsEvent removePhysicsEvent;
    [SerializeField] private ClearEvent clearEvent;
    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _panel = root.Q<VisualElement>("Panel");
        _clearButton = root.Q<Button>("Clear");
        _removePhysicsButton = root.Q<Button>("RemovePhysics");
        _sceneDropdown = root.Q<DropdownField>("DropdownScene");
        _layoutDropdown = root.Q<DropdownField>("DropdownLayout");
        
        if (_clearButton != null)
            _clearButton.clicked += () => clearEvent.TriggerEvent();
        if (_removePhysicsButton != null)
            _removePhysicsButton.clicked += () => removePhysicsEvent.TriggerEvent();
        
        if (removePhysicsEvent != null)
            removePhysicsEvent.OnEventTriggered += HandleEvent;
        
        if (_sceneDropdown != null)
            PopulateSceneDropdown();
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
    
    // private void CreateSceneDropdown()
    // {
    //     // Instantiate the dropdown prefab as child of canvas
    //     _sceneDropdown
    //     _sceneDropdownInstance = dropdownInstance;
    //     _sceneDropdownInstance.gameObject.SetActive(true);
    //
    //     // Get the dropdown component (works with both standard and TMP dropdowns)
    //     var tmpDropdown = dropdownInstance.GetComponent<TMP_Dropdown>();
    //
    //     // Clear existing options
    //     if (tmpDropdown)
    //     {
    //         tmpDropdown.ClearOptions();
    //     }
    //
    //     // Get all scenes in build settings
    //     var sceneCount = SceneManager.sceneCountInBuildSettings;
    //     var sceneOptions = new List<string>();
    //
    //     for (var i = 0; i < sceneCount; i++)
    //     {
    //         var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
    //         var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
    //         sceneOptions.Add(sceneName);
    //     }
    //
    //
    //     if (tmpDropdown)
    //     {
    //         tmpDropdown.enabled = true;
    //         tmpDropdown.AddOptions(sceneOptions);
    //     }
    //
    //     if (sceneOptions.Count > 0)
    //     {
    //         var scene = SceneManager.GetSceneByName(sceneOptions.First());
    //         Debug.Log(scene.name + " is set to be analyzed");
    //         var sceneHandler = GetComponent<SceneHandler>();
    //         if (sceneHandler)
    //             sceneHandler.analyzeScene = scene;
    //     }
    //
    //     var rectTransform = dropdownInstance.GetComponent<RectTransform>();
    //     rectTransform.anchoredPosition = GetButtonPosition(dropdownInstance.gameObject);
    //     ScriptableObject.CreateInstance<SceneReference>();
    //     dropdownInstance.onValueChanged.AddListener(OnSceneDropdownValueChanged);
    // }
    

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