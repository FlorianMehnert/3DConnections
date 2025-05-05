using System;
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
    }
    
    public void RegisterSetting(ModularMenuData modularMenu)
    {
        if (modularMenu == null) return;
        
        // Add to all settings lists
        if (_allSettings.Contains(modularMenu)) return;
        _allSettings.Add(modularMenu);
            
        // Add to a category map
        if (!_categorySettingsMap.ContainsKey(modularMenu.category))
        {
            _categorySettingsMap[modularMenu.category] = new List<ModularMenuData>();
        }
            
        _categorySettingsMap[modularMenu.category].Add(modularMenu);
    }
    
    // Method to unregister a setting
    public void UnregisterSetting(ModularMenuData modularMenu)
    {
        if (modularMenu == null) return;
        
        _allSettings.Remove(modularMenu);
        
        if (_categorySettingsMap.ContainsKey(modularMenu.category))
        {
            _categorySettingsMap[modularMenu.category].Remove(modularMenu);
            
            // Remove category if empty
            if (_categorySettingsMap[modularMenu.category].Count == 0)
            {
                _categorySettingsMap.Remove(modularMenu.category);
            }
        }
        
        // Refresh UI if needed
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            GenerateSettingsUI();
        }
    }
    
    // Generate the settings UI
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
        
        // Clear existing settings
        settingsContainer.Clear();
        
        // Create categories and add settings
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
        
        // Add reset button
        var resetButton = new Button(ResetAllSettings) { text = "Reset All Settings" };
        resetButton.AddToClassList("reset-button");
        settingsContainer.Add(resetButton);
    }
    
    // Method to reset all settings to their default values
    private void ResetAllSettings()
    {
        foreach (var setting in _allSettings)
        {
            setting.ResetToDefault();
        }
        
        // Refresh UI
        GenerateSettingsUI();
    }
    
    // Save settings to PlayerPrefs
    public void SaveSettings()
    {
        foreach (var setting in _allSettings)
        {
            var key = $"Setting_{setting.name}";
            var value = setting.GetValue();
            
            switch (value)
            {
                case bool boolValue:
                    PlayerPrefs.SetInt(key, boolValue ? 1 : 0);
                    break;
                case float floatValue:
                    PlayerPrefs.SetFloat(key, floatValue);
                    break;
                case int intValue:
                    PlayerPrefs.SetInt(key, intValue);
                    break;
                case string stringValue:
                    PlayerPrefs.SetString(key, stringValue);
                    break;
            }
        }
        
        PlayerPrefs.Save();
    }
    
    // Load settings from PlayerPrefs
    public void LoadSettings()
    {
        foreach (var setting in _allSettings)
        {
            var key = $"Setting_{setting.name}";

            switch (setting)
            {
                case BoolModularMenuData boolSetting:
                {
                    if (PlayerPrefs.HasKey(key))
                        boolSetting.Value = PlayerPrefs.GetInt(key) == 1;
                    break;
                }
                case FloatModularMenuData floatSetting:
                {
                    if (PlayerPrefs.HasKey(key))
                        floatSetting.Value = PlayerPrefs.GetFloat(key);
                    break;
                }
                case DropdownModularMenuData dropdownSetting:
                {
                    if (PlayerPrefs.HasKey(key))
                        dropdownSetting.SelectedIndex = PlayerPrefs.GetInt(key);
                    break;
                }
                case StringModularMenuData stringSetting:
                {
                    if (PlayerPrefs.HasKey(key))
                        stringSetting.Value = PlayerPrefs.GetString(key);
                    break;
                }
            }
        }
        
        // Refresh UI
        GenerateSettingsUI();
    }
}