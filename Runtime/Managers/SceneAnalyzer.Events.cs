namespace _3DConnections.Runtime.Managers
{
    using ScriptableObjectInventory;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using UnityEngine;
    using Events;
    using ScriptableObjects;

    public partial class SceneAnalyzer
    {
        // Enhanced event tracking structures
        private readonly Dictionary<Type, List<EventInfo>> _eventPublishersEnhanced = new();
        private readonly List<EventInvocation> _eventInvocations = new();
        private readonly List<EventSubscriptionEnhanced> _eventSubscriptionsEnhanced = new();
        private readonly Dictionary<string, Type> _typeNameToTypeMap = new();

        private struct EventInfo
        {
            public string EventName;
            public string FieldName;
            public EventType Type;
            public bool HasInvokeMethod;
            public string InvokeMethodName; // Store the method that invokes this event
        }

        private struct EventInvocation
        {
            public Type InvokerType;
            public string EventName;
            public string MethodPattern; // The pattern used to invoke (e.g., "?.Invoke()", ".OnEventTriggered += ")
            public Type TargetType; // The type that owns the event
            public int LineNumber;
            public string SourceFile;
            public bool IsIndirect; // True if accessing through another object
            public string AccessPath; // Full access path like "ScriptableObjectInventory.Instance.graph"
        }

        private struct EventSubscriptionEnhanced
        {
            public Type SubscriberType;
            public string EventFieldName;
            public Type PublisherType;
            public string AccessPattern; // How the event is accessed (e.g., "Instance.clearEvent.OnEventTriggered")
            public SubscriptionType Type;
            public string IntermediateField; // The field used to access the event (e.g., "clearEvent")
        }

        private enum EventType
        {
            UnityAction,
            Action,
            UnityEvent,
            CustomDelegate
        }

        private enum SubscriptionType
        {
            DirectSubscription, // obj.Event += handler
            InstanceAccess, // Instance.field.Event += handler
            ConditionalAccess, // if (Instance.field) Instance.field.Event += handler
            MethodInvocation // obj.InvokeMethod() that internally calls event
        }

        private List<(string fieldName, Type fieldType)> FindDelegateFields(object obj)
        {
            var events = new List<(string, Type)>();
            if (obj == null) return events;
            var fields = obj.GetType().GetFields(System.Reflection.BindingFlags.Instance |
                                                 System.Reflection.BindingFlags.Public |
                                                 System.Reflection.BindingFlags.NonPublic);
            events.AddRange(from field in fields
                where typeof(Delegate).IsAssignableFrom(field.FieldType)
                select (field.Name, field.FieldType));
            return events;
        }

        private void AnalyzeEventSubscriptions()
        {
            // Build type name to type mapping for better resolution
            BuildTypeNameMapping();

            // First pass: Find all event publishers (including enhanced detection)
            foreach (var monoBehaviourType in _discoveredMonoBehaviours)
            {
                try
                {
                    var sourceFile = FindSourceFileForType(monoBehaviourType);
                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    var sourceCode = File.ReadAllText(sourceFile);
                    AnalyzeEventPublishers(sourceCode, monoBehaviourType);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not analyze event publishers in {monoBehaviourType.Name}: {e.Message}");
                }
            }

            // Second pass: Find all event subscriptions and invocations
            foreach (var monoBehaviourType in _discoveredMonoBehaviours)
            {
                try
                {
                    var sourceFile = FindSourceFileForType(monoBehaviourType);
                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    var sourceCode = File.ReadAllText(sourceFile);
                    var subs = AnalyzeSourceCodeForEventSubscriptions(sourceCode, monoBehaviourType);
                    _eventSubscriptionsEnhanced.AddRange(subs);

                    var invocations = AnalyzeSourceCodeForEventInvocations(sourceCode, monoBehaviourType);
                    _eventInvocations.AddRange(invocations);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not analyze events in {monoBehaviourType.Name}: {e.Message}");
                }
            }

            // Also analyze ScriptableObjects for events
            AnalyzeScriptableObjectEvents();

            // Log summary
            Debug.Log($"Found {_eventPublishersEnhanced.Count} event publishers, {_eventInvocations.Count} invocations, and {_eventSubscriptionsEnhanced.Count} subscriptions");
        }

        private void BuildTypeNameMapping()
        {
            foreach (var type in _discoveredMonoBehaviours)
            {
                _typeNameToTypeMap[type.Name] = type;
                _typeNameToTypeMap[type.FullName] = type;
            }

            // Add known types
            _typeNameToTypeMap["ScriptableObjectInventory"] = typeof(ScriptableObjectInventory);
            _typeNameToTypeMap["NodeGraphScriptableObject"] = typeof(NodeGraphScriptableObject);
            
            // Add all loaded types that might be relevant
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsSubclassOf(typeof(MonoBehaviour)) || type.IsSubclassOf(typeof(ScriptableObject)))
                        {
                            _typeNameToTypeMap[type.Name] = type;
                            if (!string.IsNullOrEmpty(type.FullName))
                                _typeNameToTypeMap[type.FullName] = type;
                        }
                    }
                }
                catch { /* Ignore assembly loading errors */ }
            }
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

                        EventType eventType = DetermineEventType(typeName);
                        if (eventType != EventType.CustomDelegate || IsKnownEventType(typeName))
                        {
                            // Look for invoke methods for this event
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
                }

                // Look for event declarations
                var eventDeclarations = root.DescendantNodes().OfType<EventDeclarationSyntax>();
                foreach (var eventDecl in eventDeclarations)
                {
                    var eventName = eventDecl.Identifier.ValueText;
                    var typeName = eventDecl.Type.ToString();
                    var invokeMethodName = FindInvokeMethodForEvent(root, eventName);

                    events.Add(new EventInfo
                    {
                        EventName = eventName,
                        FieldName = eventName,
                        Type = DetermineEventType(typeName),
                        HasInvokeMethod = HasInvokePattern(sourceCode, eventName),
                        InvokeMethodName = invokeMethodName
                    });
                }

                if (events.Count > 0)
                {
                    _eventPublishersEnhanced[publisherType] = events;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error analyzing event publishers for {publisherType.Name}: {e.Message}");
            }
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

        private List<EventInvocation> AnalyzeSourceCodeForEventInvocations(string sourceCode, Type invokerType)
        {
            var invocations = new List<EventInvocation>();

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = syntaxTree.GetRoot();

                // Look for direct Invoke() calls
                var invocationExpressions = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocationExpressions)
                {
                    var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
                    if (memberAccess?.Name.Identifier.ValueText == "Invoke")
                    {
                        var fullExpression = memberAccess.Expression.ToString();
                        var lineNumber = syntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;

                        // Try to match this invoke to known events
                        var matchedEvent = FindMatchingEventForInvocation(fullExpression);
                        if (matchedEvent.HasValue)
                        {
                            invocations.Add(new EventInvocation
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
                        }
                    }
                }

                // Look for method invocations that might trigger events (like InvokeOnAllCountChanged)
                foreach (var invocation in invocationExpressions)
                {
                    if (invocation.Expression is MemberAccessExpressionSyntax memberAccess2)
                    {
                        var methodName = memberAccess2.Name.Identifier.ValueText;
                        
                        // Check if this is an invoke method pattern
                        if (methodName.StartsWith("Invoke") && methodName.Length > 6)
                        {
                            var lineNumber = syntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                            var fullExpression = memberAccess2.ToString();
                            var targetType = ResolveTypeFromExpression(memberAccess2.Expression);

                            // Extract the event name from the method name
                            var potentialEventName = methodName.Substring(6); // Remove "Invoke" prefix
                            
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
                    }
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

        private void AnalyzeIndirectInvocations(SyntaxNode root, Type invokerType, List<EventInvocation> invocations)
        {
            // Look for patterns like: ScriptableObjectInventory.Instance.graph.InvokeOnAllCountChanged()
            var memberAccessExpressions = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            
            foreach (var memberAccess in memberAccessExpressions)
            {
                // Check if this is accessing a field/property that might have events
                var accessPath = memberAccess.ToString();
                
                // Look for patterns that suggest event access
                if (accessPath.Contains("Instance.") && 
                    (accessPath.Contains("Event") || accessPath.Contains("event") || 
                     accessPath.Contains("graph") || accessPath.Contains("clearEvent")))
                {
                    // Find the parent invocation if any
                    var parentInvocation = memberAccess.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                    if (parentInvocation != null)
                    {
                        var lineNumber = root.SyntaxTree.GetLineSpan(parentInvocation.Span).StartLinePosition.Line + 1;
                        var targetType = ResolveTypeFromAccessPath(accessPath);
                        
                        invocations.Add(new EventInvocation
                        {
                            InvokerType = invokerType,
                            EventName = ExtractEventNameFromPath(accessPath),
                            MethodPattern = parentInvocation.ToString(),
                            TargetType = targetType ?? typeof(object),
                            LineNumber = lineNumber,
                            SourceFile = FindSourceFileForType(invokerType),
                            IsIndirect = true,
                            AccessPath = accessPath
                        });
                    }
                }
            }
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

        private Type ResolveTypeFromAccessPath(string accessPath)
        {
            // Try to resolve types from access paths like "ScriptableObjectInventory.Instance.graph"
            var parts = accessPath.Split('.');
            
            // Look for known type patterns
            if (accessPath.Contains("ScriptableObjectInventory.Instance"))
            {
                if (accessPath.Contains("graph"))
                    return typeof(NodeGraphScriptableObject);
                if (accessPath.Contains("clearEvent"))
                    return typeof(ClearEvent);
                return typeof(ScriptableObjectInventory);
            }
            
            // Try to match the first part to a known type
            if (parts.Length > 0 && _typeNameToTypeMap.TryGetValue(parts[0], out var type))
            {
                return type;
            }
            
            return null;
        }

        private string ExtractEventNameFromPath(string accessPath)
        {
            var parts = accessPath.Split('.');
            return parts.Length > 0 ? parts[parts.Length - 1] : "UnknownEvent";
        }

        private List<EventSubscriptionEnhanced> AnalyzeSourceCodeForEventSubscriptions(string sourceCode,
            Type subscriberType)
        {
            var results = new List<EventSubscriptionEnhanced>();

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = syntaxTree.GetRoot();

                // Look for "+=" assignments (existing logic enhanced)
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
                    // Look for patterns like: if (Instance.clearEvent) Instance.clearEvent.OnEventTriggered += handler
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

                // Look for "-=" unsubscriptions as well
                var unsubscriptions = root.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Where(a => a.OperatorToken.IsKind(SyntaxKind.MinusEqualsToken));

                foreach (var assignment in unsubscriptions)
                {
                    var leftSide = assignment.Left.ToString();
                    AnalyzeEventSubscriptionPattern(leftSide, subscriberType, results);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error analyzing event subscriptions for {subscriberType.Name}: {e.Message}");
            }

            return results;
        }

        private void AnalyzeEventSubscriptionPattern(string leftSide, Type subscriberType,
            List<EventSubscriptionEnhanced> results, string condition = null)
        {
            var parts = leftSide.Split('.');
            
            // Pattern 1: Direct subscription (object.Event += handler)
            if (parts.Length == 2 && !leftSide.Contains("Instance"))
            {
                var eventName = parts[1];

                results.Add(new EventSubscriptionEnhanced
                {
                    SubscriberType = subscriberType,
                    EventFieldName = eventName,
                    PublisherType = FindTypeForEventName(eventName),
                    AccessPattern = leftSide,
                    Type = SubscriptionType.DirectSubscription,
                    IntermediateField = null
                });
            }
            // Pattern 2: Instance access (Instance.field.Event += handler)
            else if (leftSide.Contains("Instance."))
            {
                if (parts.Length >= 3) // Instance.field.Event or longer paths
                {
                    var instanceIndex = Array.IndexOf(parts, "Instance");
                    if (instanceIndex >= 0 && instanceIndex < parts.Length - 2)
                    {
                        var fieldName = parts[instanceIndex + 1];
                        var eventName = parts[parts.Length - 1];
                        var publisherType = ResolveTypeFromAccessPath(leftSide);

                        results.Add(new EventSubscriptionEnhanced
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
            }
            // Pattern 3: Complex access patterns
            else if (parts.Length > 2)
            {
                var eventName = parts[parts.Length - 1];
                var publisherType = ResolveTypeFromAccessPath(leftSide);
                
                results.Add(new EventSubscriptionEnhanced
                {
                    SubscriberType = subscriberType,
                    EventFieldName = eventName,
                    PublisherType = publisherType,
                    AccessPattern = leftSide,
                    Type = SubscriptionType.InstanceAccess,
                    IntermediateField = parts.Length > 2 ? parts[parts.Length - 2] : null
                });
            }
        }

        private void AnalyzeScriptableObjectEvents()
        {
            // Find all ScriptableObject types in the project and analyze them for events
            var scriptableObjects = FindObjectsOfType<ScriptableObject>();
            foreach (var so in scriptableObjects)
            {
                if (so == null) continue;

                var soType = so.GetType();
                if (_discoveredMonoBehaviours.Contains(soType)) continue; // Already analyzed

                try
                {
                    var sourceFile = FindSourceFileForType(soType);
                    if (!string.IsNullOrEmpty(sourceFile))
                    {
                        var sourceCode = File.ReadAllText(sourceFile);
                        AnalyzeEventPublishers(sourceCode, soType);
                        
                        // Also analyze for invocations in ScriptableObjects
                        var invocations = AnalyzeSourceCodeForEventInvocations(sourceCode, soType);
                        _eventInvocations.AddRange(invocations);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not analyze ScriptableObject events in {soType.Name}: {e.Message}");
                }
            }
        }

        private EventType DetermineEventType(string typeName)
        {
            if (typeName.Contains("UnityAction")) return EventType.UnityAction;
            if (typeName.Contains("Action")) return EventType.Action;
            if (typeName.Contains("UnityEvent")) return EventType.UnityEvent;
            return EventType.CustomDelegate;
        }

        private bool IsKnownEventType(string typeName)
        {
            return typeName.Contains("Event") || typeName.Contains("Action") ||
                   typeName.Contains("Delegate") || typeName.Contains("Func");
        }

        private bool HasInvokePattern(string sourceCode, string eventName)
        {
            return sourceCode.Contains($"{eventName}?.Invoke(") ||
                   sourceCode.Contains($"{eventName}.Invoke(") ||
                   sourceCode.Contains($"Invoke{eventName}");
        }

        private (string eventName, Type targetType)? FindMatchingEventForInvocation(string expression)
        {
            // Try to match expressions like "OnGoCountChanged" to known events
            foreach (var kvp in _eventPublishersEnhanced)
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

        private Type FindTypeForEventName(string eventName)
        {
            foreach (var kvp in _eventPublishersEnhanced)
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
            // Map field names to their types based on known patterns
            var fieldTypeMap = new Dictionary<string, Type>
            {
                { "clearEvent", typeof(ClearEvent) },
                { "removePhysicsEvent", typeof(RemovePhysicsEvent) },
                { "toggleOverlayEvent", typeof(ToggleOverlayEvent) },
                { "updateLOD", typeof(UpdateLOD) },
                { "graph", typeof(NodeGraphScriptableObject) },
                { "conSo", typeof(NodeConnectionsScriptableObject) },
                { "nodeColors", typeof(NodeColorsScriptableObject) },
                { "overlay", typeof(OverlaySceneScriptableObject) },
                { "applicationState", typeof(ApplicationState) },
                { "menuState", typeof(MenuState) },
                { "layout", typeof(LayoutParameters) },
                { "simConfig", typeof(PhysicsSimulationConfiguration) }
            };

            if (fieldTypeMap.TryGetValue(fieldName, out var type))
            {
                return type;
            }

            // Try to find the type by searching through known types
            foreach (var kvp in _typeNameToTypeMap)
            {
                if (kvp.Key.ToLower().Contains(fieldName.ToLower()))
                {
                    return kvp.Value;
                }
            }

            return typeof(ScriptableObjectInventory); // Default fallback
        }

        private void CreateEventConnections()
        {
            // Create connections for enhanced event subscriptions
            foreach (var sub in _eventSubscriptionsEnhanced)
            {
                GameObject subscriberNode = FindNodeByComponentType(sub.SubscriberType);
                if (subscriberNode == null) continue;

                int eventDepth = 1;
                GameObject publisherNode = GetOrSpawnNode(null, eventDepth, subscriberNode, false, sub.PublisherType);
                if (publisherNode == null) continue;

                // Use different colors based on subscription type
                Color connectionColor = sub.Type switch
                {
                    SubscriptionType.DirectSubscription => unityEventConnection,
                    SubscriptionType.InstanceAccess => new Color(unityEventConnection.r * 0.8f,
                        unityEventConnection.g * 0.8f, unityEventConnection.b, 0.7f),
                    SubscriptionType.ConditionalAccess => new Color(unityEventConnection.r,
                        unityEventConnection.g * 0.6f, unityEventConnection.b * 0.6f, 0.8f),
                    _ => unityEventConnection
                };

                subscriberNode.ConnectNodes(publisherNode,
                    connectionColor,
                    eventDepth,
                    $"eventSubscription_{sub.Type}_{sub.IntermediateField ?? "direct"}",
                    ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);

                Debug.Log($"Connected event subscription: {sub.AccessPattern} -> {sub.SubscriberType.Name} (Type: {sub.Type}, Field: {sub.IntermediateField})");
            }

            // Create connections for event invocations
            foreach (var invocation in _eventInvocations)
            {
                GameObject invokerNode = FindNodeByComponentType(invocation.InvokerType);
                if (invokerNode == null) continue;

                GameObject targetNode = FindNodeByComponentType(invocation.TargetType);
                if (targetNode == null)
                {
                    targetNode = GetOrSpawnNode(null, 1, invokerNode, false, invocation.TargetType);
                }

                if (targetNode == null) continue;

                // Use a distinct color for invocations, with variation for indirect access
                Color invocationColor = invocation.IsIndirect
                    ? new Color(dynamicComponentConnection.r * 1.2f, dynamicComponentConnection.g * 0.8f,
                        dynamicComponentConnection.b, 0.9f)
                    : new Color(dynamicComponentConnection.r, dynamicComponentConnection.g * 1.2f,
                        dynamicComponentConnection.b, 0.9f);

                invokerNode.ConnectNodes(targetNode,
                    invocationColor,
                    1,
                    invocation.IsIndirect ? "indirectEventInvocation" : "eventInvocation",
                    ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);

                Debug.Log(
                    $"Connected event invocation: {invocation.MethodPattern} from {invocation.InvokerType.Name} to {invocation.TargetType.Name} (Indirect: {invocation.IsIndirect}, Path: {invocation.AccessPath})");
            }
        }
    }
}
