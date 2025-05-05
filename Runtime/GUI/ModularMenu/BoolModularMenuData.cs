using System;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "New Bool Setting", menuName = "3DConnections/Modular Menu Setting/Bool Setting")]
public class BoolModularMenuData : ModularMenuData
{
    public bool defaultValue;
    [SerializeField] private bool currentValue;
    
    public bool Value 
    { 
        get => currentValue;
        set 
        {
            var oldValue = currentValue;
            currentValue = value;
            if (oldValue != currentValue && OnValueChanged != null)
                OnValueChanged(currentValue);
        }
    }
    
    public event Action<bool> OnValueChanged;
    
    public override VisualElement CreateSettingElement()
    {
        var container = new VisualElement();
        container.AddToClassList("setting-container");
        
        var toggle = new Toggle(settingName)
        {
            tooltip = description,
            value = currentValue
        };
        toggle.RegisterValueChangedCallback(evt => Value = evt.newValue);
        
        var label = new Label(description);
        label.AddToClassList("setting-description");
        
        container.Add(toggle);
        container.Add(label);
        
        return container;
    }
    
    public override object GetValue() => Value;
    
    public override void ResetToDefault()
    {
        Value = defaultValue;
    }
    
    private void OnEnable()
    {
        if (currentValue == false && defaultValue)
            currentValue = defaultValue;
    }
}