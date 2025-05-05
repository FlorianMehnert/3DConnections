using System;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "New Float Setting", menuName = "3DConnections/Modular Menu Setting/Float Setting")]
public class FloatModularMenuData : ModularMenuData
{
    public float defaultValue;
    public float minValue;
    public float maxValue = 1f;
    [SerializeField] private float currentValue;
    
    public float Value 
    { 
        get => currentValue;
        set 
        {
            var oldValue = currentValue;
            currentValue = Mathf.Clamp(value, minValue, maxValue);
            if (!Mathf.Approximately(oldValue, currentValue) && OnValueChanged != null)
                OnValueChanged(currentValue);
        }
    }
    
    public event Action<float> OnValueChanged;
    
    public override VisualElement CreateSettingElement()
    {
        var container = new VisualElement();
        container.AddToClassList("setting-container");
        
        var labelContainer = new VisualElement();
        labelContainer.AddToClassList("setting-header");
        
        var titleLabel = new Label(settingName);
        titleLabel.AddToClassList("setting-title");
        
        var valueLabel = new Label(Value.ToString("F2"));
        valueLabel.AddToClassList("setting-value");
        
        labelContainer.Add(titleLabel);
        labelContainer.Add(valueLabel);
        
        var slider = new Slider(minValue, maxValue)
        {
            value = currentValue,
            lowValue = minValue,
            highValue = maxValue,
            tooltip = description
        };

        slider.RegisterValueChangedCallback(evt => {
            Value = evt.newValue;
            valueLabel.text = evt.newValue.ToString("F2");
        });
        
        var descLabel = new Label(description);
        descLabel.AddToClassList("setting-description");
        
        container.Add(labelContainer);
        container.Add(slider);
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
        if (currentValue == 0f && defaultValue != 0f)
            currentValue = defaultValue;
    }
}