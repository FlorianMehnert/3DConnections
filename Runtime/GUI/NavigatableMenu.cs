using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

public class NavigatableMenu : MonoBehaviour
{
    private UIDocument menuDocument;
    private PlayerInput playerInput;
    private InputAction navigationAction;
    private InputAction submitAction;
    private InputAction tabAction;
    private TabView tabView;
    private List<Tab> tabButtons;
    private List<VisualElement> tabContents;
    private List<Button> currentTabButtons;
    private int currentTabIndex = 0;
    private int currentButtonIndex = -1;
    [SerializeField] MenuState menuState;

    private void Awake()
    {
        menuDocument = GetComponent<UIDocument>();
        playerInput = GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        var root = menuDocument.rootVisualElement;
        
        tabView = root.Q<TabView>("tabs");
        tabButtons = tabView.Query<Tab>().ToList(); 
        tabContents = new List<VisualElement>();
        Debug.Log(tabButtons.Count);
        
        foreach (var content in tabButtons.Select(tab => root.Q<VisualElement>(tab.name + "Content")).Where(content => content != null))
        {
            tabContents.Add(content);
        }
    }

    public void OnTabSwitchPerformed(InputAction.CallbackContext context)
    {
        if(context.performed || context.canceled ) return;
        var tabDirection = context.ReadValue<float>();
    
        var currentIndex = tabView.selectedTabIndex;
        var totalTabs = tabButtons.Count;
    
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
        tabView.selectedTabIndex = newIndex;
        var currentTab = tabButtons[newIndex];
        currentTab.Focus();

        var currentElements = currentTab.Query<Button>().ToList();
        if (currentElements.Count > 0)
            currentElements[0].Focus();
    }

    public void OnMenuToggle(InputAction.CallbackContext context)
    {
        var settingsMenuGeneral = GetComponent<SettingsMenuGeneral>();
        if (settingsMenuGeneral == null)
        {
            Debug.Log("did not find settingsMenuGeneral on menuToggle");
        };
        settingsMenuGeneral.ToggleMenu();
    }

    public void OnNavigationPerformed(InputAction.CallbackContext context)
    {
        if (currentTabButtons == null || currentTabButtons.Count == 0) return;

        Vector2 direction = context.ReadValue<Vector2>();
        int previousIndex = currentButtonIndex;

        // Handle vertical navigation
        if (direction.y != 0)
        {
            currentButtonIndex += direction.y < 0 ? 1 : -1;
            currentButtonIndex = Mathf.Clamp(currentButtonIndex, 0, currentTabButtons.Count - 1);
        }
        
        // Handle horizontal navigation if you have buttons in a grid layout
        else if (direction.x != 0)
        {
            // Implement grid-based navigation here if needed
        }

        if (previousIndex != currentButtonIndex)
        {
            UpdateButtonFocus();
        }
    }

    public void UpdateButtonFocus()
    {
        if (currentButtonIndex >= 0 && currentButtonIndex < currentTabButtons.Count)
        {
            currentTabButtons[currentButtonIndex].Focus();
        }
    }

    public void OnSubmitPerformed(InputAction.CallbackContext context)
    {
        if (currentButtonIndex >= 0 && currentButtonIndex < currentTabButtons.Count)
        {
            // Simulate button click
            var button = currentTabButtons[currentButtonIndex];
            using var e = new NavigationSubmitEvent();
            e.target = button;
            button.SendEvent(e);
            ;
        }
    }
}
