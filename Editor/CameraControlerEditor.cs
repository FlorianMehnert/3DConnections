using _3DConnections.Runtime.Managers;
using _3DConnections.Runtime.ScriptableObjects;
using UnityEditor;
using UnityEngine;


namespace _3DConnections.Editor
{
    [CustomEditor(typeof(CameraController))]
    public class CameraControllerEditor : UnityEditor.Editor
    {
        [SerializeField] private NodeGraphScriptableObject nodeGraph;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (CameraController)target;
            if (GUILayout.Button("Center on selection"))
            {
                if (nodeGraph != null)
                    controller.CenterOnTarget(nodeGraph.currentlySelectedGameObject, true);
                ;
            }

            if (GUILayout.Button("Center on selection including Editor"))
            {
                if (nodeGraph != null)
                    controller.CenterOnTarget(nodeGraph.currentlySelectedGameObject, true);
                ;
            }
        }
    }
}