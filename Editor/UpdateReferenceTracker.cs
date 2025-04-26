using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace _3DConnections.Editor
{
    public class UpdateReferenceTracker : EditorWindow
    {
        // Lists to store tracking data
        private List<MonoBehaviourInfo> updateMethods = new();
        private List<ReferenceInfo> references = new();
        private bool isPaused;
        private Vector2 executionScrollPosition;
        private Vector2 referenceScrollPosition;
        private string searchFilter = "";
        private bool showUpdateMethodsWithNoReferences = true;
        private bool autoRefresh = false;
        private double lastRefreshTime;
        private float refreshInterval = 5.0f; // seconds
        private Color oddRowColor;
        private Color evenRowColor;
        private GUIStyle headerStyle;
        private GUIStyle rowStyle;

        // Toolbar selection
        private int selectedTab = 0;
        private readonly string[] tabOptions = { "Update Methods", "References", "Settings" };

        [MenuItem("Tools/Update Reference Tracker")]
        public static void ShowWindow()
        {
            GetWindow<UpdateReferenceTracker>("Update Reference Tracker");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            oddRowColor = new Color(0.93f, 0.93f, 0.93f, 0.3f);
            evenRowColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);

            headerStyle = new GUIStyle();
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            
            rowStyle = new GUIStyle();
            rowStyle.padding = new RectOffset(5, 5, 3, 3);
            rowStyle.margin = new RectOffset(0, 0, 1, 1);

            // Perform initial scan
            TrackUpdateReferences();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (isPaused)
                return;

            if (autoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime > refreshInterval)
            {
                TrackUpdateReferences();
                lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void TrackUpdateReferences()
        {
            updateMethods.Clear();
            references.Clear();

            var allScripts = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID);
            
            // First pass: collect all update methods
            foreach (var script in allScripts)
            {
                if (script == null) continue;
                
                var updateMethod = script.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (updateMethod == null) continue;
                
                updateMethods.Add(new MonoBehaviourInfo
                {
                    GameObject = script.gameObject,
                    ComponentName = script.GetType().Name,
                    InstanceID = script.GetInstanceID(),
                    IsActive = script.isActiveAndEnabled && script.gameObject.activeInHierarchy
                });
            }
            
            // Second pass: track references
            foreach (var info in updateMethods)
            {
                var component = EditorUtility.InstanceIDToObject(info.InstanceID) as MonoBehaviour;
                if (component == null) continue;
                
                TrackReferences(component, info);
            }
        }

        private void TrackReferences(MonoBehaviour script, MonoBehaviourInfo info)
        {
            // Track references to other MonoBehaviours
            FieldInfo[] fields = script.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            foreach (var field in fields)
            {
                // Check for MonoBehaviour references
                if (typeof(MonoBehaviour).IsAssignableFrom(field.FieldType))
                {
                    var referencedComponent = field.GetValue(script) as MonoBehaviour;
                    if (referencedComponent != null)
                    {
                        references.Add(new ReferenceInfo
                        {
                            SourceInstanceID = info.InstanceID,
                            SourceName = $"{info.GameObject.name} ({info.ComponentName})",
                            TargetInstanceID = referencedComponent.GetInstanceID(),
                            TargetName = $"{referencedComponent.gameObject.name} ({referencedComponent.GetType().Name})",
                            FieldName = field.Name,
                            IsActive = referencedComponent.isActiveAndEnabled && referencedComponent.gameObject.activeInHierarchy
                        });
                    }
                }
                // Check for GameObject references
                else if (field.FieldType == typeof(GameObject))
                {
                    var referencedGameObject = field.GetValue(script) as GameObject;
                    if (referencedGameObject != null)
                    {
                        references.Add(new ReferenceInfo
                        {
                            SourceInstanceID = info.InstanceID,
                            SourceName = $"{info.GameObject.name} ({info.ComponentName})",
                            TargetInstanceID = referencedGameObject.GetInstanceID(),
                            TargetName = $"{referencedGameObject.name} (GameObject)",
                            FieldName = field.Name,
                            IsActive = referencedGameObject.activeInHierarchy
                        });
                    }
                }
                // Check for Component arrays/lists
                else if (field.FieldType.IsArray && typeof(Component).IsAssignableFrom(field.FieldType.GetElementType()))
                {
                    var array = field.GetValue(script) as Component[];
                    if (array != null)
                    {
                        foreach (var component in array)
                        {
                            if (component != null)
                            {
                                references.Add(new ReferenceInfo
                                {
                                    SourceInstanceID = info.InstanceID,
                                    SourceName = $"{info.GameObject.name} ({info.ComponentName})",
                                    TargetInstanceID = component.GetInstanceID(),
                                    TargetName = $"{component.gameObject.name} ({component.GetType().Name})",
                                    FieldName = $"{field.Name}[]",
                                    IsActive = component is MonoBehaviour mb ? mb.isActiveAndEnabled && mb.gameObject.activeInHierarchy : component.gameObject.activeInHierarchy
                                });
                            }
                        }
                    }
                }
                else if (field.FieldType.IsGenericType && typeof(List<>).IsAssignableFrom(field.FieldType.GetGenericTypeDefinition()))
                {
                    var listType = field.FieldType.GetGenericArguments()[0];
                    if (typeof(Component).IsAssignableFrom(listType))
                    {
                        var list = field.GetValue(script) as System.Collections.IList;
                        if (list != null)
                        {
                            foreach (Component component in list)
                            {
                                if (component != null)
                                {
                                    references.Add(new ReferenceInfo
                                    {
                                        SourceInstanceID = info.InstanceID,
                                        SourceName = $"{info.GameObject.name} ({info.ComponentName})",
                                        TargetInstanceID = component.GetInstanceID(),
                                        TargetName = $"{component.gameObject.name} ({component.GetType().Name})",
                                        FieldName = $"{field.Name} List",
                                        IsActive = component is MonoBehaviour mb ? mb.isActiveAndEnabled && mb.gameObject.activeInHierarchy : component.gameObject.activeInHierarchy
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.Space();

            // Search filter
            GUI.backgroundColor = Color.white;
            searchFilter = EditorGUILayout.TextField("Search", searchFilter, EditorStyles.toolbarSearchField);
            
            EditorGUILayout.Space();

            // Display the selected tab
            switch (selectedTab)
            {
                case 0:
                    DrawUpdateMethodsTab();
                    break;
                case 1:
                    DrawReferencesTab();
                    break;
                case 2:
                    DrawSettingsTab();
                    break;
            }
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            selectedTab = GUILayout.Toolbar(selectedTab, tabOptions, EditorStyles.toolbarButton);
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                TrackUpdateReferences();
            }
            
            string pauseButtonLabel = isPaused ? "Resume" : "Pause";
            if (GUILayout.Button(pauseButtonLabel, EditorStyles.toolbarButton))
            {
                isPaused = !isPaused;
            }
            
            GUILayout.EndHorizontal();
        }

        private void DrawUpdateMethodsTab()
        {
            EditorGUILayout.LabelField($"Update Methods ({updateMethods.Count})", EditorStyles.boldLabel);

            // Filter update methods based on search
            var filteredMethods = updateMethods.Where(method => 
                string.IsNullOrEmpty(searchFilter) || 
                method.GameObject.name.Contains(searchFilter, System.StringComparison.OrdinalIgnoreCase) || 
                method.ComponentName.Contains(searchFilter, System.StringComparison.OrdinalIgnoreCase)
            ).ToList();

            // Header row
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("GameObject", headerStyle, GUILayout.Width(200));
            EditorGUILayout.LabelField("Component", headerStyle, GUILayout.Width(150));
            EditorGUILayout.LabelField("Status", headerStyle, GUILayout.Width(80));
            EditorGUILayout.LabelField("References", headerStyle, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            // Scrollable list
            executionScrollPosition = EditorGUILayout.BeginScrollView(executionScrollPosition);
            
            for (int i = 0; i < filteredMethods.Count; i++)
            {
                var method = filteredMethods[i];
                
                // Count references for this method
                int refCount = references.Count(r => r.SourceInstanceID == method.InstanceID);
                
                // Skip if no references and filter is on
                if (refCount == 0 && !showUpdateMethodsWithNoReferences)
                    continue;
                
                // Alternate row colors
                GUI.backgroundColor = i % 2 == 0 ? evenRowColor : oddRowColor;
                EditorGUILayout.BeginHorizontal(rowStyle);
                
                // Button to select the gameObject
                if (GUILayout.Button(method.GameObject.name, EditorStyles.label, GUILayout.Width(200)))
                {
                    Selection.activeGameObject = method.GameObject;
                    EditorGUIUtility.PingObject(method.GameObject);
                }
                
                EditorGUILayout.LabelField(method.ComponentName, GUILayout.Width(150));
                
                // Status indicator
                GUI.color = method.IsActive ? Color.green : Color.gray;
                EditorGUILayout.LabelField(method.IsActive ? "Active" : "Inactive", GUILayout.Width(80));
                GUI.color = Color.white;
                
                // Reference count
                EditorGUILayout.LabelField(refCount.ToString(), GUILayout.Width(80));
                
                EditorGUILayout.EndHorizontal();
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();
        }

        private void DrawReferencesTab()
        {
            EditorGUILayout.LabelField($"References ({references.Count})", EditorStyles.boldLabel);

            // Filter references based on search
            var filteredReferences = references.Where(reference => 
                string.IsNullOrEmpty(searchFilter) || 
                reference.SourceName.Contains(searchFilter, System.StringComparison.OrdinalIgnoreCase) || 
                reference.TargetName.Contains(searchFilter, System.StringComparison.OrdinalIgnoreCase) || 
                reference.FieldName.Contains(searchFilter, System.StringComparison.OrdinalIgnoreCase)
            ).ToList();

            // Header row
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Source", headerStyle, GUILayout.Width(200));
            EditorGUILayout.LabelField("Field", headerStyle, GUILayout.Width(100));
            EditorGUILayout.LabelField("Target", headerStyle, GUILayout.Width(200));
            EditorGUILayout.LabelField("Status", headerStyle, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            // Scrollable list
            referenceScrollPosition = EditorGUILayout.BeginScrollView(referenceScrollPosition);
            
            for (int i = 0; i < filteredReferences.Count; i++)
            {
                var reference = filteredReferences[i];
                
                // Alternate row colors
                GUI.backgroundColor = i % 2 == 0 ? evenRowColor : oddRowColor;
                EditorGUILayout.BeginHorizontal(rowStyle);
                
                // Source object (clickable)
                if (GUILayout.Button(reference.SourceName, EditorStyles.label, GUILayout.Width(200)))
                {
                    var obj = EditorUtility.InstanceIDToObject(reference.SourceInstanceID);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }
                
                // Field name
                EditorGUILayout.LabelField(reference.FieldName, GUILayout.Width(100));
                
                // Target object (clickable)
                if (GUILayout.Button(reference.TargetName, EditorStyles.label, GUILayout.Width(200)))
                {
                    var obj = EditorUtility.InstanceIDToObject(reference.TargetInstanceID);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }
                
                // Status indicator
                GUI.color = reference.IsActive ? Color.green : Color.gray;
                EditorGUILayout.LabelField(reference.IsActive ? "Active" : "Inactive", GUILayout.Width(80));
                GUI.color = Color.white;
                
                EditorGUILayout.EndHorizontal();
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();
        }

        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            // Auto-refresh settings
            EditorGUILayout.BeginHorizontal();
            autoRefresh = EditorGUILayout.Toggle("Auto Refresh", autoRefresh);
            EditorGUILayout.EndHorizontal();
            
            if (autoRefresh)
            {
                EditorGUI.indentLevel++;
                refreshInterval = EditorGUILayout.Slider("Refresh Interval (s)", refreshInterval, 1f, 30f);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Filter settings
            showUpdateMethodsWithNoReferences = EditorGUILayout.Toggle("Show Methods With No References", showUpdateMethodsWithNoReferences);
            
            EditorGUILayout.Space();
            
            // Stats
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Total Update Methods:", updateMethods.Count.ToString());
            EditorGUILayout.LabelField("Total References:", references.Count.ToString());
            EditorGUILayout.LabelField("Active Update Methods:", updateMethods.Count(m => m.IsActive).ToString());
            EditorGUILayout.LabelField("Active References:", references.Count(r => r.IsActive).ToString());
            
            EditorGUILayout.Space();
            
            // Action buttons
            if (GUILayout.Button("Clear All Data"))
            {
                updateMethods.Clear();
                references.Clear();
            }
        }

        // Data structures to store tracking information
        private class MonoBehaviourInfo
        {
            public GameObject GameObject;
            public string ComponentName;
            public int InstanceID;
            public bool IsActive;
        }

        private class ReferenceInfo
        {
            public int SourceInstanceID;
            public string SourceName;
            public int TargetInstanceID;
            public string TargetName;
            public string FieldName;
            public bool IsActive;
        }
    }
}