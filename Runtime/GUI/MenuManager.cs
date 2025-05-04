using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>
/// Interface for all menu setting items
/// </summary>
public interface IMenuSetting
{
    string Name { get; }
    GameObject CreateUI(Transform parent);
    void UpdateUI();
}

/// <summary>
/// Main controller for the settings menu system
/// </summary>
public class MenuManager : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Transform settingsContainer;
    [SerializeField] private GameObject togglePrefab;
    [SerializeField] private GameObject sliderPrefab;
    [SerializeField] private GameObject dropdownPrefab;
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private GameObject textInputPrefab;
    [SerializeField] private KeyCode toggleMenuKey = KeyCode.Escape;

    private static MenuManager _instance;
    private readonly Dictionary<string, IMenuSetting> _settings = new();
    private bool _isMenuOpen;

    public static MenuManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindFirstObjectByType<MenuManager>();
            if (_instance == null)
            {
                Debug.LogError("No MenuManager found in the scene!");
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleMenuKey))
        {
            ToggleMenu();
        }
    }

    private void ToggleMenu()
    {
        _isMenuOpen = !_isMenuOpen;
        
        if (menuPanel)
        {
            menuPanel.SetActive(_isMenuOpen);
        }
        
        // Optionally pause the game when menu is open
        // Time.timeScale = _isMenuOpen ? 0f : 1f;
    }

    public void RegisterSetting(IMenuSetting setting)
    {
        if (_settings.ContainsKey(setting.Name))
        {
            Debug.LogWarning($"Setting with name '{setting.Name}' already exists! Overriding.");
        }
        
        _settings[setting.Name] = setting;
        
        // If the menu is already active, create UI for this setting
        if (menuPanel != null && menuPanel.activeInHierarchy && settingsContainer != null)
        {
            setting.CreateUI(settingsContainer);
        }
    }

    public void UnregisterSetting(string name)
    {
        if (!_settings.Remove(name)) return;
        RebuildMenu();
    }

    private void RebuildMenu()
    {
        // Clear existing settings UI
        foreach (Transform child in settingsContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Rebuild all settings UI
        foreach (var setting in _settings.Values)
        {
            setting.CreateUI(settingsContainer);
        }
    }

    public GameObject GetSettingPrefab(string settingType)
    {
        switch (settingType)
        {
            case "Toggle":
                return togglePrefab;
            case "Slider":
                return sliderPrefab;
            case "Dropdown":
                return dropdownPrefab;
            case "Button":
                return buttonPrefab;
            case "TextInput":
                return textInputPrefab;
            default:
                Debug.LogError($"Prefab of type {settingType} not found!");
                return null;
        }
    }

    // Helper method to get a setting
    public T GetSetting<T>(string name) where T : class, IMenuSetting
    {
        if (!_settings.TryGetValue(name, out IMenuSetting setting)) return null;
        if (setting is T typedSetting)
        {
            return typedSetting;
        }
        return null;
    }
}

/// <summary>
/// Toggle setting implementation
/// </summary>
public class ToggleSetting : IMenuSetting
{
    public string Name { get; }
    private bool Value { get; set; }
    private readonly Action<bool> _onValueChanged;
    private Toggle _toggleUI;

    public ToggleSetting(string name, bool defaultValue, Action<bool> onValueChanged)
    {
        Name = name;
        Value = defaultValue;
        _onValueChanged = onValueChanged;
    }

    public GameObject CreateUI(Transform parent)
    {
        GameObject toggleObj = Object.Instantiate(MenuManager.Instance.GetSettingPrefab("Toggle"), parent);
        
        // Get components
        _toggleUI = toggleObj.GetComponentInChildren<Toggle>();
        TextMeshProUGUI labelText = toggleObj.GetComponentInChildren<TextMeshProUGUI>();
        
        // Setup
        if (labelText != null)
            labelText.text = Name;
        
        if (_toggleUI != null)
        {
            _toggleUI.isOn = Value;
            _toggleUI.onValueChanged.AddListener(OnToggleChanged);
        }
        
        return toggleObj;
    }

    private void OnToggleChanged(bool newValue)
    {
        Value = newValue;
        _onValueChanged?.Invoke(Value);
    }

    public void UpdateUI()
    {
        if (_toggleUI != null)
        {
            _toggleUI.isOn = Value;
        }
    }
}

/// <summary>
/// Slider setting implementation
/// </summary>
public class SliderSetting : IMenuSetting
{
    public string Name { get; }
    private float Value { get; set; }
    private float MinValue { get; }
    private float MaxValue { get; }
    private readonly Action<float> _onValueChanged;
    private Slider _sliderUI;
    private TextMeshProUGUI _valueText;

    public SliderSetting(string name, float defaultValue, float minValue, float maxValue, Action<float> onValueChanged)
    {
        Name = name;
        Value = defaultValue;
        MinValue = minValue;
        MaxValue = maxValue;
        _onValueChanged = onValueChanged;
    }

