using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class ModularSettingsManager : MonoBehaviour, IMenu
{
    public UIDocument uiDocument;
    public List<ModularMenuData> defaultSettings = new List<ModularMenuData>();
    
    [Header("Menu Settings")]
    public KeyCode menuKeybind = KeyCode.F4;
    public bool showOnStart = true;

    private readonly Dictionary<string, List<ModularMenuData>> _categorySettingsMap = new Dictionary<string, List<ModularMenuData>>();
    private readonly List<ModularMenuData> _allSettings = new List<ModularMenuData>();
    private VisualElement _settingsWindow;
    private Button _resetButton;
    private bool _isMenuVisible = true;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("ModularSettingsManager requires a UIDocument component");
                enabled = false;
            }
        }
    }
    
    private void Start()
    {
        // Initialize UI
        try
        {
            InitializeUI();
            
            // Register this menu with the MenuManager
            MenuManager.Instance.RegisterMenu(menuKeybind, this);
            
            // Set initial visibility
            if (!showOnStart)
            {
                OnMenuClose();
            }
            else
            {
                OnMenuOpen();
            }
            
            Debug.Log($"ModularSettingsManager initialized with keybind {menuKeybind}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing ModularSettingsManager: {e.Message}\n{e.StackTrace}");
        }
    }

    private void InitializeUI()
    {
        // Register settings
        foreach (var setting in defaultSettings)
        {
            RegisterSetting(setting);
        }

        // Find UI elements
        var rootElement = uiDocument.rootVisualElement;
        _settingsWindow = rootElement.Q<VisualElement>("settings-window");
        
        if (_settingsWindow == null)
        {
            Debug.LogError("Could not find 'settings-window' element in the UI Document");
            return;
        }
        
        _resetButton = rootElement.Q<Button>("reset-button");
        if (_resetButton != null)
        {
            _resetButton.clicked += ResetAllSettings;
        }
        
        // Generate settings UI
        GenerateSettingsUI();
    }

    public void RegisterSetting(ModularMenuData modularMenu)
    {
        if (!modularMenu) return;

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
        if (!uiDocument || uiDocument.rootVisualElement == null) return;

        var root = uiDocument.rootVisualElement;
        var settingsContainer = root.Q<VisualElement>("settings-container");

        if (settingsContainer == null)
        {
            Debug.LogError("Settings container not found in UI Document. Make sure your UI has an element with the name 'settings-container'");
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
    
    public void OnMenuOpen()
    {
        if (_settingsWindow == null)
        {
            Debug.LogError("Settings window not found. Make sure your UI has an element with the name 'settings-window'");
            return;
        }
        
        _settingsWindow.RemoveFromClassList("hidden");
        _settingsWindow.style.display = DisplayStyle.Flex;
        _isMenuVisible = true;
        ScriptableObjectInventory.Instance.menuState.modularMenuOpen = true;
    }
    
    public void OnMenuClose()
    {
        if (_settingsWindow == null)
        {
            Debug.LogError("Settings window not found. Make sure your UI has an element with the name 'settings-window'");
            return;
        }
        
        _settingsWindow.AddToClassList("hidden");
        _settingsWindow.style.display = DisplayStyle.None;
        _isMenuVisible = false;
        ScriptableObjectInventory.Instance.menuState.modularMenuOpen = false;
        
    }
    
    // Toggle menu visibility directly (for testing)
    public void ToggleMenu()
    {
        if (_isMenuVisible)
        {
            OnMenuClose();
        }
        else
        {
            OnMenuOpen();
        }
    }
    
    // For debugging - toggle menu with a key press without going through MenuManager
    private void Update()
    {
        // Only for debugging - remove in production
        if (!Input.GetKeyDown(KeyCode.F12)) return;
        Debug.Log("Debug toggle menu");
        ToggleMenu();
    }
}