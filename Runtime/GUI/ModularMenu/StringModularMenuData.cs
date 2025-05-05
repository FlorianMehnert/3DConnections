using System;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "New String Setting", menuName = "3DConnections/Modular Menu Setting/String Setting")]
public class StringModularMenuData : ModularMenuData
{
    public string defaultValue = "";
    [SerializeField] private string currentValue = "";
    
    public string Value 
    { 
        get => currentValue;
        set 
        {
            string oldValue = currentValue;
            currentValue = value;
            if (oldValue != currentValue && OnValueChanged != null)
                OnValueChanged(currentValue);
        }
    }
    
    public event Action<string> OnValueChanged;
    
    public override VisualElement CreateSettingElement()
    {
        var container = new VisualElement();
        container.AddToClassList("setting-container");
        
        var titleLabel = new Label(settingName);
        titleLabel.AddToClassList("setting-title");
        
        var textField = new TextField
        {
            value = currentValue,
            tooltip = description
        };

        textField.RegisterValueChangedCallback(evt => {
            Value = evt.newValue;
        });
        
        var descLabel = new Label(description);
        descLabel.AddToClassList("setting-description");
        
        container.Add(titleLabel);
        container.Add(textField);
        container.Add(descLabel);
        
        return container;
    }
    
    public override object GetValue() => Value;
    
    public override void ResetToDefault()
    {
        Value = defaultValue;
    }
    
    private void OnEnable()
    {
        // Initialize value if not set
        if (string.IsNullOrEmpty(currentValue) && !string.IsNullOrEmpty(defaultValue))
            currentValue = defaultValue;
    }
}