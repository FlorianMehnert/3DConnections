using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor.UIElements;

namespace _3DConnections.Editor.NodeGraph
{
    public class SceneNodeGraphWindow : EditorWindow
    {
        private SceneGraphView m_GraphView;
        private SliderInt m_HierarchyDepthSlider;
        private Label m_DepthLabel;
        private ToolbarSearchField m_SearchField;
        private ToolbarButton m_FocusModeButton;
        private bool m_IsFocusMode = false;
        private GameObjectGraphNode m_LastSelectedNode = null;

        [MenuItem("Tools/Scene Node Graph")]
        public static void ShowWindow()
        {
            GetWindow<SceneNodeGraphWindow>("Scene Node Graph");
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // Create main toolbar
            var toolbar = new Toolbar();

            var refreshButton = new ToolbarButton(() => m_GraphView?.RefreshGraph()) { text = "Refresh Graph" };
            var clearButton = new ToolbarButton(() => m_GraphView?.ClearGraph()) { text = "Clear" };
            var layoutButton = new ToolbarButton(() => m_GraphView?.ApplySugiyamaLayout()) { text = "Apply Layout" };
            var collapseAllButton = new ToolbarButton(() => m_GraphView?.CollapseAllNodes()) { text = "Collapse All" };
            var expandAllButton = new ToolbarButton(() => m_GraphView?.ExpandAllNodes()) { text = "Expand All" };

            // Search field
            m_SearchField = new ToolbarSearchField();
            m_SearchField.style.minWidth = 200;
            m_SearchField.RegisterValueChangedCallback(evt => OnSearchChanged(evt.newValue));

            // Focus mode button
            m_FocusModeButton = new ToolbarButton(ToggleFocusMode) { text = "Focus Mode: Off" };
            m_FocusModeButton.AddToClassList("focus-mode-button");

            toolbar.Add(refreshButton);
            toolbar.Add(clearButton);
            toolbar.Add(layoutButton);
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(m_SearchField);
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(m_FocusModeButton);
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            toolbar.Add(collapseAllButton);
            toolbar.Add(expandAllButton);

            root.Add(toolbar);

            // Create hierarchy depth control toolbar
            var depthToolbar = new Toolbar();

            m_DepthLabel = new Label("Max Depth: All");
            m_DepthLabel.style.minWidth = 100;
            depthToolbar.Add(m_DepthLabel);

            m_HierarchyDepthSlider = new SliderInt("Hierarchy Depth", 0, 10, 0);
            m_HierarchyDepthSlider.style.flexGrow = 1;
            m_HierarchyDepthSlider.style.minWidth = 200;
            m_HierarchyDepthSlider.RegisterValueChangedCallback(evt => OnDepthSliderChanged(evt.newValue));
            depthToolbar.Add(m_HierarchyDepthSlider);

            var showAllButton = new ToolbarButton(() =>
            {
                m_HierarchyDepthSlider.value = 0;
                OnDepthSliderChanged(0);
            }) { text = "Show All" };
            depthToolbar.Add(showAllButton);

            root.Add(depthToolbar);

            // Create graph view (fills remaining space)
            m_GraphView = new SceneGraphView
            {
                style = { flexGrow = 1 }
            };
            root.Add(m_GraphView);

            // Calculate max depth and set slider range
            UpdateSliderRange();

            // Initial graph generation
            m_GraphView.RefreshGraph();
        }

        private void OnSearchChanged(string searchText)
        {
            m_GraphView?.SetSearchFilter(searchText);
        }

        private void ShowMessage(string message)
        {
            EditorUtility.DisplayDialog("Scene Node Graph", message, "OK");
        }

        private void OnDepthSliderChanged(int maxDepth)
        {
            if (maxDepth == 0)
            {
                m_DepthLabel.text = "Max Depth: All";
                m_GraphView?.SetHierarchyDepthFilter(-1);
            }
            else
            {
                m_DepthLabel.text = $"Max Depth: {maxDepth}";
                m_GraphView?.SetHierarchyDepthFilter(maxDepth);
            }
        }

        private void UpdateSliderRange()
        {
            int maxDepth = CalculateMaxHierarchyDepth();
            m_HierarchyDepthSlider.highValue = maxDepth;
        }

        private int CalculateMaxHierarchyDepth()
        {
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            int maxDepth = 0;

            foreach (var rootObj in rootObjects)
            {
                maxDepth = Mathf.Max(maxDepth, GetGameObjectDepth(rootObj, 0));
            }

            return maxDepth;
        }

        private int GetGameObjectDepth(GameObject gameObject, int currentDepth)
        {
            int maxChildDepth = currentDepth;

            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                var child = gameObject.transform.GetChild(i).gameObject;
                maxChildDepth = Mathf.Max(maxChildDepth, GetGameObjectDepth(child, currentDepth + 1));
            }

            return maxChildDepth;
        }

        private void OnSelectionChanged(List<ISelectable> selection)
        {
            if (selection.Count == 1 && selection[0] is GameObjectGraphNode selectedNode)
            {
                m_LastSelectedNode = selectedNode;

                // If focus mode is on, automatically update focus to selected node
                if (m_IsFocusMode)
                {
                    m_GraphView.FocusOnNode(selectedNode);
                }
            }
        }

        private void ToggleFocusMode()
        {
            m_IsFocusMode = !m_IsFocusMode;

            if (m_IsFocusMode)
            {
                m_FocusModeButton.text = "Focus Mode: On";
                m_FocusModeButton.AddToClassList("focus-mode-active");

                // Use last selected node or current selection for focus
                if (m_LastSelectedNode != null)
                {
                    m_GraphView.FocusOnNode(m_LastSelectedNode);
                }
                else if (m_GraphView.selection.Count == 1 &&
                         m_GraphView.selection.FirstOrDefault() is GameObjectGraphNode selectedNode)
                {
                    m_GraphView.FocusOnNode(selectedNode);
                    m_LastSelectedNode = selectedNode;
                }
                else
                {
                    // No selection, turn focus mode back off
                    m_IsFocusMode = false;
                    m_FocusModeButton.text = "Focus Mode: Off";
                    m_FocusModeButton.RemoveFromClassList("focus-mode-active");
                    ShowMessage("Please select a GameObject node to use Focus Mode");
                }
            }
            else
            {
                m_FocusModeButton.text = "Focus Mode: Off";
                m_FocusModeButton.RemoveFromClassList("focus-mode-active");
                m_GraphView.ClearFocusMode();
            }
        }
    }
}
#endif