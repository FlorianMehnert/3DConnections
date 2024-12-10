using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace _3DConnections.Editor
{
    public class RoslynProjectGraphWindow : EditorWindow
    {
        private RoslynGraphView _graphView;

        [MenuItem("Tools/Roslyn/Project Graph")]
        public static void OpenWindow()
        {
            RoslynProjectGraphWindow window = GetWindow<RoslynProjectGraphWindow>();
            window.titleContent = new GUIContent("Roslyn Project Graph");
        }

        private void OnEnable()
        {
            _graphView = new RoslynGraphView
            {
                name = "Roslyn Project Graph"
            };
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
        
            // Load and visualize the scripts
            CreateGraph();
        }

        private void CreateGraph()
        {
            string rootPath = Application.dataPath;
            List<string> files = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories).ToList();

            foreach (var filePath in files)
            {
                try
                {
                    string code = File.ReadAllText(filePath);
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
                    var root = tree.GetRoot();

                    foreach (ClassDeclarationSyntax classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                    {
                        Node node = _graphView.CreateNode(classDeclaration.Identifier.Text);
                        _graphView.AddElement(node);

                        foreach (MethodDeclarationSyntax method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
                        {
                            Node methodNode = _graphView.CreateNode(method.Identifier.Text);
                            _graphView.AddElement(methodNode);
                            _graphView.CreateEdge(node, methodNode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse {filePath}: {ex.Message}");
                }
            }
        }
    }

    public class RoslynGraphView : GraphView
    {
        public RoslynGraphView()
        {
            // styleSheets.Add(Resources.Load<StyleSheet>("GraphViewStyle"));
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
        }

        public Node CreateNode(string nodeName)
        {
            Node node = new Node
            {
                title = nodeName,
                style =
                {
                    width = 150
                }
            };
            node.capabilities |= Capabilities.Movable;
            node.capabilities |= Capabilities.Selectable;
            node.capabilities |= Capabilities.Resizable;
            node.capabilities |= Capabilities.Collapsible;
            node.SetPosition(new Rect(Vector2.zero, new Vector2(150, 200)));

            return node;
        }

        public Edge CreateEdge(Node outputNode, Node inputNode)
        {
            Edge edge = new Edge
            {
                output = outputNode.outputContainer.Children().First() as Port,
                input = inputNode.inputContainer.Children().First() as Port
            };

            edge.input.Connect(edge);
            edge.output.Connect(edge);

            return edge;
        }
    }
}