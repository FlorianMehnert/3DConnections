using UnityEngine;
using UnityEngine.UIElements;

public class SettingsMenuGeneral : MonoBehaviour
{
    public UIDocument uiDocument;
    private VisualElement _panel;
    private Button _clearButton;
    private Button _removePhysicsButton;
    [SerializeField] private RemovePhysicsEvent removePhysicsEvent;
    [SerializeField] private ClearEvent clearEvent;
    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _panel = root.Q<VisualElement>("Panel");
        _clearButton = root.Q<Button>("Clear");
        _removePhysicsButton = root.Q<Button>("RemovePhysics");
        
        if (_clearButton != null)
            _clearButton.clicked += () => clearEvent.TriggerEvent();
        if (_removePhysicsButton != null)
            _removePhysicsButton.clicked += () => removePhysicsEvent.TriggerEvent();
        
        if (removePhysicsEvent != null)
            removePhysicsEvent.OnEventTriggered += HandleEvent;
    }
    
    private void OnDisable()
    {
        if (removePhysicsEvent != null)
            removePhysicsEvent.OnEventTriggered -= HandleEvent;
    }
    

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    private void ShowMenu()
    {
        _panel.RemoveFromClassList("hidden");
    }

    private void HideMenu()
    {
        _panel.AddToClassList("hidden");
    }

    private void ToggleMenu()
    {
        if (_panel.ClassListContains("hidden"))
            ShowMenu();
        else
            HideMenu();
    }
    
    private void HandleEvent()
    {
        // ChangeButtonEnabled(_executeNodeSpawnButton.gameObject, true);
        // ChangeButtonEnabled(_removePhysicsButton.gameObject, false);
    }
}