using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _3DConnections.Runtime.Managers;
using _3DConnections.Runtime.Nodes.Connection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;
using cols = _3DConnections.Runtime.ScriptableObjects.NodeColorsScriptableObject;

namespace _3DConnections.Runtime.Analysis
{
    public class ComponentReferenceAnalyzer : IComponentReferenceAnalyzer
    {
        private readonly IFileLocator _fileLocator;
        private readonly ITypeResolver _typeResolver;
        private readonly ILogger _logger;
        private readonly AnalysisFilterSettings _settings;
        private readonly Dictionary<Type, List<ComponentReference>> _dynamicComponentReferences = new();
        private readonly IProgressReporter _progressReporter;

        public ComponentReferenceAnalyzer(IFileLocator fileLocator, ITypeResolver typeResolver, 
            ILogger logger, AnalysisFilterSettings settings, IProgressReporter progressReporter = null)
        {
            _fileLocator = fileLocator;
            _typeResolver = typeResolver;
            _logger = logger;
            _settings = settings;
            _progressReporter = progressReporter ?? new NullProgressReporter();
        }
        
        public void AnalyzeAllComponents(IEnumerable<Type> monoBehaviourTypes)
        {
            var types = monoBehaviourTypes.ToArray();
            _progressReporter.StartOperation("Component Reference Analysis", types.Length);

            for (int i = 0; i < types.Length; i++)
            {
                _progressReporter.ReportProgress("Analyzing Components", i + 1, types.Length, 
                    types[i].Name);
                AnalyzeComponentReferences(types[i]);
            }

            _progressReporter.CompleteOperation();
        }

        public List<ComponentReference> AnalyzeComponentReferences(Type monoBehaviourType)
        {
            try
            {
                var sourceFile = _fileLocator.FindSourceFileForType(monoBehaviourType);
                if (string.IsNullOrEmpty(sourceFile)) return new List<ComponentReference>();

                var sourceCode = File.ReadAllText(sourceFile);
                var references = AnalyzeSourceCodeForComponentReferences(sourceCode, sourceFile);

                if (references.Count <= 0) return references;
                _dynamicComponentReferences[monoBehaviourType] = references;
                _logger.Log($"Found {references.Count} dynamic references in {monoBehaviourType.Name}");

                return references;
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Could not analyze {monoBehaviourType.Name}: {e.Message}");
                return new List<ComponentReference>();
            }
        }


        private List<ComponentReference> AnalyzeSourceCodeForComponentReferences(string sourceCode, string sourceFile)
        {
            var references = new List<ComponentReference>();

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = syntaxTree.GetRoot();

                var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

                foreach (var invocation in invocations)
                {
                    var methodName = ExtractMethodName(invocation);
                    if (methodName == null) continue;

                    bool isAddComponent = _settings.ShowAddComponentCalls && methodName.StartsWith("AddComponent");
                    bool isGetComponent = _settings.ShowGetComponentCalls && methodName.StartsWith("GetComponent");

                    if (!isAddComponent && !isGetComponent) continue;

                    var componentType = ExtractComponentTypeFromInvocation(invocation);
                    if (componentType == null) continue;

                    var lineNumber = syntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;

                    references.Add(new ComponentReference
                    {
                        ReferencedComponentType = componentType,
                        MethodName = methodName,
                        LineNumber = lineNumber,
                        SourceFile = sourceFile
                    });
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Error analyzing source code in {sourceFile}: {e.Message}");
            }

            return references;
        }

        private string ExtractMethodName(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
                _ => null
            };
        }

        private Type ExtractComponentTypeFromInvocation(InvocationExpressionSyntax invocation)
        {
            // Handle generic method calls like AddComponent<Rigidbody>()
            if (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName })
            {
                var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                if (typeArg != null)
                {
                    var typeName = typeArg.ToString();
                    return _typeResolver.FindTypeByName(typeName);
                }
            }

