using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using _3DConnections.Runtime.Managers;
using UnityEditorInternal;

namespace _3DConnections.Editor
{
    [CustomEditor(typeof(SceneAnalyzer))]
    public class SceneAnalyzerEditor : UnityEditor.Editor
    {
        private SerializedProperty allowedAssembliesProp;

        // UI state
        private Vector2 assemblyScrollPosition;
        private string assemblySearchFilter = "";
        private bool showEditorAssemblies;
        private bool showPackageAssemblies = true;
        private bool showUnityAssemblies;
        private bool foldoutAssemblySection = true;

        // Assembly cache
        private Assembly[] allProjectAssemblies;
        private Dictionary<string, AssemblyInfo> assemblyInfoCache;

        private class AssemblyInfo
        {
            public string Name;
            public bool IsEditorAssembly;
            public bool IsPackageAssembly;
            public bool IsUnityAssembly;
            public string Path;
        }

        private void OnEnable()
        {
            allowedAssembliesProp = serializedObject.FindProperty("allowedScriptAssemblies");
            RefreshAssemblyCache();
        }

        private void RefreshAssemblyCache()
        {
            allProjectAssemblies = CompilationPipeline.GetAssemblies() ?? Array.Empty<Assembly>();
            assemblyInfoCache = new Dictionary<string, AssemblyInfo>();

            foreach (var asm in allProjectAssemblies)
            {
                var info = new AssemblyInfo
                {
                    Name = asm.name,
                    IsEditorAssembly = (asm.flags & AssemblyFlags.EditorAssembly) != 0,
                    Path = asm.sourceFiles?.FirstOrDefault() ?? ""
                };

                // Determine if it's a package or Unity assembly
                info.IsPackageAssembly = !string.IsNullOrEmpty(info.Path) &&
                                         info.Path.IndexOf("/PackageCache/", StringComparison.OrdinalIgnoreCase) >= 0;
                info.IsUnityAssembly = info.Name.StartsWith("Unity.") ||
                                       info.Name.StartsWith("UnityEngine.") ||
                                       info.Name.StartsWith("UnityEditor.");

                assemblyInfoCache[info.Name] = info;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw all default properties except our custom one
            DrawPropertiesExcluding(serializedObject, "allowedScriptAssemblies");

            EditorGUILayout.Space(10);

            // Custom assembly selector UI
            DrawAssemblySelector();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAssemblySelector()
        {
            // Header with foldout
            using (new EditorGUILayout.HorizontalScope())
            {
                foldoutAssemblySection = EditorGUILayout.Foldout(foldoutAssemblySection,
                    "Allowed Script Assemblies", true, EditorStyles.foldoutHeader);

                GUILayout.FlexibleSpace();

                // Refresh button
                if (GUILayout.Button(new GUIContent("↻", "Refresh assembly list"), GUILayout.Width(25)))
                {
                    RefreshAssemblyCache();
                }
            }

            if (!foldoutAssemblySection) return;

            using (new EditorGUI.IndentLevelScope())
            {
                // Help box
                EditorGUILayout.HelpBox(
                    "Only scripts in the selected assemblies will be analyzed for dynamic references. " +
                    "Leave empty to analyze all assemblies.",
                    MessageType.Info);

                // Current selection summary
                DrawSelectionSummary();

                EditorGUILayout.Space(5);

                // Quick actions
                DrawQuickActions();

                EditorGUILayout.Space(5);

                // Filters and search
                DrawFilters();

                EditorGUILayout.Space(5);

                // Assembly list
                DrawAssemblyList();
            }
        }

        private void DrawSelectionSummary()
        {
            var selectedCount = allowedAssembliesProp.arraySize;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (selectedCount == 0)
                {
                    EditorGUILayout.LabelField("✓ All assemblies allowed (no restrictions)", EditorStyles.boldLabel);
                }
                else
                {
                    EditorGUILayout.LabelField($"✓ {selectedCount} assemblies selected", EditorStyles.boldLabel);

                    // Show selected assembly chips
                    if (selectedCount <= 0 || selectedCount > 10) return;
                    EditorGUILayout.Space(3);
                    DrawSelectedChips();
                }
            }
        }

        private void DrawSelectedChips()
        {
            var toRemove = new List<int>();

            for (int i = 0; i < allowedAssembliesProp.arraySize; i++)
            {
                var element = allowedAssembliesProp.GetArrayElementAtIndex(i);
                var asmName = element.stringValue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    // Chip style box
                    var chipStyle = new GUIStyle("button")
                    {
                        margin = new RectOffset(2, 2, 2, 2),
                        padding = new RectOffset(4, 20, 2, 2),
                        fontSize = 10
                    };

                    if (GUILayout.Button($"{asmName} ×", chipStyle, GUILayout.ExpandWidth(false)))
                    {
                        toRemove.Add(i);
                    }

                    if (i % 3 != 2) continue; // Break line every 3 chips
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }

            // Remove clicked items
            for (int i = toRemove.Count - 1; i >= 0; i--)
            {
                allowedAssembliesProp.DeleteArrayElementAtIndex(toRemove[i]);
            }
        }

        private void DrawQuickActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Project Assemblies"))
                {
                    AddProjectAssemblies();
                }

                if (GUILayout.Button("Add from .asmdef"))
                {
                    AddFromAsmdefAssets();
                }

                if (GUILayout.Button("Clear All"))
                {
                    allowedAssembliesProp.ClearArray();
                }
            }

