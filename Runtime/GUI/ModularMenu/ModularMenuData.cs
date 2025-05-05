using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "New Setting", menuName = "3DConnections/Modular Menu Setting/Base Setting")]
public abstract class ModularMenuData : ScriptableObject
{
    public string settingName;
    public string description;
    public string category = "General";
    
    // Abstract method that each setting type will implement to create its UI element
    public abstract VisualElement CreateSettingElement();
    
    // Additional method to get the current value for serialization
    public abstract object GetValue();
    
    // Method to reset setting to default value
    public abstract void ResetToDefault();
}