namespace _3DConnections.Editor.NodeGraph
{
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor.Experimental.GraphView;
    using System.Collections.Generic;
    using UnityEditor;

    public class GameObjectGraphNode : Node
    {
        private GameObject m_GameObject;
        private VisualElement m_ComponentContainer;
        private Button m_ExpandButton;
        private bool m_IsExpanded = false;
        private Dictionary<string, ComponentElement> m_ComponentElements = new Dictionary<string, ComponentElement>();
        private SceneGraphView m_GraphView;
        
        public GameObject GameObject => m_GameObject;
        public Port HierarchyOutputPort { get; private set; }
        public Port HierarchyInputPort { get; private set; }
        public Port ReferenceInputPort { get; private set; }
        
        public GameObjectGraphNode(GameObject gameObject, SceneGraphView graphView)
        {
            m_GameObject = gameObject;
            m_GraphView = graphView;
            title = gameObject.name;
            
            AddToClassList("gameobject-node");
            
            CreatePorts();
            CreateHeader();
            CreateComponentContainer();
            
            RefreshExpandedState();
        }
        
        private void CreatePorts()
        {
            // Hierarchy ports (for parent-child relationships)
            HierarchyInputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(GameObject));
            HierarchyInputPort.portName = "Parent";
            HierarchyInputPort.AddToClassList("hierarchy-port");
            inputContainer.Add(HierarchyInputPort);
            
            HierarchyOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(GameObject));
            HierarchyOutputPort.portName = "Children";
            HierarchyOutputPort.AddToClassList("hierarchy-port");
            outputContainer.Add(HierarchyOutputPort);
            
            // Reference input port (for component references)
            ReferenceInputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(Object));
            ReferenceInputPort.portName = "Referenced By";
            ReferenceInputPort.AddToClassList("reference-port");
            inputContainer.Add(ReferenceInputPort);
        }
        
        private void CreateHeader()
        {
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("node-header-container");
            
            // GameObject icon
            var icon = new VisualElement();
            icon.AddToClassList("gameobject-icon");
            headerContainer.Add(icon);
            
            // Active toggle
            var activeToggle = new Toggle();
            activeToggle.value = m_GameObject.activeInHierarchy;
            activeToggle.RegisterValueChangedCallback(evt => 
            {
                Undo.RecordObject(m_GameObject, "Toggle GameObject Active");
                m_GameObject.SetActive(evt.newValue);
            });
            headerContainer.Add(activeToggle);
            
            // Expand/Collapse button
            m_ExpandButton = new Button(ToggleExpanded);
            m_ExpandButton.text = m_IsExpanded ? "▼" : "▶";
            m_ExpandButton.AddToClassList("expand-button");
            headerContainer.Add(m_ExpandButton);
            
            titleContainer.Add(headerContainer);
        }
        
        private void CreateComponentContainer()
        {
            m_ComponentContainer = new VisualElement();
            m_ComponentContainer.AddToClassList("component-container");
            
            var components = m_GameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component != null)
                {
                    CreateComponentElement(component);
                }
            }
            
            extensionContainer.Add(m_ComponentContainer);
        }
        
        private void CreateComponentElement(Component component)
        {
            var componentElement = new ComponentElement(component, this);
            m_ComponentElements[component.GetType().Name] = componentElement;
            m_ComponentContainer.Add(componentElement);
        }
        
        public void OnReferencePropertyChanged(Component component, string propertyPath)
        {
            // Notify the graph view to update connections
            m_GraphView?.OnReferenceChanged(this, component, propertyPath);
        }
        
        public ComponentElement GetComponentElement(Component component)
        {
            m_ComponentElements.TryGetValue(component.GetType().Name, out var element);
            return element;
        }
        
        public Port GetReferenceOutputPort(Component component, string propertyPath)
        {
            Port port = null;
            var componentElement = GetComponentElement(component);
            componentElement?.ReferenceOutputPorts.TryGetValue(propertyPath, out port);
            return port;
        }
        
        private void ToggleExpanded()
        {
            SetExpanded(!m_IsExpanded);
        }
        
        public void SetExpanded(bool expanded)
        {
            m_IsExpanded = expanded;
            m_ExpandButton.text = m_IsExpanded ? "▼" : "▶";
            
            m_ComponentContainer.style.display = m_IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Update component element visibility
            foreach (var componentElement in m_ComponentElements.Values)
            {
                foreach (var port in componentElement.ReferenceOutputPorts.Values)
                {
                    port.style.display = m_IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
            
            RefreshExpandedState();
        }
        
        public override void OnSelected()
        {
            base.OnSelected();
            Selection.activeGameObject = m_GameObject;
        }
    }
}
