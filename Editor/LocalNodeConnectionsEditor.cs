using _3DConnections.Runtime.Managers.Scene;
using UnityEditor.Search;

namespace _3DConnections.Editor
{
    using UnityEngine;
    using UnityEditor;
    using Runtime.Nodes;
    using Runtime.Managers;

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
                // Define the full rect for the scroll view
                Rect scrollRect = GUILayoutUtility.GetRect(
                    0, // min width
                    float.MaxValue, // max width (fill inspector)
                    0, // min height
                    ScrollViewHeight // fixed height
                );

                // Begin scroll view
                scrollPos = GUI.BeginScrollView(scrollRect, scrollPos,
                    new Rect(0, 0, scrollRect.width - 16f, listProp.arraySize * EditorGUIUtility.singleLineHeight));

                for (int i = 0; i < listProp.arraySize; i++)
                {
                    Rect lineRect = new Rect(
                        0,
                        i * EditorGUIUtility.singleLineHeight,
                        scrollRect.width - 16f, // subtract scrollbar width
                        EditorGUIUtility.singleLineHeight
                    );

                    SerializedProperty element = listProp.GetArrayElementAtIndex(i);
                    element.objectReferenceValue = EditorGUI.ObjectField(
                        lineRect,
                        $"{prefix} {i}",
                        element.objectReferenceValue,
                        typeof(GameObject),
                        true
                    );

                    HandleContextMenu(element, lineRect);
                }

                GUI.EndScrollView();
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
                menu.AddItem(new GUIContent("Focus on this node"), false, () =>
                {
                    if (!go) return;
                    CameraController.AdjustCameraToViewObjects(SceneHandler.GetCameraOfOverlayedScene(), new [] {go});
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
