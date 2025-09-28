using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;

namespace _3DConnections.Editor.NodeGraph
{
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor;
    using System.Collections.Generic;

    public class ComponentElement : VisualElement
    {
        private Component m_Component;
        private GameObjectGraphNode m_ParentNode;
        private SceneGraphView m_GraphView;
        private VisualElement m_PropertyContainer;
        private bool m_IsExpanded;
        private Button m_ExpandButton;
        private Dictionary<string, Port> m_ReferenceOutputPorts = new();
        private Dictionary<string, PropertyField> m_PropertyFields = new();

        public Component Component => m_Component;
        public Dictionary<string, Port> ReferenceOutputPorts => m_ReferenceOutputPorts;

        public ComponentElement(Component component, GameObjectGraphNode parentNode, SceneGraphView graphView)
        {
            m_Component = component;
            m_ParentNode = parentNode;
            m_GraphView = graphView;

            AddToClassList("component-element");

            CreateHeader();
            CreatePropertyContainer();
        }

        private void CreateHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("component-header");

            // Component icon
            var icon = new VisualElement();
            icon.AddToClassList("component-icon");
            header.Add(icon);

            // Component name
            var nameLabel = new Label(m_Component.GetType().Name);
            nameLabel.AddToClassList("component-name");
            header.Add(nameLabel);

            // Expand button for properties
            m_ExpandButton = new Button(ToggleProperties);
            m_ExpandButton.text = "▶";
            m_ExpandButton.AddToClassList("property-expand-button");
            header.Add(m_ExpandButton);

            // Enable/disable toggle for components that support it
            if (m_Component is Behaviour behaviour)
            {
                var enabledToggle = new Toggle();
                enabledToggle.value = behaviour.enabled;
                enabledToggle.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(behaviour, "Toggle Component Enabled");
                    behaviour.enabled = evt.newValue;
                });
                header.Add(enabledToggle);
            }

            Add(header);
        }

        private void CreatePropertyContainer()
        {
            m_PropertyContainer = new VisualElement();
            m_PropertyContainer.AddToClassList("property-container");
            m_PropertyContainer.style.display = DisplayStyle.None;

            CreatePropertyElements();

            Add(m_PropertyContainer);
        }

        private void CreatePropertyElements()
        {
            var serializedObject = new SerializedObject(m_Component);
            var property = serializedObject.GetIterator();

            if (property.NextVisible(true))
            {
                do
                {
                    if (ShouldShowProperty(property))
                    {
                        CreatePropertyElement(property);
                    }
                } while (property.NextVisible(false));
            }
        }

        private bool ShouldShowProperty(SerializedProperty property)
        {
            // Skip script reference and other internal properties
            return property.name != "m_Script" &&
                   property.name != "m_GameObject" &&
                   property.name != "m_Enabled" &&
                   !property.name.StartsWith("m_");
        }

        private void CreatePropertyElement(SerializedProperty property)
        {
            var propertyElement = new VisualElement();
            propertyElement.AddToClassList("property-element");

            // Add connection port for object references
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                var outputPort = m_ParentNode.InstantiatePort(Orientation.Horizontal, Direction.Output,
                    Port.Capacity.Single, typeof(Object));
                outputPort.portName = property.displayName;
                outputPort.AddToClassList("reference-port");
                outputPort.userData = new ReferencePortData
                {
                    Component = m_Component,
                    PropertyName = property.name,
                    PropertyPath = property.propertyPath
                };

                m_ReferenceOutputPorts[property.propertyPath] = outputPort;

                var connectionIndicator = new VisualElement();
                connectionIndicator.AddToClassList("connection-indicator");
                connectionIndicator.Add(outputPort);
                propertyElement.Add(connectionIndicator);
            }

            var propertyField = new PropertyField(property.Copy());
            propertyField.Bind(property.serializedObject);
            m_PropertyFields[property.propertyPath] = propertyField;

            // Listen for property changes to update connections
            propertyField.RegisterValueChangeCallback(evt => OnPropertyChanged(property.propertyPath));

            propertyElement.Add(propertyField);
            m_PropertyContainer.Add(propertyElement);
        }

        private void OnPropertyChanged(string propertyPath)
        {
            // Notify parent node that a reference has changed
            m_ParentNode.OnReferencePropertyChanged(m_Component, propertyPath);
        }

        public void UpdatePropertyValue(string propertyPath, Object newValue)
        {
            var serializedObject = new SerializedObject(m_Component);
            var property = serializedObject.FindProperty(propertyPath);
            if (property != null)
            {
                Undo.RecordObject(m_Component, $"Update {property.displayName}");
                property.objectReferenceValue = newValue;
                serializedObject.ApplyModifiedProperties();

                // Update the property field display
                if (m_PropertyFields.TryGetValue(propertyPath, out var propertyField))
                {
                    propertyField.Bind(serializedObject);
                }
            }
        }

        public void ColorEdges(bool enable)
        {
            var color = enable ? Color.red : Color.white;
            foreach (var port in m_ReferenceOutputPorts.Values)
            {
                {
                    port.portColor = color;

                    // Color all connected edges
                    foreach (var edge in port.connections)
                    {
                        edge.edgeControl.inputColor = color;
                        edge.edgeControl.outputColor = color;
                        // edge.MarkDirtyRepaint();
                    }
                }
            }
        }
        
        private void ToggleProperties()
        {
            m_IsExpanded = !m_IsExpanded;
            m_ExpandButton.text = m_IsExpanded ? "▼" : "▶";
            m_PropertyContainer.style.display = m_IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;

            // Update port visibility
            foreach (var port in m_ReferenceOutputPorts.Values)
            {
                port.style.display = m_IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            }
    
            ColorEdges(m_IsExpanded);
    
            // Notify parent node to handle edge rerouting
            m_ParentNode.OnComponentElementToggled(this);
        }



        public bool IsExpanded => m_IsExpanded;
    }

    public class ReferencePortData
    {
        public Component Component;
        public string PropertyName;
        public string PropertyPath;
    }
}