namespace _3DConnections.Editor
{
    using UnityEngine;
    using UnityEditor;
    using Runtime.Nodes;

    [CustomEditor(typeof(LocalNodeConnections))]
    public class GameObjectListDisplayEditor : Editor
    {
        private SerializedProperty _inConnectionsProp;
        private SerializedProperty _outConnectionsProp;

        private void OnEnable()
        {
            _inConnectionsProp = serializedObject.FindProperty("inConnections");
            _outConnectionsProp = serializedObject.FindProperty("outConnections");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("In Connections", EditorStyles.boldLabel);
            if (_inConnectionsProp.arraySize > 0)
            {
                for (int i = 0; i < _inConnectionsProp.arraySize; i++)
                {
                    var element = _inConnectionsProp.GetArrayElementAtIndex(i);
                    element.objectReferenceValue = EditorGUILayout.ObjectField(
                        $"In {i}",
                        element.objectReferenceValue,
                        typeof(GameObject),
                        true
                    );
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No In Connections defined.", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Out Connections", EditorStyles.boldLabel);
            if (_outConnectionsProp.arraySize > 0)
            {
                for (var i = 0; i < _outConnectionsProp.arraySize; i++)
                {
                    var element = _outConnectionsProp.GetArrayElementAtIndex(i);
                    element.objectReferenceValue = EditorGUILayout.ObjectField(
                        $"Out {i}",
                        element.objectReferenceValue,
                        typeof(GameObject),
                        true
                    );
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No Out Connections defined.", MessageType.Info);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Add In Connection"))
            {
                _inConnectionsProp.InsertArrayElementAtIndex(_inConnectionsProp.arraySize);
                _inConnectionsProp.GetArrayElementAtIndex(_inConnectionsProp.arraySize - 1).objectReferenceValue = null;
            }

            if (GUILayout.Button("Add Out Connection"))
            {
                _outConnectionsProp.InsertArrayElementAtIndex(_outConnectionsProp.arraySize);
                _outConnectionsProp.GetArrayElementAtIndex(_outConnectionsProp.arraySize - 1).objectReferenceValue = null;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