            // Preset buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Presets:", GUILayout.Width(50));

                if (GUILayout.Button("Game Only"))
                {
                    ApplyPreset(new[] { "Assembly-CSharp", "Assembly-CSharp-firstpass" });
                }

                if (GUILayout.Button("Game + Plugins"))
                {
                    ApplyPreset(GetNonUnityAssemblies().ToArray());
                }
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // Search field
                GUILayout.Label("Search:", GUILayout.Width(50));
                assemblySearchFilter = EditorGUILayout.TextField(assemblySearchFilter, EditorStyles.toolbarSearchField);

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    assemblySearchFilter = "";
                    GUI.FocusControl(null);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                showEditorAssemblies = GUILayout.Toggle(showEditorAssemblies, "Editor", "Button", GUILayout.Width(60));
                showPackageAssemblies =
                    GUILayout.Toggle(showPackageAssemblies, "Packages", "Button", GUILayout.Width(70));
                showUnityAssemblies = GUILayout.Toggle(showUnityAssemblies, "Unity", "Button", GUILayout.Width(50));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Select Visible", GUILayout.Width(100)))
                {
                    SelectAllVisible();
                }
            }
        }

        private void DrawAssemblyList()
        {
            var currentSelection = GetCurrentSelection();
            var filteredAssemblies = GetFilteredAssemblies();

            using var scrollView = new EditorGUILayout.ScrollViewScope(assemblyScrollPosition,
                GUILayout.MaxHeight(300));
            assemblyScrollPosition = scrollView.scrollPosition;

            foreach (var asmName in filteredAssemblies)
            {
                if (!assemblyInfoCache.TryGetValue(asmName, out var info))
                    continue;

                bool isSelected = currentSelection.Contains(asmName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    // Checkbox
                    bool newSelected = EditorGUILayout.ToggleLeft("", isSelected, GUILayout.Width(20));

                    // Assembly name with icon
                    var icon = GetAssemblyIcon(info);
                    var content = new GUIContent($" {asmName}", icon);

                    if (GUILayout.Button(content, EditorStyles.label))
                    {
                        newSelected = !isSelected;
                    }

                    // Type badges
                    GUILayout.FlexibleSpace();
                    DrawAssemblyBadges(info);

                    // Handle selection change
                    if (newSelected == isSelected) continue;
                    if (newSelected)
                        AddAssembly(asmName);
                    else
                        RemoveAssembly(asmName);
                }
            }

            if (filteredAssemblies.Count == 0)
            {
                EditorGUILayout.HelpBox("No assemblies match the current filters.", MessageType.Info);
            }
        }

        private void DrawAssemblyBadges(AssemblyInfo info)
        {
            var miniStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                margin = new RectOffset(2, 2, 0, 0)
            };

            if (info.IsEditorAssembly)
                GUILayout.Label("[Editor]", miniStyle);
            if (info.IsPackageAssembly)
                GUILayout.Label("[Package]", miniStyle);
            if (info.IsUnityAssembly)
                GUILayout.Label("[Unity]", miniStyle);
        }

        private Texture GetAssemblyIcon(AssemblyInfo info)
        {
            if (info.IsEditorAssembly)
                return EditorGUIUtility.IconContent("cs Script Icon").image;
            if (info.IsPackageAssembly)
                return EditorGUIUtility.IconContent("Package Manager").image;
            if (info.IsUnityAssembly)
                return EditorGUIUtility.IconContent("UnityLogo").image;

            return EditorGUIUtility.IconContent("cs Script Icon").image;
        }

        private HashSet<string> GetCurrentSelection()
        {
            var selection = new HashSet<string>();
            for (int i = 0; i < allowedAssembliesProp.arraySize; i++)
            {
                selection.Add(allowedAssembliesProp.GetArrayElementAtIndex(i).stringValue);
            }

            return selection;
        }

        private List<string> GetFilteredAssemblies()
        {
            return assemblyInfoCache.Values
                .Where(info =>
                {
                    // Apply type filters
                    if (!showEditorAssemblies && info.IsEditorAssembly) return false;
                    if (!showPackageAssemblies && info.IsPackageAssembly) return false;
                    if (!showUnityAssemblies && info.IsUnityAssembly) return false;

                    // Apply search filter
                    if (!string.IsNullOrEmpty(assemblySearchFilter))
                    {
                        return info.Name.IndexOf(assemblySearchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                    }

                    return true;
                })
                .Select(info => info.Name)
                .OrderBy(name => name)
                .ToList();
        }

        private void AddAssembly(string assemblyName)
        {
            var currentSelection = GetCurrentSelection();
            if (currentSelection.Contains(assemblyName)) return;
            allowedAssembliesProp.InsertArrayElementAtIndex(allowedAssembliesProp.arraySize);
            allowedAssembliesProp.GetArrayElementAtIndex(allowedAssembliesProp.arraySize - 1).stringValue =
                assemblyName;
        }

        private void RemoveAssembly(string assemblyName)
        {
            for (int i = 0; i < allowedAssembliesProp.arraySize; i++)
            {
                if (allowedAssembliesProp.GetArrayElementAtIndex(i).stringValue != assemblyName) continue;
                allowedAssembliesProp.DeleteArrayElementAtIndex(i);
                break;
            }
        }

        private void SelectAllVisible()
        {
            var visible = GetFilteredAssemblies();
            foreach (var asmName in visible)
            {
                AddAssembly(asmName);
            }
        }

        private void AddProjectAssemblies()
        {
            var projectAssemblies = assemblyInfoCache.Values
                .Where(info => !info.IsUnityAssembly && !info.IsPackageAssembly && !info.IsEditorAssembly)
                .Select(info => info.Name);

            foreach (var asmName in projectAssemblies)
            {
                AddAssembly(asmName);
            }
        }

        private void AddFromAsmdefAssets()
        {
            var asmdefAssets = Selection.GetFiltered<AssemblyDefinitionAsset>(SelectionMode.Assets);
            if (asmdefAssets.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection",
                    "Please select one or more .asmdef files in the Project window first.", "OK");
                return;
            }

            foreach (var asmdef in asmdefAssets)
            {
                if (asmdef)
                {
                    AddAssembly(asmdef.name);
                }
            }
        }

        private void ApplyPreset(string[] assemblyNames)
        {
            allowedAssembliesProp.ClearArray();
            foreach (var name in assemblyNames)
            {
                if (assemblyInfoCache.ContainsKey(name))
                {
                    AddAssembly(name);
                }
            }
        }

        private IEnumerable<string> GetNonUnityAssemblies()
        {
            return assemblyInfoCache.Values
                .Where(info => !info.IsUnityAssembly)
                .Select(info => info.Name);
        }
    }
}