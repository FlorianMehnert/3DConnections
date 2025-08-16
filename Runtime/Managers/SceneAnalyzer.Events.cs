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

    public partial class SceneAnalyzer : MonoBehaviour
    {
        private void AnalyzeEventSubscriptions()
        {
            foreach (var monoBehaviourType in _discoveredMonoBehaviours)
            {
                try
                {
                    var sourceFile = FindSourceFileForType(monoBehaviourType);
                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    var sourceCode = File.ReadAllText(sourceFile);
                    var subs = AnalyzeSourceCodeForEventSubscriptions(sourceCode, monoBehaviourType);
                    _eventSubscriptions.AddRange(subs);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not analyze event subscriptions in {monoBehaviourType.Name}: {e.Message}");
                }
            }
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

                    // Match "something.EventName"
                    if (!leftSide.Contains(".")) continue;
                    var fieldName = leftSide.Split('.').Last();

                    // Check if this matches any known publisher field
                    results.AddRange(from kvp in _eventPublishers
                        let publisherType = kvp.Key
                        where kvp.Value.Contains(fieldName)
                        select new EventSubscription
                        {
                            SubscriberType = subscriberType, EventFieldName = fieldName, PublisherType = publisherType
                        });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error analyzing event subscriptions for {subscriberType.Name}: {e.Message}");
            }

            return results;
        }

        private List<(string fieldName, Type fieldType)> FindDelegateFields(object obj)
        {
            var events = new List<(string, Type)>();
            if (obj == null) return events;

            var fields = obj.GetType().GetFields(System.Reflection.BindingFlags.Instance |
                                                 System.Reflection.BindingFlags.Public |
                                                 System.Reflection.BindingFlags.NonPublic);

            events.AddRange(from field in fields where typeof(Delegate).IsAssignableFrom(field.FieldType) select (field.Name, field.FieldType));

            return events;
        }

        private void CreateEventConnections()
        {
            foreach (var sub in _eventSubscriptions)
            {
                // Find the subscriber node first
                GameObject subscriberNode = FindNodeByComponentType(sub.SubscriberType);
                if (subscriberNode == null) continue;

                // Use GetOrSpawnNode to find or create a publisher node with proper depth and parent tracking
                int eventDepth = 1; // Event connections are typically one level deep
        
                // Create/find the publisher node connected to the subscriber (reverse direction for event flow)
                GameObject publisherNode = GetOrSpawnNode(null, eventDepth, subscriberNode, false, sub.PublisherType);
                if (publisherNode == null) continue;

                // The connection was already made in GetOrSpawnNode, but we can override with event-specific styling
                subscriberNode.ConnectNodes(publisherNode,
                    unityEventConnection,
                    eventDepth,
                    "eventSubscriptionConnection",
                    ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);

                Debug.Log(
                    $"Connected event publisher {sub.PublisherType.Name}.{sub.EventFieldName} -> subscriber {sub.SubscriberType.Name}");
            }
        }
    }
}