    public GameObject CreateUI(Transform parent)
    {
        GameObject sliderObj = Object.Instantiate(MenuManager.Instance.GetSettingPrefab("Slider"), parent);
        
        // Get components
        _sliderUI = sliderObj.GetComponentInChildren<Slider>();
        TextMeshProUGUI labelText = sliderObj.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        _valueText = sliderObj.transform.Find("Value")?.GetComponent<TextMeshProUGUI>();
        
        // Setup
        if (labelText != null)
            labelText.text = Name;
            
        if (_sliderUI != null)
        {
            _sliderUI.minValue = MinValue;
            _sliderUI.maxValue = MaxValue;
            _sliderUI.value = Value;
            _sliderUI.onValueChanged.AddListener(OnSliderChanged);
        }
        
        UpdateValueText();
        
        return sliderObj;
    }

    private void OnSliderChanged(float newValue)
    {
        Value = newValue;
        UpdateValueText();
        _onValueChanged?.Invoke(Value);
    }
    
    private void UpdateValueText()
    {
        if (_valueText != null)
        {
            _valueText.text = Value.ToString("F2");
        }
    }

    public void UpdateUI()
    {
        if (_sliderUI != null)
        {
            _sliderUI.value = Value;
        }
        UpdateValueText();
    }
}

/// <summary>
/// Dropdown setting implementation
/// </summary>
public class DropdownSetting : IMenuSetting
{
    public string Name { get; }
    private int SelectedIndex { get; set; }
    private List<string> Options { get; }
    private readonly Action<int> _onValueChanged;
    private TMP_Dropdown _dropdownUI;

    public DropdownSetting(string name, List<string> options, int defaultIndex, Action<int> onValueChanged)
    {
        Name = name;
        Options = options;
        SelectedIndex = defaultIndex;
        _onValueChanged = onValueChanged;
    }

    public GameObject CreateUI(Transform parent)
    {
        GameObject dropdownObj = Object.Instantiate(MenuManager.Instance.GetSettingPrefab("Dropdown"), parent);
        
        // Get components
        _dropdownUI = dropdownObj.GetComponentInChildren<TMP_Dropdown>();
        TextMeshProUGUI labelText = dropdownObj.GetComponentInChildren<TextMeshProUGUI>();
        
        // Setup
        if (labelText != null)
            labelText.text = Name;
            
        if (_dropdownUI != null)
        {
            _dropdownUI.ClearOptions();
            _dropdownUI.AddOptions(Options);
            _dropdownUI.value = SelectedIndex;
            _dropdownUI.onValueChanged.AddListener(OnDropdownChanged);
        }
        
        return dropdownObj;
    }

    private void OnDropdownChanged(int newIndex)
    {
        SelectedIndex = newIndex;
        _onValueChanged?.Invoke(SelectedIndex);
    }

    public void UpdateUI()
    {
        if (_dropdownUI != null)
        {
            _dropdownUI.value = SelectedIndex;
        }
    }
}

/// <summary>
/// Button setting implementation
/// </summary>
public class ButtonSetting : IMenuSetting
{
    public string Name { get; }
    private readonly Action _onButtonPressed;
    private Button _buttonUI;

    public ButtonSetting(string name, Action onButtonPressed)
    {
        Name = name;
        _onButtonPressed = onButtonPressed;
    }

    public GameObject CreateUI(Transform parent)
    {
        GameObject buttonObj = Object.Instantiate(MenuManager.Instance.GetSettingPrefab("Button"), parent);

        
        // Get components
        _buttonUI = buttonObj.GetComponent<Button>();
        TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        
        // Setup
        if (buttonText != null)
            buttonText.text = Name;
            
        if (_buttonUI != null)
        {
            _buttonUI.onClick.AddListener(() => _onButtonPressed?.Invoke());
        }
        
        return buttonObj;
    }

    public void UpdateUI()
    {
        // Nothing to update for button
    }
}

/// <summary>
/// Text Input setting implementation
/// </summary>
public class TextInputSetting : IMenuSetting
{
    public string Name { get; }
    private string Value { get; set; }
    private readonly Action<string> _onValueChanged;
    private TMP_InputField _inputUI;

    public TextInputSetting(string name, string defaultValue, Action<string> onValueChanged)
    {
        Name = name;
        Value = defaultValue;
        _onValueChanged = onValueChanged;
    }

    public GameObject CreateUI(Transform parent)
    {
        GameObject inputObj = Object.Instantiate(MenuManager.Instance.GetSettingPrefab("Input"), parent);
        
        // Get components
        _inputUI = inputObj.GetComponentInChildren<TMP_InputField>();
        TextMeshProUGUI labelText = inputObj.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        
        // Setup
        if (labelText != null)
            labelText.text = Name;
            
        if (_inputUI != null)
        {
            _inputUI.text = Value;
            _inputUI.onValueChanged.AddListener(OnInputChanged);
        }
        
        return inputObj;
    }

    private void OnInputChanged(string newValue)
    {
        Value = newValue;
        _onValueChanged?.Invoke(Value);
    }

    public void UpdateUI()
    {
        if (_inputUI != null)
        {
            _inputUI.text = Value;
        }
    }
}