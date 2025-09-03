using JetBrains.Annotations;

namespace _3DConnections.Runtime.Managers
{
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEngine.InputSystem;

    public class InfoPanelNavigation : MonoBehaviour
    {
        [UsedImplicitly]
        public void OnMenuToggle(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            var infoToggle = GetComponent<UIDocument>();
            if (infoToggle == null) return;
            var root = infoToggle.rootVisualElement;
            var panel = root.Q<VisualElement>("Panel");
            if (panel.ClassListContains("fade_in_opacity"))
            {
                panel.RemoveFromClassList("fade_in_opacity");
                panel.AddToClassList("fade_out_opacity");
            }
            else
            {
                panel.RemoveFromClassList("fade_out_opacity");
                panel.AddToClassList("fade_in_opacity");
            }
        }
    }
}