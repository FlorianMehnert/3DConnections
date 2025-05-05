// Dropdown/Enum setting type

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "New Dropdown Setting", menuName = "3DConnections/Modular Menu Setting/Dropdown Setting")]
public class DropdownModularMenuData : ModularMenuData
{
    public List<string> options = new();
    public int defaultValueIndex;
    [SerializeField] private int currentValueIndex;
    
    public int SelectedIndex 
    { 
        get => currentValueIndex;
        set 
        {
            int oldValue = currentValueIndex;
            currentValueIndex = Mathf.Clamp(value, 0, options.Count - 1);
            if (oldValue != currentValueIndex && OnValueChanged != null)
                OnValueChanged(currentValueIndex);
        }
    }
    
    public string SelectedValue => options.Count > currentValueIndex ? options[currentValueIndex] : string.Empty;
    
    public event Action<int> OnValueChanged;
    
    public override VisualElement CreateSettingElement()
    {
        var container = new VisualElement();
        container.AddToClassList("setting-container");
        
        var titleLabel = new Label(settingName);
        titleLabel.AddToClassList("setting-title");
        
        var dropdown = new DropdownField
        {
            choices = options,
            index = currentValueIndex,
            tooltip = description
        };

        dropdown.RegisterValueChangedCallback(evt => {
            SelectedIndex = dropdown.index;
        });
        
        var descLabel = new Label(description);
        descLabel.AddToClassList("setting-description");
        
        container.Add(titleLabel);
        container.Add(dropdown);
        container.Add(descLabel);
        
        return container;
    }
    
    public override object GetValue() => SelectedIndex;
    
    public override void ResetToDefault()
    {
        SelectedIndex = defaultValueIndex;
    }
    
    private void OnEnable()
    {
        // Initialize value if not set
        if (options.Count > 0 && (currentValueIndex < 0 || currentValueIndex >= options.Count))
            currentValueIndex = defaultValueIndex;
    }
}