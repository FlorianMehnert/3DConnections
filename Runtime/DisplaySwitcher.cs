using UnityEngine;

/// <summary>
/// Manager responsible for display switching using shortcuts
/// </summary>
public class DisplaySwitcher : MonoBehaviour
{
    private void Update()
    {
        // Check if F1 is pressed to switch to Display 1
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SwitchDisplay(0); // Display 1 (Index 0)
        }

        // Check if F2 is pressed to switch to Display 2
        if (Input.GetKeyDown(KeyCode.F2))
        {
            SwitchDisplay(1); // Display 2 (Index 1)
        }
    }

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