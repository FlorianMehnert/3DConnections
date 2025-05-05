using System;
using System.Reflection;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class RegisterModularStringSetting : Attribute
{
    private readonly string _settingName;
    private readonly string _description;
    private readonly string _category;
    private readonly string _defaultValue;

    public RegisterModularStringSetting(string settingName, string description, string category, string defaultValue)
    {
        _settingName = settingName;
        _description = description;
        _category = category;
        _defaultValue = defaultValue;
    }

    public static void Register(MonoBehaviour target, FieldInfo field, RegisterModularStringSetting attr, ModularSettingsManager settingsManager)
    {
        var stringSetting = ScriptableObject.CreateInstance<StringModularMenuData>();
        stringSetting.settingName = attr._settingName;
        stringSetting.description = attr._description;
        stringSetting.category = attr._category;
        stringSetting.defaultValue = attr._defaultValue;

        settingsManager.RegisterSetting(stringSetting);
        stringSetting.OnValueChanged += value => field.SetValue(target, value);
    }
}