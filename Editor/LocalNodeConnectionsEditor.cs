using UnityEditor.Search;

namespace _3DConnections.Editor
{
    using UnityEngine;
    using UnityEditor;
    using Runtime.Nodes;

    [CustomEditor(typeof(LocalNodeConnections))]
    public class LocalNodeConnectionsEditor : Editor
    {
        private SerializedProperty _inConnectionsProp;
        private SerializedProperty _outConnectionsProp;

        private Vector2 _inScrollPos;
        private Vector2 _outScrollPos;

        private const float ScrollViewHeight = 200f;

        private void OnEnable()
        {
            _inConnectionsProp = serializedObject.FindProperty("inConnections");
            _outConnectionsProp = serializedObject.FindProperty("outConnections");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawConnectionList(_inConnectionsProp, "In Connections", "In", ref _inScrollPos);
            EditorGUILayout.Space();
            DrawConnectionList(_outConnectionsProp, "Out Connections", "Out", ref _outScrollPos);

            EditorGUILayout.Space();
            if (GUILayout.Button("Add In Connection"))
            {
                AddConnection(_inConnectionsProp);
            }
            if (GUILayout.Button("Add Out Connection"))
            {
                AddConnection(_outConnectionsProp);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawConnectionList(SerializedProperty listProp, string label, string prefix, ref Vector2 scrollPos)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (listProp.arraySize > 0)
            {
                // Begin scroll view
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(ScrollViewHeight));
                for (var i = 0; i < listProp.arraySize; i++)
                {
                    var element = listProp.GetArrayElementAtIndex(i);
                    element.objectReferenceValue = EditorGUILayout.ObjectField(
                        $"{prefix} {i}",
                        element.objectReferenceValue,
                        typeof(GameObject),
                        true
                    );

                    HandleContextMenu(element, GUILayoutUtility.GetLastRect());
                }
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox($"No {label} defined.", MessageType.Info);
            }
        }

        private static void HandleContextMenu(SerializedProperty element, Rect fieldRect)
        {
            var e = Event.current;
            if (e.type != EventType.ContextClick || !fieldRect.Contains(e.mousePosition)) return;
            var menu = new GenericMenu();
            if (element.objectReferenceValue != null)
            {
                var go = element.objectReferenceValue as GameObject;
                menu.AddItem(new GUIContent("Print node's name"), false, () =>
                {
                    if (!go) return;
                    SearchService.ShowWindow(
                        SearchService.CreateContext($"node: highlight{{{go.name}}}"));
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("This element needs to be assigned first"));
            }

            menu.ShowAsContext();
            e.Use();
        }

        private static void AddConnection(SerializedProperty listProp)
        {
            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = null;
        }
    }
}
