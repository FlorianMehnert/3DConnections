using UnityEngine;

/// <summary>
/// Manager responsible for display switching using shortcuts
/// </summary>
public class OverlayToggle : MonoBehaviour
{
    [SerializeField] private OverlaySceneScriptableObject overlay;
    [SerializeField] private ToggleOverlayEvent overlayEvent;

    private void Update()
    {
        // Check if F1 is pressed to switch to Display 1
        if (!Input.GetKeyDown(KeyCode.F1)) return;
        overlay.ToggleOverlay();
        ToggleAnalyzedScene();
        Debug.Log(overlay.OverlayIsActive());
        ToggleOverlayScene(overlay.OverlayIsActive());
    }

    private void ToggleAnalyzedScene()
    {
        Debug.Log("toggle overlay");
        overlayEvent?.TriggerEvent();
    }

    /// <summary>
    /// Enable/Disable whole overlayScene except the manager to still allowing toggling back
    /// </summary>
    /// <param name="value">true for enable/false for disable overlay scene</param>
    private void ToggleOverlayScene(bool value)
    {
        foreach (var go in gameObject.scene.GetRootGameObjects())
        {
            if (go != gameObject)
                go.SetActive(value);
            else
            {
                foreach (Transform child in gameObject.transform)
                {
                    child.gameObject.SetActive(value);
                }
            }
        }
    }
}