﻿// EventAnalyzer.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _3DConnections.Runtime.Managers;
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
        
        private readonly Dictionary<Type, List<EventInfo>> _eventPublishers = new();
        private readonly List<EventInvocation> _eventInvocations = new();
        private readonly List<EventSubscription> _eventSubscriptions = new();
        private readonly Dictionary<string, Type> _typeNameToTypeMap = new();
        private readonly Dictionary<Type, List<EventInfo>> EventPublishersEnhanced = new();

        public EventAnalyzer(IFileLocator fileLocator, ITypeResolver typeResolver, ILogger logger)
        {
            _fileLocator = fileLocator;
            _typeResolver = typeResolver;
            _logger = logger;
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
                var invocationExpressionSyntaxes = invocationExpressions as InvocationExpressionSyntax[] ?? invocationExpressions.ToArray();
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
        
        private void AnalyzeIndirectInvocations(SyntaxNode root, Type invokerType, List<EventInvocation> invocations)
        {
            // Look for patterns like: ScriptableObjectInventory.Instance.graph.InvokeOnAllCountChanged()
            var memberAccessExpressions = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();

            invocations.AddRange(from memberAccess in memberAccessExpressions
            let accessPath = memberAccess.ToString()
            where accessPath.Contains("Instance.") && (accessPath.Contains("Event") || accessPath.Contains("event") || accessPath.Contains("graph") || accessPath.Contains("clearEvent"))
            let parentInvocation = memberAccess.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault()
            where parentInvocation != null
            let lineNumber = root.SyntaxTree.GetLineSpan(parentInvocation.Span).StartLinePosition.Line + 1
            let targetType = ResolveTypeFromAccessPath(accessPath)
            select new EventInvocation
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
            var behaviourTypes = monoBehaviourTypes as Type[] ?? monoBehaviourTypes.ToArray();
            BuildTypeNameMapping(behaviourTypes);

            // First pass: Find all event publishers
            foreach (var monoBehaviourType in behaviourTypes)
            {
                try
                {
                    var sourceFile = _fileLocator.FindSourceFileForType(monoBehaviourType);
                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    var sourceCode = File.ReadAllText(sourceFile);
                    AnalyzeEventPublishers(sourceCode, monoBehaviourType);
                }
                catch (Exception e)
                {
                    _logger.LogWarning($"Could not analyze event publishers in {monoBehaviourType.Name}: {e.Message}");
                }
            }

            // Second pass: Find all event subscriptions and invocations
            foreach (var monoBehaviourType in behaviourTypes)
            {
                try
                {
                    var sourceFile = _fileLocator.FindSourceFileForType(monoBehaviourType);
                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    var sourceCode = File.ReadAllText(sourceFile);
                    var subscriptions = AnalyzeSourceCodeForEventSubscriptions(sourceCode, monoBehaviourType);
                    _eventSubscriptions.AddRange(subscriptions);

                    var invocations = AnalyzeSourceCodeForEventInvocations(sourceCode, monoBehaviourType);
                    _eventInvocations.AddRange(invocations);
                }
                catch (Exception e)
                {
                    _logger.LogWarning($"Could not analyze events in {monoBehaviourType.Name}: {e.Message}");
                }
            }

            _logger.Log($"Found {_eventPublishers.Count} event publishers, {_eventInvocations.Count} invocations, and {_eventSubscriptions.Count} subscriptions");
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
            if (typeName.Contains("EventHandler")) return EventType.SystemEventHandler; // Add this
            if (typeName.Contains("Func")) return EventType.FuncDelegate; // Add this
            return EventType.CustomDelegate;
        }

        public void CreateEventConnections(INodeGraphManager nodeManager)
        {
            // Create connections for event subscriptions
            foreach (var subscription in _eventSubscriptions)
            {
                var subscriberNode = nodeManager.FindNodeByType(subscription.SubscriberType);
                if (subscriberNode == null) continue;

                var publisherNode = nodeManager.FindNodeByType(subscription.PublisherType);
                if (publisherNode == null)
                {
                    nodeManager.CreateNode(null, 1, subscriberNode, false, subscription.PublisherType);
                }
                else
                {
                    var connectionColor = subscription.Type switch
                    {
                        SubscriptionType.DirectSubscription => cols.UnityEventConnection,
                        SubscriptionType.InstanceAccess => new Color(cols.UnityEventConnection.r * 0.8f,
                            cols.UnityEventConnection.g * 0.8f, cols.UnityEventConnection.b, 0.7f),
                        SubscriptionType.ConditionalAccess => new Color(cols.UnityEventConnection.r,
                            cols.UnityEventConnection.g * 0.6f, cols.UnityEventConnection.b * 0.6f, 0.8f),
                        _ => cols.UnityEventConnection
                    };

                    subscriberNode.ConnectNodes(publisherNode, connectionColor, 1,
                            $"eventSubscription_{subscription.Type}_{subscription.IntermediateField ?? "direct"}",
                            cols.MaxWidthHierarchy);
                }
            }

            // Create connections for event invocations
            foreach (var invocation in _eventInvocations)
            {
                var invokerNode = nodeManager.FindNodeByType(invocation.InvokerType);
                if (invokerNode == null) continue;

                var targetNode = nodeManager.FindNodeByType(invocation.TargetType);
                if (targetNode == null)
                {
                    targetNode = nodeManager.CreateNode(null, 1, invokerNode, false, invocation.TargetType);
                }

                if (targetNode == null) continue;

                var invocationColor = invocation.IsIndirect
                    ? new Color(cols.DynamicComponentConnection.r * 1.2f, cols.DynamicComponentConnection.g * 0.8f,
                        cols.DynamicComponentConnection.b, 0.9f)
                    : new Color(cols.DynamicComponentConnection.r, cols.DynamicComponentConnection.g * 1.2f,
                        cols.DynamicComponentConnection.b, 0.9f);

                invokerNode.ConnectNodes(targetNode, invocationColor, 1,
                        invocation.IsIndirect ? "indirectEventInvocation" : "eventInvocation",
                        cols.MaxWidthHierarchy);

                _logger.Log($"Connected event invocation: {invocation.MethodPattern} from {invocation.InvokerType.Name} to {invocation.TargetType.Name}");
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
            _typeNameToTypeMap["ScriptableObjectInventory"] = typeof(ScriptableObjectInventory.ScriptableObjectInventory);
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

        private Type ResolveTypeFromAccessPath(string accessPath)
        {
            if (!accessPath.Contains("ScriptableObjectInventory.Instance")) return null;
            if (accessPath.Contains("graph"))
                return _typeResolver.FindTypeByName("NodeGraphScriptableObject");
            if (accessPath.Contains("clearEvent"))
                return _typeResolver.FindTypeByName("ClearEvent");
            return _typeResolver.FindTypeByName("ScriptableObjectInventory");

        }
    }
}
