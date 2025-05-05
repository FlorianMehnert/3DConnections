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
    private readonly float _minValue;
    private readonly float _maxValue;

    public RegisterModularFloatSetting(string settingName, string description, string category, float defaultValue, float minValue, float maxValue)
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
        floatSetting.minValue = attr._minValue;
        floatSetting.maxValue = attr._maxValue;

        settingsManager.RegisterSetting(floatSetting);
        floatSetting.OnValueChanged += value => field.SetValue(target, value);
    }
}