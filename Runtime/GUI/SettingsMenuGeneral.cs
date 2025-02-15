using UnityEngine;
using UnityEngine.UIElements;

public class SettingsMenuGeneral : MonoBehaviour
{
    public UIDocument uiDocument;
    private VisualElement _panel;
    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _panel = root.Q<VisualElement>("Panel");
        Debug.Log("panel is: " + _panel.panel);
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
}