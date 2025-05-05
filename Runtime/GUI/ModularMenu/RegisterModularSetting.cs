using System;
using System.Reflection;
using UnityEngine;

[System.AttributeUsage(AttributeTargets.Field)]
public class RegisterModularSetting : Attribute
{
    private readonly string _settingName;
    private readonly string _description;
    private readonly string _category;
    private readonly bool _defaultValue;

    public RegisterModularSetting(string settingName, string description, string category, bool defaultValue)
    {
        _settingName = settingName;
        _description = description;
        _category = category;
        _defaultValue = defaultValue;
    }
    
    public static void RegisterAllSettings(MonoBehaviour target, ModularSettingsManager settingsManager)
    {
        var fields = target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            var attr = field.GetCustomAttribute<RegisterModularSetting>();
            if (attr == null || field.FieldType != typeof(bool)) continue;
            var boolSetting = ScriptableObject.CreateInstance<BoolModularMenuData>();
            boolSetting.settingName = attr._settingName;
            boolSetting.description = attr._description;
            boolSetting.category = attr._category;
            boolSetting.defaultValue = attr._defaultValue;

            settingsManager.RegisterSetting(boolSetting);

            boolSetting.OnValueChanged += (value) =>
            {
                field.SetValue(target, value);
            };
        }
    }
}