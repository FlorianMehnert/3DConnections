using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.XR;
#endif

namespace _3DConnections.Runtime.Selection
{
    using ScriptableObjectInventory;

    public class InputValidator : MonoBehaviour
    {
        [SerializeField] private bool isActive = true;

        public bool IsActive
        {
            get => isActive;
            set => isActive = value;
        }

        public bool ShouldProcessInput()
        {
            // Check if menu is open
            if (ScriptableObjectInventory.Instance.menuState?.menuOpen == true)
                return false;

            // Check if system is active
            if (!isActive)
                return false;

            // Check if mouse is in scene view (Editor only)
            if (!IsMouseInSceneView())
                return false;

            return true;
        }

        private bool IsMouseInSceneView()
        {
#if UNITY_EDITOR
            var w = EditorWindow.mouseOverWindow; 
            return w && w.GetType().Name == "GameView"; 
#else 
            return true;
#endif
        }

#if UNITY_EDITOR 
        private EditorWindow _lastFocusedWindow; 
        private bool _skipNextClick;
        
        private void Update()
        {
            var focused = EditorWindow.focusedWindow;
            if (focused == _lastFocusedWindow) return;
            _lastFocusedWindow = focused;
            if (focused && focused.GetType().Name == "GameView")
            {
                _skipNextClick = true;
            }
        }
        
        public bool ConsumeFocusClick()
        {
            if (_skipNextClick)
            {
                _skipNextClick = false;
                return true;
            }
            return false;
        }
        
        public void PingInEditor(GameObject target)
        {
            if (!target) return;

            EditorGUIUtility.PingObject(target);
            UnityEditor.Selection.activeGameObject = target;
        }
#endif
    }
}