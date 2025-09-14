using _3DConnections.Runtime.ScriptableObjects;

namespace _3DConnections.Editor
{
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// See Unity open-project-1
    /// <see href="https://github.com/UnityTechnologies/open-project-1/blob/devlogs/2-scriptable-objects/UOP1_Project/Assets/Scripts/Editor/GameSceneSOEditor.cs"/>
    /// </summary>
    [CustomEditor(typeof(GameSceneSO), true)]
    // ReSharper disable once InconsistentNaming
    public class GameSceneSOEditor : Editor
    {
        private const string NoScenesWarning =
            "There is no Scene associated to this location yet. Add a new scene with the dropdown below";

        private GUIStyle _headerLabelStyle;
        private static readonly string[] ExcludedProperties = { "m_Script", "sceneName" };

        private string[] _sceneList;
        private GameSceneSO _gameSceneInspected;

        private void OnEnable()
        {
            _gameSceneInspected = target as GameSceneSO;
            PopulateScenePicker();
            InitializeGuiStyles();
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Scene information", _headerLabelStyle);
            EditorGUILayout.Space();
            DrawScenePicker();
            DrawPropertiesExcluding(serializedObject, ExcludedProperties);
        }

        private void DrawScenePicker()
        {
            var sceneName = _gameSceneInspected.sceneName;
            EditorGUI.BeginChangeCheck();
            var selectedScene = _sceneList.ToList().IndexOf(sceneName);

            if (selectedScene < 0)
            {
                EditorGUILayout.HelpBox(NoScenesWarning, MessageType.Warning);
            }

            selectedScene = EditorGUILayout.Popup("Scene", selectedScene, _sceneList);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(target, "Changed selected scene");
            _gameSceneInspected.sceneName = _sceneList[selectedScene];
            MarkAllDirty();
        }

        private void InitializeGuiStyles()
        {
            _headerLabelStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 18,
                fixedHeight = 70.0f
            };
        }

        /// <summary>
        /// Populates the Scene picker with Scenes included in the game's build index
        /// </summary>
        private void PopulateScenePicker()
        {
            var sceneCount = SceneManager.sceneCountInBuildSettings;
            _sceneList = new string[sceneCount];
            for (int i = 0; i < sceneCount; i++)
            {
                _sceneList[i] = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            }
        }

        /// <summary>
        /// Marks scenes as dirty so data can be saved
        /// </summary>
        private void MarkAllDirty()
        {
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkAllScenesDirty();
        }
    }
}