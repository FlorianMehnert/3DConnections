#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

// Custom editor for SettingData ScriptableObjects
[CustomEditor(typeof(ModularMenuData), true)]
public class SettingDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("settingName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("category"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Setting Values", EditorStyles.boldLabel);
        
        // Draw the rest of the inspector
        DrawPropertiesExcluding(serializedObject, "m_Script", "settingName", "description", "category");
        
        serializedObject.ApplyModifiedProperties();
    }
}

// Editor window to create settings assets
public class SettingsCreationWindow : EditorWindow
{
    private string _settingName = "New Setting";
    private string _description = "";
    private string _category = "General";
    private int _selectedType;
    private readonly string[] _settingTypes = { "Bool Setting", "Float Setting", "String Setting", "Dropdown Setting" };
    
    // For Float Settings
    private float _defaultFloatValue;
    private float _minValue;
    private float _maxValue = 1f;
    
    // For Bool Settings
    private bool _defaultBoolValue;
    
    // For String Settings
    private string _defaultStringValue = "";
    
    // For Dropdown Settings
    private string _dropdownOptions = "Option 1,Option 2,Option 3";
    private int _defaultDropdownIndex;
    
    // Path for saving assets
    private string _savePath = "Assets/Settings";
    
    [MenuItem("Tools/3DConnections/Settings System/Create Setting")]
    public static void ShowWindow()
    {
        GetWindow<SettingsCreationWindow>("Create Setting");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Create New Setting", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        
        // Basic setting properties
        _settingName = EditorGUILayout.TextField("Setting Name", _settingName);
        _description = EditorGUILayout.TextField("Description", _description);
        _category = EditorGUILayout.TextField("Category", _category);
        
        EditorGUILayout.Space();
        
        // Setting type
        _selectedType = EditorGUILayout.Popup("Setting Type", _selectedType, _settingTypes);
        
        EditorGUILayout.Space();
        
        // Type-specific properties
        switch (_selectedType)
        {
            case 0: // Bool
                _defaultBoolValue = EditorGUILayout.Toggle("Default Value", _defaultBoolValue);
                break;
                
            case 1: // Float
                _defaultFloatValue = EditorGUILayout.FloatField("Default Value", _defaultFloatValue);
                _minValue = EditorGUILayout.FloatField("Min Value", _minValue);
                _maxValue = EditorGUILayout.FloatField("Max Value", _maxValue);
                break;
                
            case 2: // String
                _defaultStringValue = EditorGUILayout.TextField("Default Value", _defaultStringValue);
                break;
                
            case 3: // Dropdown
                EditorGUILayout.LabelField("Options (comma separated)");
                _dropdownOptions = EditorGUILayout.TextArea(_dropdownOptions, GUILayout.Height(60));
                
                string[] options = _dropdownOptions.Split(',');
                string[] displayOptions = new string[options.Length];
                for (int i = 0; i < options.Length; i++)
                {
                    displayOptions[i] = $"{i}: {options[i].Trim()}";
                }
                
                _defaultDropdownIndex = EditorGUILayout.Popup("Default Option", _defaultDropdownIndex, displayOptions);
                _defaultDropdownIndex = Mathf.Clamp(_defaultDropdownIndex, 0, options.Length - 1);
                break;
        }
        
        EditorGUILayout.Space();
        
        // Save path
        _savePath = EditorGUILayout.TextField("Save Path", _savePath);
        
        if (GUILayout.Button("Create Setting"))
        {
            CreateSetting();
        }
    }
    
    private void CreateSetting()
    {
        // Create directory if it doesn't exist
        if (!Directory.Exists(_savePath))
        {
            Directory.CreateDirectory(_savePath);
        }
        
        // Sanitize file name
        string fileName = _settingName.Replace(" ", "").Replace("/", "_").Replace("\\", "_");
        string assetPath = $"{_savePath}/{fileName}.asset";
        
        // Create the appropriate setting type
        ScriptableObject settingAsset = null;
        
        switch (_selectedType)
        {
            case 0: // Bool
                var boolSetting = CreateInstance<BoolModularMenuData>();
                boolSetting.settingName = _settingName;
                boolSetting.description = _description;
                boolSetting.category = _category;
                boolSetting.defaultValue = _defaultBoolValue;
                settingAsset = boolSetting;
                break;
                
            case 1: // Float
                var floatSetting = CreateInstance<FloatModularMenuData>();
                floatSetting.settingName = _settingName;
                floatSetting.description = _description;
                floatSetting.category = _category;
                floatSetting.defaultValue = _defaultFloatValue;
                floatSetting.minValue = _minValue;
                floatSetting.maxValue = _maxValue;
                settingAsset = floatSetting;
                break;
                
            case 2: // String
                var stringSetting = CreateInstance<StringModularMenuData>();
                stringSetting.settingName = _settingName;
                stringSetting.description = _description;
                stringSetting.category = _category;
                stringSetting.defaultValue = _defaultStringValue;
                settingAsset = stringSetting;
                break;
                
            case 3: // Dropdown
                var dropdownSetting = CreateInstance<DropdownModularMenuData>();
                dropdownSetting.settingName = _settingName;
                dropdownSetting.description = _description;
                dropdownSetting.category = _category;
                
                string[] options = _dropdownOptions.Split(',');
                dropdownSetting.options.Clear();
                foreach (var option in options)
                {
                    dropdownSetting.options.Add(option.Trim());
                }
                dropdownSetting.defaultValueIndex = _defaultDropdownIndex;
                settingAsset = dropdownSetting;
                break;
        }
        
        // Create the asset
        if (settingAsset != null)
        {
            AssetDatabase.CreateAsset(settingAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Select the newly created asset
            Selection.activeObject = settingAsset;
            
            EditorUtility.DisplayDialog("Setting Created", $"Setting '{_settingName}' created at {assetPath}", "OK");
        }
    }
}

// Custom editor for SettingsManager
[CustomEditor(typeof(ModularSettingsManager))]
public class SettingsManagerEditor : Editor
{
    private SerializedProperty _uiDocumentProperty;
    private SerializedProperty _defaultSettingsProperty;
    
