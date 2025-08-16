﻿
namespace _3DConnections.Runtime.Managers
{
    using Interfaces;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEngine.InputSystem;
    using System.Collections.Generic;

    public class NavigatableMenu : MonoBehaviour
    {
        private UIDocument _menuDocument;
        private InputAction _navigationAction;
        private InputAction _submitAction;
        private InputAction _tabAction;
        private TabView _tabView;
        private List<Tab> _tabButtons;
        private List<Button> _currentTabButtons;
        private int _currentButtonIndex = -1;

        private void Awake()
        {
            _menuDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _menuDocument.rootVisualElement;

            _tabView = root.Q<TabView>("tabs");
            _tabButtons = _tabView.Query<Tab>().ToList();
        }

        public void OnTabSwitchPerformed(InputAction.CallbackContext context)
        {
            if (context.performed || context.canceled) return;
            var tabDirection = context.ReadValue<float>();

            var currentIndex = _tabView.selectedTabIndex;
            var totalTabs = _tabButtons.Count;

            int newIndex;
            if (tabDirection > 0)
            {
                newIndex = (currentIndex + 1) % totalTabs;
            }
            else
            {
                newIndex = (currentIndex - 1 + totalTabs) % totalTabs;
            }

            if (newIndex < 0 || newIndex >= totalTabs) return;
            _tabView.selectedTabIndex = newIndex;
            var currentTab = _tabButtons[newIndex];
            currentTab.Focus();

            var currentElements = currentTab.Query<Button>().ToList();
            if (currentElements.Count > 0)
                currentElements[0].Focus();
        }

        public void OnMenuToggle(InputAction.CallbackContext context)
        {
            var settingsMenuGeneral = GetComponent<SettingsMenuGeneral>();
            if (!settingsMenuGeneral)
            {
                Debug.Log("did not find settingsMenuGeneral on menuToggle");
            }

            ;
            MenuManager.Instance.ActivateMenu(settingsMenuGeneral);
        }

        public void OnNavigationPerformed(InputAction.CallbackContext context)
        {
            if (_currentTabButtons == null || _currentTabButtons.Count == 0) return;

            var direction = context.ReadValue<Vector2>();
            var previousIndex = _currentButtonIndex;

            // Vertical navigation
            if (direction.y != 0)
            {
                _currentButtonIndex += direction.y < 0 ? 1 : -1;
                _currentButtonIndex = Mathf.Clamp(_currentButtonIndex, 0, _currentTabButtons.Count - 1);
            }

            if (previousIndex != _currentButtonIndex)
            {
                UpdateButtonFocus();
            }
        }

        private void UpdateButtonFocus()
        {
            if (_currentButtonIndex >= 0 && _currentButtonIndex < _currentTabButtons.Count)
            {
                _currentTabButtons[_currentButtonIndex].Focus();
            }
        }

        public void OnSubmitPerformed(InputAction.CallbackContext context)
        {
            if (_currentButtonIndex < 0 || _currentButtonIndex >= _currentTabButtons.Count) return;
            var button = _currentTabButtons[_currentButtonIndex];
            using var e = new NavigationSubmitEvent();
            e.target = button;
            button.SendEvent(e);
        }
    }
}