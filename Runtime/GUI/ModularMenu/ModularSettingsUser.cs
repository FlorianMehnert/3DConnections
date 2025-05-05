using UnityEngine;

public class ModularSettingsUser : MonoBehaviour
{
    /// <summary>
    /// Register all settings that have a [RegisterModular...]
    /// Above them and add them to the modular settings menu 
    /// </summary>
    protected void RegisterModularSettings()
    {
        var settingsManager = FindFirstObjectByType<ModularSettingsManager>();
        if (settingsManager)
        {
            ModularSettingsRegistrar.RegisterAllSettings(this, settingsManager);
        }
    }
}