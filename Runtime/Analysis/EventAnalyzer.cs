// EventAnalyzer.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _3DConnections.Runtime.Managers;
using _3DConnections.Runtime.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;
using cols = _3DConnections.Runtime.ScriptableObjects.NodeColorsScriptableObject;

namespace _3DConnections.Runtime.Analysis
{
    public class EventAnalyzer : IEventAnalyzer
    {
        private readonly IFileLocator _fileLocator;
        private readonly ITypeResolver _typeResolver;
        private readonly ILogger _logger;
        private readonly IProgressReporter _progressReporter;

        private readonly Dictionary<Type, List<EventInfo>> _eventPublishers = new();
        private readonly List<EventInvocation> _eventInvocations = new();
        private readonly List<EventSubscription> _eventSubscriptions = new();
        private readonly Dictionary<string, Type> _typeNameToTypeMap = new();
        private readonly Dictionary<Type, List<EventInfo>> EventPublishersEnhanced = new();

        public EventAnalyzer(IFileLocator fileLocator, ITypeResolver typeResolver, ILogger logger, IProgressReporter progressReporter = null)
        {
            _fileLocator = fileLocator;
            _typeResolver = typeResolver;
            _logger = logger;
            _progressReporter = progressReporter ?? new NullProgressReporter();
        }

        private List<EventInvocation> AnalyzeSourceCodeForEventInvocations(string sourceCode, Type invokerType)
        {
            var invocations = new List<EventInvocation>();

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = syntaxTree.GetRoot();

                // Look for direct Invoke() calls
                var invocationExpressions = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
                var invocationExpressionSyntaxes = invocationExpressions as InvocationExpressionSyntax[] ??
                                                   invocationExpressions.ToArray();
                invocations.AddRange(from invocation in invocationExpressionSyntaxes
                    let memberAccess = invocation.Expression as MemberAccessExpressionSyntax
                    where memberAccess?.Name.Identifier.ValueText == "Invoke"
                    let fullExpression = memberAccess.Expression.ToString()
                    let lineNumber = syntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1
                    let matchedEvent = FindMatchingEventForInvocation(fullExpression)
                    where matchedEvent.HasValue
                    select new EventInvocation
                    {
                        InvokerType = invokerType,
                        EventName = matchedEvent.Value.eventName,
                        MethodPattern = fullExpression + ".Invoke()",
                        TargetType = matchedEvent.Value.targetType,
                        LineNumber = lineNumber,
                        SourceFile = FindSourceFileForType(invokerType),
                        IsIndirect = fullExpression.Contains("."),
                        AccessPath = fullExpression
                    });

                // Look for method invocations that might trigger events (like InvokeOnAllCountChanged)
                foreach (var invocation in invocationExpressionSyntaxes)
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess2) continue;
                    var methodName = memberAccess2.Name.Identifier.ValueText;

                    // Check if this is an invoke method pattern
                    if (!methodName.StartsWith("Invoke") || methodName.Length <= 6) continue;
                    var lineNumber = syntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                    var fullExpression = memberAccess2.ToString();
                    var targetType = ResolveTypeFromExpression(memberAccess2.Expression);

                    // Extract the event name from the method name
                    var potentialEventName = methodName[6..]; // Remove "Invoke" prefix

                    invocations.Add(new EventInvocation
                    {
                        InvokerType = invokerType,
                        EventName = potentialEventName,
                        MethodPattern = fullExpression,
                        TargetType = targetType ?? invokerType,
                        LineNumber = lineNumber,
                        SourceFile = FindSourceFileForType(invokerType),
                        IsIndirect = memberAccess2.Expression.ToString().Contains("."),
                        AccessPath = memberAccess2.Expression.ToString()
                    });
                }

