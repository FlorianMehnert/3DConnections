namespace _3DConnections.Runtime.Managers
{
    using UnityEngine;
    
    using ScriptableObjectInventory;

    /// <summary>
    /// Manager responsible for display switching using shortcuts
    /// </summary>
    public class OverlayToggle : MonoBehaviour
    {

        private void Update()
        {
            // Check if F1 is pressed to switch to Display 1
            if (!Input.GetKeyDown(KeyCode.F1)) return;
            ScriptableObjectInventory.Instance.overlay.ToggleOverlay();
            ToggleAnalyzedScene();
            ToggleOverlayScene(ScriptableObjectInventory.Instance.overlay.OverlayIsActive());
        }

        private void ToggleAnalyzedScene()
        {
            ScriptableObjectInventory.Instance.toggleOverlayEvent?.TriggerEvent();
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
                        if (child.name is "SettingsMenu" or "ModularMenu" or "GUIManager") continue;
                        child.gameObject.SetActive(value);
                    }
                }
            }
        }
    }
}