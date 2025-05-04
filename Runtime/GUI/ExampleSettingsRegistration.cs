using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Example script showing how to register different types of settings
/// </summary>
public class ExampleSettingsRegistration : MonoBehaviour
{
    // Reference to the menu manager in the scene (needed only for setup purposes)
    [SerializeField] private MenuManager menuManager;
    
    // Game settings
    private float _musicVolume = 0.75f;
    private bool _fullscreenMode = true;
    private int _graphicsQuality = 1;
    private string _playerName = "Player";
    
    private void Start()
    {
        // Register settings
        RegisterGameSettings();
        RegisterDebugSettings();
    }
    
    private void RegisterGameSettings()
    {
        // Volume slider
        SliderSetting musicVolume = new SliderSetting(
            "Music Volume",
            _musicVolume,
            0f,
            1f,
            (value) => {
                _musicVolume = value;
                SetMusicVolume(value);
            }
        );
        MenuManager.Instance.RegisterSetting(musicVolume);
        
        // Fullscreen toggle
        ToggleSetting fullscreen = new ToggleSetting(
            "Fullscreen Mode",
            _fullscreenMode,
            (value) => {
                _fullscreenMode = value;
                SetFullscreen(value);
            }
        );
        MenuManager.Instance.RegisterSetting(fullscreen);
        
        // Graphics quality dropdown
        List<string> qualityOptions = new List<string> { "Low", "Medium", "High", "Ultra" };
        DropdownSetting graphicsQuality = new DropdownSetting(
            "Graphics Quality",
            qualityOptions,
            _graphicsQuality,
            (index) => {
                _graphicsQuality = index;
                SetGraphicsQuality(index);
            }
        );
        MenuManager.Instance.RegisterSetting(graphicsQuality);
        
        // Player name input
        TextInputSetting playerName = new TextInputSetting(
            "Player Name",
            _playerName,
            (name) => {
                _playerName = name;
                SetPlayerName(name);
            }
        );
        MenuManager.Instance.RegisterSetting(playerName);
        
        // Save button
        ButtonSetting saveButton = new ButtonSetting(
            "Save Settings",
            () => {
                SaveSettings();
            }
        );
        MenuManager.Instance.RegisterSetting(saveButton);
    }
    
    private void RegisterDebugSettings()
    {
        // Example of dynamically adding debug settings that only developers would use
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        ToggleSetting showFPS = new ToggleSetting(
            "Show FPS Counter",
            false,
            (value) => {
                // Implementation to show/hide FPS counter
                Debug.Log($"FPS Counter: {(value ? "Enabled" : "Disabled")}");
            }
        );
        MenuManager.Instance.RegisterSetting(showFPS);
        
        ButtonSetting resetProgress = new ButtonSetting(
            "Reset Game Progress",
            () => {
                // Implementation to reset game progress
                Debug.Log("Game progress reset!");
            }
        );
        MenuManager.Instance.RegisterSetting(resetProgress);
        #endif
    }
    
    // Implementation of settings application
    private void SetMusicVolume(float volume)
    {
        Debug.Log($"Setting music volume to {volume}");
        // Apply volume to your audio mixer or audio sources
    }
    
    private void SetFullscreen(bool isFullscreen)
    {
        Debug.Log($"Setting fullscreen mode to {isFullscreen}");
        Screen.fullScreen = isFullscreen;
    }
    
    private void SetGraphicsQuality(int qualityIndex)
    {
        Debug.Log($"Setting graphics quality to {qualityIndex}");
        QualitySettings.SetQualityLevel(qualityIndex);
    }
    
    private void SetPlayerName(string name)
    {
        Debug.Log($"Setting player name to {name}");
        // Store player name in player prefs or save system
    }
    
    private void SaveSettings()
    {
        Debug.Log("Saving all settings...");
        // Implementation to save all settings to PlayerPrefs or custom save system
        PlayerPrefs.SetFloat("MusicVolume", _musicVolume);
        PlayerPrefs.SetInt("Fullscreen", _fullscreenMode ? 1 : 0);
        PlayerPrefs.SetInt("GraphicsQuality", _graphicsQuality);
        PlayerPrefs.SetString("PlayerName", _playerName);
        PlayerPrefs.Save();
        
        Debug.Log("Settings saved successfully!");
    }
}