                // Also look for indirect invocations through property/field access
                AnalyzeIndirectInvocations(root, invokerType, invocations);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error analyzing event invocations for {invokerType.Name}: {e.Message}");
            }

            return invocations;
        }

        private Type ResolveTypeFromExpression(ExpressionSyntax expression)
        {
            var expressionString = expression.ToString();

            // Handle Instance pattern
            if (expressionString.Contains("Instance"))
            {
                // Extract the type name before .Instance
                var parts = expressionString.Split('.');
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (parts[i + 1] == "Instance" && _typeNameToTypeMap.TryGetValue(parts[i], out var type))
                    {
                        return type;
                    }
                }
            }

            // Handle direct type references
            if (_typeNameToTypeMap.TryGetValue(expressionString, out var directType))
            {
                return directType;
            }

            // Handle this/base references
            if (expressionString == "this" || expressionString == "base")
            {
                return null; // Will be resolved to invokerType later
            }

            return null;
        }

        private string FindSourceFileForType(Type type)
        {
#if UNITY_EDITOR
            // Try to find the script asset
            var guids = AssetDatabase.FindAssets($"t:MonoScript {type.Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script && script.GetClass() == type)
                {
                    return Path.GetFullPath(path);
                }
            }
#endif
            return null;
        }

        private string ExtractEventNameFromPath(string accessPath)
        {
            var parts = accessPath.Split('.');
            return parts.Length > 0 ? parts[^1] : "UnknownEvent";
        }


        private (string eventName, Type targetType)? FindMatchingEventForInvocation(string expression)
        {
            // Try to match expressions like "OnGoCountChanged" to known events
            foreach (var kvp in EventPublishersEnhanced)
            {
                foreach (var eventInfo in kvp.Value)
                {
                    if (expression.Contains(eventInfo.EventName))
                    {
                        return (eventInfo.EventName, kvp.Key);
                    }
                }
            }

            return null;
        }

        public void AnalyzeEvents(IEnumerable<Type> monoBehaviourTypes)
        {
            var behaviourTypes = monoBehaviourTypes.ToArray();
            _progressReporter.StartOperation("Event Analysis", behaviourTypes.Length * 2); // Two passes

            BuildTypeNameMapping(behaviourTypes);

            // First pass: Find all event publishers
            for (int i = 0; i < behaviourTypes.Length; i++)
            {
                _progressReporter.ReportProgress("Finding Event Publishers", i + 1, behaviourTypes.Length, 
                    behaviourTypes[i].Name);
                try
                {
                    var sourceFile = _fileLocator.FindSourceFileForType(behaviourTypes[i]);
                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    var sourceCode = File.ReadAllText(sourceFile);
                    AnalyzeEventPublishers(sourceCode, behaviourTypes[i]);
                }
                catch (Exception e)
                {
                    _logger.LogWarning($"Could not analyze event publishers in {behaviourTypes[i].Name}: {e.Message}");
                }
            }

            // Second pass: Find all event subscriptions and invocations
            for (int i = 0; i < behaviourTypes.Length; i++)
            {
                _progressReporter.ReportProgress("Finding Event Subscriptions", 
                    behaviourTypes.Length + i + 1, behaviourTypes.Length * 2, behaviourTypes[i].Name);
                try
                {
                    var sourceFile = _fileLocator.FindSourceFileForType(behaviourTypes[i]);
                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    var sourceCode = File.ReadAllText(sourceFile);
                    var subscriptions = AnalyzeSourceCodeForEventSubscriptions(sourceCode, behaviourTypes[i]);
                    _eventSubscriptions.AddRange(subscriptions);

                    var invocations = AnalyzeSourceCodeForEventInvocations(sourceCode, behaviourTypes[i]);
                    _eventInvocations.AddRange(invocations);
                }
                catch (Exception e)
                {
                    _logger.LogWarning($"Could not analyze events in {behaviourTypes[i].Name}: {e.Message}");
                }
            }
            
            _progressReporter.CompleteOperation();

            _logger.Log(
                $"Found {_eventPublishers.Count} event publishers, {_eventInvocations.Count} invocations, and {_eventSubscriptions.Count} subscriptions");
        }

        private bool IsKnownEventType(string typeName)
        {
            return typeName.Contains("Event") || typeName.Contains("Action") ||
                   typeName.Contains("Delegate") || typeName.Contains("Func");
        }

        private EventType DetermineEventType(string typeName)
        {
            if (typeName.Contains("UnityAction")) return EventType.UnityAction;
            if (typeName.Contains("Action")) return EventType.Action;
            if (typeName.Contains("UnityEvent")) return EventType.UnityEvent;
            if (typeName.Contains("UnityEvent")) return EventType.UnityEvent;
            if (typeName.Contains("EventHandler")) return EventType.SystemEventHandler;
            if (typeName.Contains("Func")) return EventType.FuncDelegate;
            return EventType.CustomDelegate;
        }

        public void CreateEventConnections(INodeGraphManager nodeManager)
        {
            // Create connections for event invocations
            foreach (var invocation in _eventInvocations)
            {
                var invokerNode = nodeManager.FindNodeByType(invocation.InvokerType);
                if (!invokerNode) continue;

                var targetNode = nodeManager.FindNodeByType(invocation.TargetType);
                if (!targetNode)
                {
                    targetNode = nodeManager.CreateNode(null, 1, invokerNode, false, invocation.TargetType);
                }

                if (!targetNode) continue;

                var invocationColor = invocation.IsIndirect
                    ? new Color(cols.DynamicComponentConnection.r * 1.2f, cols.DynamicComponentConnection.g * 0.8f,
                        cols.DynamicComponentConnection.b, 0.9f)
                    : new Color(cols.DynamicComponentConnection.r, cols.DynamicComponentConnection.g * 1.2f,
                        cols.DynamicComponentConnection.b, 0.9f);

                // Use the new extension method with code reference
                invokerNode.ConnectNodesWithCodeReference(
                    targetNode, 
                    invocationColor, 
                    1,
                    invocation.IsIndirect ? "indirectEventInvocation" : "eventInvocation",
                    cols.MaxWidthHierarchy,
                    invocation.SourceFile,
                    invocation.LineNumber,
                    invocation.MethodPattern
                );
            }
        }


        private void BuildTypeNameMapping(IEnumerable<Type> monoBehaviourTypes)
        {
            foreach (var type in monoBehaviourTypes)
            {
                _typeNameToTypeMap[type.Name] = type;
                if (!string.IsNullOrEmpty(type.FullName))
                    _typeNameToTypeMap[type.FullName] = type;
            }

            // Add known types
            _typeNameToTypeMap["ScriptableObjectInventory"] =
                typeof(ScriptableObjectInventory.ScriptableObjectInventory);
        }

        private void AnalyzeEventPublishers(string sourceCode, Type publisherType)
        {
            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = syntaxTree.GetRoot();
                var events = new List<EventInfo>();

                // Look for field declarations that are events/actions
                var fieldDeclarations = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
                foreach (var field in fieldDeclarations)
                {
                    foreach (var variable in field.Declaration.Variables)
                    {
                        var fieldName = variable.Identifier.ValueText;
                        var typeName = field.Declaration.Type.ToString();

                        var eventType = DetermineEventType(typeName);
                        if (eventType == EventType.CustomDelegate && !IsKnownEventType(typeName)) continue;
                        var invokeMethodName = FindInvokeMethodForEvent(root, fieldName);

                        events.Add(new EventInfo
                        {
                            EventName = fieldName,
                            FieldName = fieldName,
                            Type = eventType,
                            HasInvokeMethod = HasInvokePattern(sourceCode, fieldName),
                            InvokeMethodName = invokeMethodName
                        });
                    }
                }

                if (events.Count > 0)
                {
                    _eventPublishers[publisherType] = events;
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Error analyzing event publishers for {publisherType.Name}: {e.Message}");
            }
        }

        private bool HasInvokePattern(string sourceCode, string eventName)
        {
            return sourceCode.Contains($"{eventName}?.Invoke(") ||
                   sourceCode.Contains($"{eventName}.Invoke(") ||
                   sourceCode.Contains($"Invoke{eventName}");
        }

        private string FindInvokeMethodForEvent(SyntaxNode root, string eventName)
        {
            // Look for methods that invoke this specific event
            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methodDeclarations)
            {
                var methodBody = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "";
                if (methodBody.Contains($"{eventName}?.Invoke") || methodBody.Contains($"{eventName}.Invoke"))
                {
                    return method.Identifier.ValueText;
                }
            }

            return null;
        }

        private List<EventSubscription> AnalyzeSourceCodeForEventSubscriptions(string sourceCode, Type subscriberType)
        {
            var results = new List<EventSubscription>();

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = syntaxTree.GetRoot();

                // Look for "+=" assignments
                var assignments = root.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Where(a => a.OperatorToken.IsKind(SyntaxKind.PlusEqualsToken));

                foreach (var assignment in assignments)
                {
                    var leftSide = assignment.Left.ToString();
                    AnalyzeEventSubscriptionPattern(leftSide, subscriberType, results);
                }

                // Look for conditional event subscriptions
                var ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>();
                foreach (var ifStatement in ifStatements)
                {
                    var condition = ifStatement.Condition.ToString();

                    var assignmentsInIf = ifStatement.DescendantNodes()
                        .OfType<AssignmentExpressionSyntax>()
                        .Where(a => a.OperatorToken.IsKind(SyntaxKind.PlusEqualsToken));

                    foreach (var assignment in assignmentsInIf)
                    {
                        var leftSide = assignment.Left.ToString();
                        AnalyzeEventSubscriptionPattern(leftSide, subscriberType, results, condition);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Error analyzing event subscriptions for {subscriberType.Name}: {e.Message}");
            }

            return results;
        }

        private void AnalyzeEventSubscriptionPattern(string leftSide, Type subscriberType,
            List<EventSubscription> results, string condition = null)
        {
            var parts = leftSide.Split('.');

            if (parts.Length == 2 && !leftSide.Contains("Instance"))
            {
                var eventName = parts[1];
                results.Add(new EventSubscription
                {
                    SubscriberType = subscriberType,
                    EventFieldName = eventName,
                    PublisherType = FindTypeForEventName(eventName),
                    AccessPattern = leftSide,
                    Type = SubscriptionType.DirectSubscription,
                    IntermediateField = null
                });
            }
            else if (leftSide.Contains("Instance."))
            {
                if (parts.Length < 3) return;
                var instanceIndex = Array.IndexOf(parts, "Instance");
                if (instanceIndex < 0 || instanceIndex >= parts.Length - 2) return;
                var fieldName = parts[instanceIndex + 1];
                var eventName = parts[^1];
                var publisherType = ResolveTypeFromAccessPath(leftSide);

                results.Add(new EventSubscription
                {
                    SubscriberType = subscriberType,
                    EventFieldName = eventName,
                    PublisherType = publisherType ?? FindTypeForInstanceField(fieldName),
                    AccessPattern = leftSide,
                    Type = condition != null ? SubscriptionType.ConditionalAccess : SubscriptionType.InstanceAccess,
                    IntermediateField = fieldName
                });
            }
        }

        private Type FindTypeForEventName(string eventName)
        {
            foreach (var kvp in _eventPublishers)
            {
                if (kvp.Value.Any(e => e.EventName == eventName))
                {
                    return kvp.Key;
                }
            }

            return null;
        }

        private Type FindTypeForInstanceField(string fieldName)
        {
            var fieldTypeMap = new Dictionary<string, Type>
            {
                { "clearEvent", _typeResolver.FindTypeByName("ClearEvent") },
                { "removePhysicsEvent", _typeResolver.FindTypeByName("RemovePhysicsEvent") },
                { "graph", _typeResolver.FindTypeByName("NodeGraphScriptableObject") }
            };

            return fieldTypeMap.GetValueOrDefault(fieldName);
        }

// ============================================================================
//  Roslyn-powered target-type resolution + richer access-path heuristics
//  (replace the existing helpers in EventAnalyzer.cs with the block below)
// ============================================================================

        #region === 1.  Roslyn helpers  ===============================================

// Cache one semantic model per syntax tree (enough for our single-file passes)
        private Tuple<SyntaxTree, SemanticModel> _semanticModelCache;

        private SemanticModel GetSemanticModel(SyntaxTree tree)
        {
            if (_semanticModelCache != null && _semanticModelCache.Item1 == tree)
                return _semanticModelCache.Item2;

            // Build a minimal compilation that contains just this tree
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location));

            var compilation = CSharpCompilation.Create(
                "TmpEventAnalysis",
                new[] { tree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var model = compilation.GetSemanticModel(tree, true);
            _semanticModelCache = Tuple.Create(tree, model);
            return model;
        }

// Convert a Roslyn ITypeSymbol into a System.Type via reflection
        private Type ConvertSymbolToType(ITypeSymbol symbol)
        {
            if (symbol == null) return null;

            string fullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty);

            // Try fully-qualified first, then short name
            return _typeResolver.FindTypeByName(fullName) ??
                   _typeResolver.FindTypeByName(symbol.Name);
        }

        #endregion


        #region === 2.  Access-path → Type heuristics  ================================

