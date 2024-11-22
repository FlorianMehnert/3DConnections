using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace com.florian_mehnert._3d_connections.Editor
{
    public class ScriptDependencyVisualizer : EditorWindow
    {
        private SceneAsset _previousScene;
        private Scene _visualizationScene;
        private string _namespaceFilter = "";
        private int _maxScripts = 100;
        private float _radius = 15f;

        [MenuItem("Tools/Script Dependency Visualizer")]
        public static void ShowWindow()
        {
            GetWindow<ScriptDependencyVisualizer>("Script Dependency Visualizer");
        }

        private void OnGUI()
        {
            GUILayout.Label("Visualization Settings", EditorStyles.boldLabel);
            _namespaceFilter = EditorGUILayout.TextField("Namespace Filter", _namespaceFilter);
            _maxScripts = EditorGUILayout.IntField("Max Scripts", _maxScripts);
            _radius = EditorGUILayout.Slider("Layout Radius", _radius, 5f, 50f);

            if (GUILayout.Button("Open Visualization"))
            {
                OpenVisualizationScene();
                VisualizeDependencies();
            }
        }

        private void OpenVisualizationScene()
        {
            _previousScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneManager.GetActiveScene().path);
            _visualizationScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        private void VisualizeDependencies()
        {
            var scripts = Resources.FindObjectsOfTypeAll<MonoScript>()
                .Where(s => typeof(MonoBehaviour).IsAssignableFrom(s.GetClass()) && 
                            (string.IsNullOrEmpty(_namespaceFilter) || s.GetClass().Namespace?.Contains(_namespaceFilter) == true))
                .Take(_maxScripts)
                .ToArray();

            var scriptObjects = new Dictionary<MonoScript, GameObject>();

            for (var i = 0; i < scripts.Length; i++)
            {
                var script = scripts[i];
                var angle = i * Mathf.PI * 2 / scripts.Length;
                var radius = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * _radius;

                var scriptObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                scriptObject.name = script.name;
                scriptObject.transform.position = radius;
                scriptObjects[script] = scriptObject;

                // Create a label with smaller text
                var textObject = new GameObject($"{script.name}_Label");
                var textMesh = textObject.AddComponent<TextMesh>();
                textMesh.text = script.name;
                textMesh.fontSize = 20; // Reduced font size
                textObject.transform.position = scriptObject.transform.position + Vector3.up * 1.5f;
                textObject.transform.localScale = Vector3.one * 0.5f; // Make the text smaller
                textObject.transform.parent = scriptObject.transform;
            }

            foreach (var scriptPair in scriptObjects)
            {
                var script = scriptPair.Key;
                var sourceObject = scriptPair.Value;

                var dependencies = GetScriptDependencies(script);

                foreach (var dependency in dependencies)
                {
                    if (scriptObjects.TryGetValue(dependency, out var targetObject))
                    {
                        var lineObject = new GameObject($"{script.name}_to_{dependency.name}");
                        var lineRenderer = lineObject.AddComponent<LineRenderer>();
                        lineRenderer.positionCount = 2;
                        lineRenderer.SetPosition(0, sourceObject.transform.position);
                        lineRenderer.SetPosition(1, targetObject.transform.position);
                        lineRenderer.startWidth = 0.05f;
                        lineRenderer.endWidth = 0.05f;
                        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                        lineRenderer.startColor = Color.cyan;
                        lineRenderer.endColor = Color.cyan;
                    }
                }
            }
        }

        private List<MonoScript> GetScriptDependencies(MonoScript script)
        {
            var dependencies = new List<MonoScript>();
            var scriptText = script.text;
            var scriptClass = script.GetClass();
            if (scriptClass == null) return dependencies;

            var referencedClasses = Regex.Matches(scriptText, @"\busing\s+([A-Za-z0-9_.]+);")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct();

            foreach (var otherScript in Resources.FindObjectsOfTypeAll<MonoScript>())
            {
                var otherClass = otherScript.GetClass();
                var classes = referencedClasses as string[] ?? referencedClasses.ToArray();
                var enumerable = referencedClasses as string[] ?? classes.ToArray();
                if (otherClass != null && enumerable.Contains(otherClass.Namespace))
                {
                    dependencies.Add(otherScript);
                }
            }

            return dependencies;
        }

        private void OnDisable()
        {
            if (_previousScene != null)
            {
                EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(_previousScene));
            }
        }
    }
}
