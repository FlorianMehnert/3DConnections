using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;


public class ModularSettingsManager : MonoBehaviour
{
    public UIDocument uiDocument;
    public List<ModularMenuData> defaultSettings = new();

    private readonly Dictionary<string, List<ModularMenuData>> _categorySettingsMap = new();
    private readonly List<ModularMenuData> _allSettings = new();
    private VisualElement _settingsWindow;
    private Button _cancelButton;
    private Button _resetButton;
    private bool _isTransitioning;
    [SerializeField] private MenuState menuState;

    private void Awake()
    {
        foreach (var setting in defaultSettings)
        {
            RegisterSetting(setting);
        }

        GenerateSettingsUI();
    }

    private void OnEnable()
    {
        GenerateSettingsUI();
        try
        {
            GrabUIElements(uiDocument.rootVisualElement);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F4) && !_isTransitioning)
        {
            ToggleMenu();
        }
    }
    
    private void GrabUIElements(VisualElement root)
    {
        _settingsWindow = root.Q<VisualElement>("settings-window");
        _cancelButton = root.Q<Button>("cancel-button");
        _resetButton = root.Q<Button>("reset-button");
        _resetButton.clicked += ResetAllSettings;
        _cancelButton.clicked += HideMenu;
    }

    private void ToggleMenu()
    {
        if (_settingsWindow.ClassListContains("hidden"))
            ShowMenu();
        else
            HideMenu();
    }
    private void ShowMenu()
    {
        _settingsWindow.style.display = DisplayStyle.Flex;
        _settingsWindow.RemoveFromClassList("hidden");
        menuState.modularMenuOpen = true;
    }
    
    private void HideMenu()
    {
        _isTransitioning = true;
        StartCoroutine(HideMenuAfterDelay());
        menuState.modularMenuOpen = false;
    }

    private IEnumerator HideMenuAfterDelay()
    {
        _settingsWindow.AddToClassList("hidden");
        yield return new WaitForSeconds(0.3f);
        _settingsWindow.style.display = DisplayStyle.None;
        _isTransitioning = false;
    }

    public void RegisterSetting(ModularMenuData modularMenu)
    {
        if (modularMenu == null) return;

        if (_allSettings.Contains(modularMenu)) return;
        _allSettings.Add(modularMenu);

        if (!_categorySettingsMap.ContainsKey(modularMenu.category))
        {
            _categorySettingsMap[modularMenu.category] = new List<ModularMenuData>();
        }

        _categorySettingsMap[modularMenu.category].Add(modularMenu);
    }

    private void GenerateSettingsUI()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        var root = uiDocument.rootVisualElement;
        var settingsContainer = root.Q<VisualElement>("settings-container");

        if (settingsContainer == null)
        {
            Debug.LogError("Settings container not found in UI Document");
            return;
        }

        settingsContainer.Clear();
        
        foreach (var category in _categorySettingsMap.Keys)
        {
            var categoryContainer = new Foldout
            {
                text = category
            };
            categoryContainer.AddToClassList("settings-category");
            categoryContainer.value = true; // Expanded by default

            foreach (var settingElement in _categorySettingsMap[category].Select(setting => setting.CreateSettingElement()))
            {
                categoryContainer.Add(settingElement);
            }

            settingsContainer.Add(categoryContainer);
        }
    }

    private void ResetAllSettings()
    {
        foreach (var setting in _allSettings)
        {
            setting.ResetToDefault();
        }

        GenerateSettingsUI();
    }
}