            // Handle method calls with Type parameter like AddComponent(typeof(Rigidbody))
            if (invocation.ArgumentList.Arguments.Count <= 0) return null;
            {
                var firstArg = invocation.ArgumentList.Arguments[0].Expression;
                if (firstArg is not TypeOfExpressionSyntax typeOfExpr) return null;
                var typeName = typeOfExpr.Type.ToString();
                return _typeResolver.FindTypeByName(typeName);
            }
        }
        
        public void CreateDynamicConnections(INodeGraphManager nodeManager, AnalysisFilterSettings filterSettings)
        {
            foreach (var (sourceType, references) in _dynamicComponentReferences)
            {
                var filteredReferences = references.Where(r => 
                    ShouldIncludeComponentCall(r.MethodName, filterSettings)).ToList();
            
                if (!filteredReferences.Any()) continue;

                var sourceNode = nodeManager.FindNodeByType(sourceType);
                if (sourceNode == null) continue;

                foreach (var reference in filteredReferences)
                {
                    var targetNode = nodeManager.FindNodeByType(reference.ReferencedComponentType);
            
                    if (targetNode == null)
                    {
                        targetNode = nodeManager.CreateNode(null, 1, sourceNode, false, reference.ReferencedComponentType);
                    }

                    if (!targetNode) continue;
            
                    var connectionInfo = GetEnhancedComponentConnectionInfo(reference, filterSettings);
                    var connection = sourceNode.ConnectNodes(targetNode, connectionInfo.Color, 1, 
                        connectionInfo.ConnectionType, cols.MaxWidthHierarchy);
            
                    if (connection != null && filterSettings.ShowCodeReferences)
                    {
                        var edgeType = connection.lineRenderer.gameObject.GetComponent<EdgeType>();
                        if (edgeType)
                        {
                            edgeType.SetCodeReference(
                                reference.SourceFile,
                                reference.LineNumber,
                                reference.MethodName,
                                sourceType.Name
                            );
                        }
                    }
                }
            }
        }
        private bool ShouldIncludeComponentCall(string methodName, AnalysisFilterSettings settings)
        {
            if (methodName.StartsWith("AddComponent")) return settings.ShowAddComponentCalls;
            if (methodName.StartsWith("GetComponent")) return settings.ShowGetComponentCalls;
            return settings.ShowOtherComponentReferences;
        }

        private (Color Color, string ConnectionType) GetEnhancedComponentConnectionInfo(ComponentReference reference, AnalysisFilterSettings settings)
        {
            if (!settings.DifferentiateComponentCallTypes)
            {
                return (cols.DimColor(cols.DynamicComponentConnection, 0.7f), "dynamicComponentConnection");
            }

            return reference.MethodName switch
            {
                var method when method.StartsWith("AddComponent") => 
                    (new Color(0.9f, 0.3f, 0.3f, 0.8f), "addComponentCall"),
                var method when method.StartsWith("GetComponent") => 
                    (new Color(0.3f, 0.9f, 0.3f, 0.8f), "getComponentCall"),
                _ => (cols.DimColor(cols.DynamicComponentConnection, 0.7f), "otherComponentCall")
            };
        }
    }

    [Serializable]
    public class AnalysisFilterSettings
    {
        [Header("Event Filtering")]
        public bool ShowUnityEvents = true;
        public bool ShowUnityActions = true;
        public bool ShowSystemActions = false;
        public bool ShowCustomDelegates = false;
        public bool ShowSystemEventHandlers = false;
        public bool ShowFuncDelegates = false;
    
        [Header("Component Reference Filtering")]
        public bool ShowAddComponentCalls = true;
        public bool ShowGetComponentCalls = true;
        public bool ShowOtherComponentReferences = false;
    
        [Header("Connection Type Enhancement")]
        public bool DifferentiateEventTypes = true;
        public bool DifferentiateComponentCallTypes = true;
        public bool ShowCodeReferences = true;
    
        [Header("Output Filtering")]
        public bool FilteredOutputOnly = false; // When true, only shows filtered types
    }
}
