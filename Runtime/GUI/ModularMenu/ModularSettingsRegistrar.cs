using System;
using System.Reflection;
using UnityEngine;


public static class ModularSettingsRegistrar
{
    public static void RegisterAllSettings(MonoBehaviour target, ModularSettingsManager settingsManager)
    {
        var fields = target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<RegisterModularBoolSetting>() is { } boolAttr)
                RegisterModularBoolSetting.Register(target, field, boolAttr, settingsManager);

            else if (field.GetCustomAttribute<RegisterModularFloatSetting>() is { } floatAttr)
                RegisterModularFloatSetting.Register(target, field, floatAttr, settingsManager);

            else if (field.GetCustomAttribute<RegisterModularStringSetting>() is { } stringAttr)
                RegisterModularStringSetting.Register(target, field, stringAttr, settingsManager);

            else if (field.GetCustomAttribute<RegisterModularDropdownSetting>() is { } dropdownAttr)
                RegisterModularDropdownSetting.Register(target, field, dropdownAttr, settingsManager);
        }
    }
}