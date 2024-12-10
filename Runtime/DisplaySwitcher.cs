using Runtime;
using UnityEngine;

/// <summary>
/// Manager responsible for display switching using shortcuts
/// </summary>
public class DisplaySwitcher : MonoBehaviour
{
    private void Update()
    {
        // Check if F1 is pressed to switch to Display 1
        if (!Input.GetKeyDown(KeyCode.F1)) return;
        Debug.Log("toggle display");
        SceneHandler.ToggleOverlay();
    }

    /// <summary>
    /// "Open" display2 if this is required. Currently, not since the node tree is more like an overlay than a second scene or window
    /// </summary>
    /// <param name="displayIndex"></param>
    private static void SwitchDisplay(int displayIndex)
    {
        // Check if the specified display index exists and is active
        if (displayIndex < Display.displays.Length)
        {
            if (!Display.displays[displayIndex].active)
            {
                Display.displays[displayIndex].Activate();
                Debug.Log($"Activated Display {displayIndex + 1}");
            }
            else
            {
                Debug.Log($"Display {displayIndex + 1} is already active.");
            }
        }
        else
        {
            Debug.LogWarning($"Display {displayIndex + 1} does not exist or is unavailable.");
        }
    }
}