    private void OnEnable()
    {
        _uiDocumentProperty = serializedObject.FindProperty("uiDocument");
        _defaultSettingsProperty = serializedObject.FindProperty("defaultSettings");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.PropertyField(_uiDocumentProperty);
        
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Default Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_defaultSettingsProperty, true);
        
        serializedObject.ApplyModifiedProperties();
        
        EditorGUILayout.Space();
        
        // Buttons for creating UI assets
        if (GUILayout.Button("Generate UI Template Files"))
        {
            CreateUITemplateFiles();
        }
        
        // Button to open setting creation window
        if (GUILayout.Button("Create New Setting"))
        {
            SettingsCreationWindow.ShowWindow();
        }
    }
    
    private void CreateUITemplateFiles()
    {
        string folder = EditorUtility.SaveFolderPanel("Select folder for UI files", "Assets", "UI");
        
        if (string.IsNullOrEmpty(folder))
            return;
            
        // Convert absolute path to project-relative path
        if (folder.StartsWith(Application.dataPath))
        {
            folder = "Assets" + folder.Substring(Application.dataPath.Length);
        }
        else
        {
            EditorUtility.DisplayDialog("Invalid Path", "Please select a folder within your project's Assets folder.", "OK");
            return;
        }
        
        // Create UXML file
        string uxmlContent = @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:uie=""UnityEditor.UIElements"">
    <Style src=""SettingsStyles.uss"" />
    <ui:VisualElement name=""settings-window"" class=""settings-window"">
        <ui:Label text=""Settings"" class=""settings-title"" />
        <ui:ScrollView class=""settings-scroll"">
            <ui:VisualElement name=""settings-container"" class=""settings-container"" />
        </ui:ScrollView>
        <ui:VisualElement class=""settings-actions"">
            <ui:Button name=""save-settings"" text=""Save"" class=""settings-button"" />
            <ui:Button name=""cancel-settings"" text=""Cancel"" class=""settings-button"" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>";

        // Create USS file
        string ussContent = @".settings-window {
    width: 100%;
    height: 100%;
    background-color: rgb(42, 42, 42);
    padding: 10px;
}

.settings-title {
    font-size: 24px;
    -unity-font-style: bold;
    color: rgb(255, 255, 255);
    margin-bottom: 20px;
}

.settings-scroll {
    flex-grow: 1;
    margin-bottom: 10px;
}

.settings-container {
    padding: 10px;
}

.settings-category {
    margin-bottom: 15px;
    background-color: rgb(60, 60, 60);
    border-radius: 4px;
    padding: 5px;
}

.settings-category > Toggle {
    padding: 5px;
    font-size: 16px;
    -unity-font-style: bold;
}

.settings-category > VisualElement {
    margin-left: 15px;
}

.setting-container {
    margin: 8px 0;
    padding: 8px;
    background-color: rgb(50, 50, 50);
    border-radius: 4px;
}

.setting-header {
    flex-direction: row;
    justify-content: space-between;
    margin-bottom: 5px;
}

.setting-title {
    font-size: 14px;
    -unity-font-style: bold;
    margin-bottom: 5px;
}

.setting-value {
    font-size: 12px;
    color: rgb(200, 200, 200);
}

.setting-description {
    font-size: 12px;
    color: rgb(180, 180, 180);
    margin-top: 5px;
    white-space: normal;
}

Slider {
    margin: 5px 0;
}

TextField {
    margin: 5px 0;
}

Toggle {
    margin: 5px 0;
}

DropdownField {
    margin: 5px 0;
}

.settings-actions {
    flex-direction: row;
    justify-content: flex-end;
    padding: 10px;
}

.settings-button {
    width: 100px;
    height: 30px;
    margin-left: 10px;
    background-color: rgb(78, 122, 199);
    color: white;
    border-radius: 4px;
}

.settings-button:hover {
    background-color: rgb(100, 140, 220);
}

.reset-button {
    margin-top: 20px;
    padding: 8px;
    background-color: rgb(190, 70, 70);
    color: white;
    border-radius: 4px;
}

.reset-button:hover {
    background-color: rgb(220, 90, 90);
}";

        // Write files
        string uxmlPath = $"{folder}/ModularMenu.uxml";
        string ussPath = $"{folder}/ModularMenu.uss";
        
        File.WriteAllText(uxmlPath, uxmlContent);
        File.WriteAllText(ussPath, ussContent);
        
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Files Created", $"UI template files created at {folder}", "OK");
    }
}
#endif