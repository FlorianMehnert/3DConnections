using System;
using System.Reflection;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class RegisterModularBoolSetting : Attribute
{
    private readonly string _settingName;
    private readonly string _description;
    private readonly string _category;
    private readonly bool _defaultValue;

    public RegisterModularBoolSetting(string settingName, string description, string category, bool defaultValue)
    {
        _settingName = settingName;
        _description = description;
        _category = category;
        _defaultValue = defaultValue;
    }

    public static void Register(MonoBehaviour target, FieldInfo field, RegisterModularBoolSetting attr, ModularSettingsManager settingsManager)
    {
        var boolSetting = ScriptableObject.CreateInstance<BoolModularMenuData>();
        boolSetting.settingName = attr._settingName;
        boolSetting.description = attr._description;
        boolSetting.category = attr._category;
        boolSetting.defaultValue = attr._defaultValue;

        settingsManager.RegisterSetting(boolSetting);
        boolSetting.OnValueChanged += value => field.SetValue(target, value);
    }
}