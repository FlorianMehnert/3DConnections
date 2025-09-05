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
            return EditorWindow.mouseOverWindow && EditorWindow.mouseOverWindow.GetType().Name == "GameView";
#else
            return true; // Always true in runtime builds
#endif
        }

#if UNITY_EDITOR
        public void PingInEditor(GameObject target)
        {
            if (target == null) return;
            
            EditorGUIUtility.PingObject(target);
            UnityEditor.Selection.activeGameObject = target;
        }
#endif
    }
}