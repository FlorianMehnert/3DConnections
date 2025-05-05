using System;
using System.Reflection;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class RegisterModularFloatSetting : Attribute
{
    private readonly string _settingName;
    private readonly string _description;
    private readonly string _category;
    private readonly float _defaultValue;

    public RegisterModularFloatSetting(string settingName, string description, string category, float defaultValue)
    {
        _settingName = settingName;
        _description = description;
        _category = category;
        _defaultValue = defaultValue;
    }

    public static void Register(MonoBehaviour target, FieldInfo field, RegisterModularFloatSetting attr, ModularSettingsManager settingsManager)
    {
        var floatSetting = ScriptableObject.CreateInstance<FloatModularMenuData>();
        floatSetting.settingName = attr._settingName;
        floatSetting.description = attr._description;
        floatSetting.category = attr._category;
        floatSetting.defaultValue = attr._defaultValue;

        settingsManager.RegisterSetting(floatSetting);
        floatSetting.OnValueChanged += value => field.SetValue(target, value);
    }
}