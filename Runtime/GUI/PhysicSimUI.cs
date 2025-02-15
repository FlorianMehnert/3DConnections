using UnityEngine;
using UnityEngine.UIElements;

public class PhysicsSimUI : MonoBehaviour
{
    public UIDocument uiDocument;
    public PhysicsSimulationConfiguration physicsConfig;
    private const float MinValue = 0.00001f; // Avoid zero (log(0) is undefined)
    private const float MaxValue = 1f;
    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        var stiffnessSlider = root.Q<Slider>("SliderStiffness");
        var dampingSlider = root.Q<Slider>("SliderDamping");
        var radiusSlider = root.Q<Slider>("SliderRadius");
        var responseSlider = root.Q<Slider>("SliderResponse");
        
        if (physicsConfig == null) return;
        stiffnessSlider.value = physicsConfig.stiffness;
        dampingSlider.value = physicsConfig.damping;
        radiusSlider.value = physicsConfig.colliderRadius;
        responseSlider.value = physicsConfig.collisionResponseStrength;
        stiffnessSlider.RegisterValueChangedCallback(evt => { physicsConfig.stiffness = ConvertToLog(evt.newValue); });
        dampingSlider.RegisterValueChangedCallback(evt => { physicsConfig.damping = evt.newValue; });
        radiusSlider.RegisterValueChangedCallback(evt => { physicsConfig.colliderRadius = evt.newValue; });
        responseSlider.RegisterValueChangedCallback(evt => { physicsConfig.collisionResponseStrength = ConvertToLog(evt.newValue); });
    }
    
    private float ConvertToLog(float linearValue)
    {
        return MinValue * Mathf.Pow(MaxValue / MinValue, linearValue);
    }

    // Convert from logarithmic value (minValue to maxValue) back to linear slider value (0 to 1)
    private float ConvertToLinear(float logValue)
    {
        return Mathf.Log(logValue / MinValue) / Mathf.Log(MaxValue / MinValue);
    }
}