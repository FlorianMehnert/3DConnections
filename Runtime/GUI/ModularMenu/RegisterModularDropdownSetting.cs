using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class RegisterModularDropdownSetting : Attribute
{
    private readonly string _settingName;
    private readonly string _description;
    private readonly string _category;
    private readonly int _defaultIndex;
    private readonly List<string> _options;

    public RegisterModularDropdownSetting(string settingName, string description, string category, int defaultIndex, List<string> options)
    {
        _settingName = settingName;
        _description = description;
        _category = category;
        _defaultIndex = defaultIndex;
        _options = options;
    }

    public static void Register(MonoBehaviour target, FieldInfo field, RegisterModularDropdownSetting attr, ModularSettingsManager settingsManager)
    {
        var dropdownSetting = ScriptableObject.CreateInstance<DropdownModularMenuData>();
        dropdownSetting.settingName = attr._settingName;
        dropdownSetting.description = attr._description;
        dropdownSetting.category = attr._category;
        dropdownSetting.options = attr._options;
        dropdownSetting.defaultValueIndex = attr._defaultIndex;

        settingsManager.RegisterSetting(dropdownSetting);
        dropdownSetting.OnValueChanged += value => field.SetValue(target, value);
    }
}