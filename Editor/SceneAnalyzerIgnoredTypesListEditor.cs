namespace _3DConnections.Editor
{
    using Runtime.Managers;
    using UnityEditor;
    using UnityEngine;
    using System;
    using System.Linq;
    using System.Collections.Generic;

    [CustomEditor(typeof(SceneAnalyzer))]
    public class SceneAnalyzerIgnoredTypesListEditor : Editor
    {
        private SceneAnalyzer _analyzer;
        private string[] _availableTypes;
        private List<string> _filteredTypes;
        private string _searchQuery = "";
        private int _selectedTypeIndex;

        private void OnEnable()
        {
            _analyzer = (SceneAnalyzer)target;

            // Include all Unity Component types (including Transform, Rigidbody, etc.)
            _availableTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && typeof(Component).IsAssignableFrom(t))
                .Select(t => t.AssemblyQualifiedName)
                .OrderBy(t => t) // Sort alphabetically for easier selection
                .ToArray();

            _filteredTypes = _availableTypes.ToList();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.LabelField("Ignored Types", EditorStyles.boldLabel);

            // Display ignored types list
            for (var i = 0; i < _analyzer.IgnoredTypes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{i}]: {_analyzer.IgnoredTypes[i]}");

                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    _analyzer.IgnoredTypes.RemoveAt(i);
                    EditorUtility.SetDirty(_analyzer);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add New Type", EditorStyles.boldLabel);

            // Search box for filtering available types
            EditorGUILayout.BeginHorizontal();
            _searchQuery = EditorGUILayout.TextField("Search:", _searchQuery);

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _searchQuery = "";
                _filteredTypes = _availableTypes.ToList();
            }

            EditorGUILayout.EndHorizontal();

            // Apply search filter
            _filteredTypes = !string.IsNullOrEmpty(_searchQuery)
                ? _availableTypes.Where(t => t.ToLower().Contains(_searchQuery.ToLower())).ToList()
                : _availableTypes.ToList();

            // Dropdown for filtered types
            if (_filteredTypes.Count > 0)
            {
                _selectedTypeIndex = EditorGUILayout.Popup("Select Type", _selectedTypeIndex, _filteredTypes.ToArray());
            }
            else
            {
                EditorGUILayout.HelpBox("No matching types found.", MessageType.Info);
            }

            if (GUILayout.Button("Add Selected Type") && _filteredTypes.Count > 0)
            {
                var selectedType = _filteredTypes[_selectedTypeIndex];
                if (!_analyzer.IgnoredTypes.Contains(selectedType))
                {
                    _analyzer.IgnoredTypes.Add(selectedType);
                    EditorUtility.SetDirty(_analyzer);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}