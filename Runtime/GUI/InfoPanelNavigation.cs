
using JetBrains.Annotations;

namespace _3DConnections.Runtime.Managers
{
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEngine.InputSystem;

    public class InfoPanelNavigation : MonoBehaviour
    {
        private UIDocument _menuDocument;
        private VisualElement _infoPanel;
        private bool _isVisible = true;

        private void Awake()
        {
            _menuDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _menuDocument.rootVisualElement;

            _infoPanel = root.Q<VisualElement>("VisualElement"); 
        }

        [UsedImplicitly]
        public void OnInfoPanelToggle(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            if (_infoPanel == null) return;

            if (_isVisible)
            {
                _infoPanel.RemoveFromClassList("fade_in_opacity");
                _infoPanel.AddToClassList("fade_out_opacity");
            }
            else
            {
                _infoPanel.RemoveFromClassList("fade_out_opacity");
                _infoPanel.AddToClassList("fade_in_opacity");
            }

            _isVisible = !_isVisible;
        }
    }
}