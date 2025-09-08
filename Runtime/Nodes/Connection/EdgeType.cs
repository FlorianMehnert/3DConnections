namespace _3DConnections.Runtime.Nodes.Connection
{
    using UnityEngine;
    
    public class EdgeType : MonoBehaviour
    {
        [SerializeField] public string connectionType;
        [SerializeField] public CodeReference codeReference;
        
        private float _lastClickTime;
        private const float DoubleClickTime = 0.3f;
        
        private void OnMouseDown()
        {
            if (Time.time - _lastClickTime < DoubleClickTime)
            {
                OnDoubleClick();
            }
            _lastClickTime = Time.time;
        }
        
        private void OnDoubleClick()
        {
            if (codeReference is { HasReference: true })
            {
                OpenCodeEditor();
            }
        }
        
        private void OpenCodeEditor()
        {
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.OpenAsset(
                UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(codeReference.sourceFile),
                codeReference.lineNumber
            );
#endif
        }
        
        public void SetCodeReference(string sourceFile, int lineNumber, string methodName = null, string className = null)
        {
            codeReference = new CodeReference
            {
                sourceFile = sourceFile,
                lineNumber = lineNumber,
                methodName = methodName,
                className = className
            };
        }
    }
}