// Extra singleton/service keywords beyond the classic “Instance”
        private static readonly string[] _singletonKeywords =
            { "Instance", "Current", "Singleton", "Service", "Services" };

        private Type ResolveTypeFromAccessPath(string accessPath)
        {
            // Generic rule:  FooBar.Instance.…  |  FooBar.Current.…  etc.
            var parts = accessPath.Split('.');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!_singletonKeywords.Contains(parts[i + 1])) continue;

                var candidate = string.Join(".", parts.Take(i + 1)); // everything before “.Instance”
                var type = _typeResolver.FindTypeByName(candidate);
                if (type != null) return type;
            }

            // Static field / property pattern:  StaticType.SomeEvent
            if (parts.Length >= 2)
            {
                var candidate = string.Join(".", parts.Take(parts.Length - 1));
                var type = _typeResolver.FindTypeByName(candidate);
                if (type != null) return type;
            }

            // Service-locator pattern:  locator.Get<SomeType>()…
            int gStart = accessPath.IndexOf('<');
            int gEnd = accessPath.IndexOf('>');
            if (gStart >= 0 && gEnd > gStart)
            {
                var genericType = accessPath.Substring(gStart + 1, gEnd - gStart - 1);
                var type = _typeResolver.FindTypeByName(genericType);
                if (type != null) return type;
            }

            // Project-specific fall-backs you already had
            if (accessPath.Contains("graph"))
                return _typeResolver.FindTypeByName("NodeGraphScriptableObject");
            if (accessPath.Contains("clearEvent"))
                return _typeResolver.FindTypeByName("ClearEvent");

            return null; // **never** fall back to typeof(object)
        }

        #endregion


        #region === 3.  Indirect invocation scanner (uses semantic model) =============

        private void AnalyzeIndirectInvocations(SyntaxNode root, Type invokerType, List<EventInvocation> invocations)
        {
            var memberAccessExpressions = root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>();
            var semantic = GetSemanticModel(root.SyntaxTree);

            foreach (var memberAccess in memberAccessExpressions)
            {
                string accessPath = memberAccess.ToString();
                if (!accessPath.Contains(".")) continue; // nothing interesting

                // 1) First choice: Roslyn semantic model
                var targetType = ResolveTargetType(memberAccess, semantic);

                // 2) Fallback: string heuristics
                targetType ??= ResolveTypeFromAccessPath(accessPath);
                if (targetType == null) continue; // still unknown → skip, no “Object” node

                var parentInvocation = memberAccess.Ancestors()
                    .OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault();

                invocations.Add(new EventInvocation
                {
                    InvokerType = invokerType,
                    EventName = ExtractEventNameFromPath(accessPath),
                    MethodPattern = parentInvocation?.ToString() ?? accessPath,
                    TargetType = targetType,
                    LineNumber = root.SyntaxTree.GetLineSpan(memberAccess.Span)
                        .StartLinePosition.Line + 1,
                    SourceFile = FindSourceFileForType(invokerType),
                    IsIndirect = true,
                    AccessPath = accessPath
                });
            }
        }

        // Use Roslyn to learn the runtime type of the expression left of “.Invoke” / “.Event”
        private Type ResolveTargetType(MemberAccessExpressionSyntax memberAccess, SemanticModel semantic)
        {
            var exprSymbol = semantic.GetSymbolInfo(memberAccess.Expression).Symbol;

            return exprSymbol switch
            {
                IPropertySymbol propSym => ConvertSymbolToType(propSym.Type),
                IFieldSymbol fieldSym => ConvertSymbolToType(fieldSym.Type),
                IMethodSymbol methodSym => ConvertSymbolToType(methodSym.ReturnType),
                _ => null
            };
        }

        #endregion
    }
}