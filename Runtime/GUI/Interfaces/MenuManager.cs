using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MenuManager : MonoBehaviour
{
    private static MenuManager _instance;
    public static MenuManager Instance
    {
        get
        {
            if (_instance) return _instance;
            _instance = FindFirstObjectByType<MenuManager>();
            if (_instance) return _instance;
            var managerObject = new GameObject("MenuManager");
            _instance = managerObject.AddComponent<MenuManager>();
            DontDestroyOnLoad(_instance.gameObject);
            return _instance;
        }
    }

    private IMenu _activeMenu;
    private readonly Dictionary<KeyCode, IMenu> _menuKeybinds = new();

    public void RegisterMenu(KeyCode keyCode, IMenu menu)
    {
        if (!_menuKeybinds.TryAdd(keyCode, menu))
        {
            Debug.LogWarning($"KeyCode {keyCode} is already registered to a menu.");
        }
    }

    public void ActivateMenu(IMenu menu)
    {
        if (_activeMenu == menu)
        {
            _activeMenu.OnMenuClose();
            _activeMenu = null;
            return;
        }

        _activeMenu?.OnMenuClose();

        menu.OnMenuOpen();
        _activeMenu = menu;
    }

    private void CloseActiveMenu()
    {
        if (_activeMenu == null) return;
        
        _activeMenu.OnMenuClose();
        _activeMenu = null;
    }

    private void Start()
    {
        foreach (var menuKeybind in _menuKeybinds)
        {
            menuKeybind.Value.OnMenuClose();
            _activeMenu = null;
        }
    }

    private void Update()
    {
        foreach (var keybind in _menuKeybinds.Where(keybind => Input.GetKeyDown(keybind.Key)))
        {
            if (_activeMenu == keybind.Value)
            {
                CloseActiveMenu();
            }
            else
            {
                ActivateMenu(keybind.Value);
            }
            break;
        }
    }
}
