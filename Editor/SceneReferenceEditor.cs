using UnityEditor;

namespace _3DConnections.Editor
{
    [CustomEditor(typeof(SceneReference))]
    public class SceneReferenceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var sceneReference = (SceneReference)target;
            serializedObject.Update();

            // Fetch all scenes in the project
            var scenes = EditorBuildSettings.scenes;
            var scenePaths = new string[scenes.Length];
            var sceneNames = new string[scenes.Length];

            for (var i = 0; i < scenes.Length; i++)
            {
                scenePaths[i] = scenes[i].path;
                sceneNames[i] = System.IO.Path.GetFileNameWithoutExtension(scenes[i].path);
            }

            // Create a dropdown for scene selection
            var selectedIndex = System.Array.IndexOf(scenePaths, sceneReference.Path);
            var newIndex = EditorGUILayout.Popup("Scene", selectedIndex, sceneNames);

            if (newIndex < 0 || newIndex >= scenePaths.Length) return;
            var pathProperty = serializedObject.FindProperty("scenePath");
            var nameProperty = serializedObject.FindProperty("sceneName");
                
            pathProperty.stringValue = scenePaths[newIndex];
            nameProperty.stringValue = sceneNames[newIndex];

            // Display the selected scene's name and path
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField("Scene Name", sceneNames[newIndex]);
            EditorGUILayout.LabelField("Scene Path", scenePaths[newIndex]);
            EditorGUI.EndDisabledGroup();
                
            serializedObject.ApplyModifiedProperties();
        }
    }
}