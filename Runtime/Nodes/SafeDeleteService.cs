using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _3DConnections.Runtime.Managers;
using _3DConnections.Runtime.ScriptableObjects;
using soi = _3DConnections.Runtime.ScriptableObjectInventory.ScriptableObjectInventory;

namespace _3DConnections.Runtime.Nodes
{
    public class SafeDeleteService : MonoBehaviour
    {
        private static SafeDeleteService _instance;
        public static SafeDeleteService Instance => _instance;
        
        // Event hooks
        public static System.Action OnConnectionsChanged;
        
        private void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        
        private void OnEnable()
        {
            // Subscribe to events
            if (soi.Instance?.clearEvent != null)
                soi.Instance.clearEvent.onEventTriggered.AddListener(ClearAll);
            
            // Hook into connection changes
            OnConnectionsChanged += RecomputeAll;
            
            // Initial computation
            RecomputeAll();
        }
        
        private void OnDisable()
        {
            if (soi.Instance?.clearEvent != null)
                soi.Instance.clearEvent.onEventTriggered.RemoveListener(ClearAll);
            
            OnConnectionsChanged -= RecomputeAll;
        }

        public void RecomputeAll()
        {
            if (!soi.Instance?.graph || !soi.Instance?.conSo) return;
            
            var nodes = soi.Instance.graph.AllNodes;
            var connections = soi.Instance.conSo.connections;
            
            // Reset all counts
            foreach (var node in nodes.Where(n => n))
            {
                var state = node.GetComponent<NodeSafetyState>();
                if (!state) state = node.AddComponent<NodeSafetyState>();
                
                state.InboundReferenceCount = 0;
                state.InboundSubscriptionCount = 0;
            }

            var subscriptionConnectionTypes = new []{"getComponentCall, addComponentCall, Event, Action, Delegate, Invocation"};
            var referenceConnectionTypes = new []{"referenceConnection, parentChildConnection, referenceConnection"};
            
            // Count inbound connections by type
            foreach (var conn in connections.Where(c => c?.endNode && c.startNode))
            {
                var state = conn.endNode.GetComponent<NodeSafetyState>();
                if (!state) continue;
                
                // subscription
                if (subscriptionConnectionTypes.Contains(conn.connectionType))
                {
                    state.InboundSubscriptionCount++;
                }
                
                // reference
                else if (referenceConnectionTypes.Contains(conn.connectionType))
                {
                    state.InboundReferenceCount++;
                }
                else
                {
                    Debug.Log("<b><color=cyan>Wrong connection type detected during recomputation of SafeDeletion state for :</color></b>", gameObject);
                }
            }
            
            // Update visuals
            foreach (var node in nodes.Where(n => n))
            {
                var state = node.GetComponent<NodeSafetyState>();
                state?.UpdateVisual();
            }
            
            UpdateClusterBadges();
        }
        
        private void UpdateClusterBadges()
        {
            var lodManager = FindFirstObjectByType<GraphLODManager>();
            if (!lodManager || !lodManager.enabled) return;
            
            // This could aggregate risk states to cluster hulls
        }
        
        public void HighlightInboundSources(GameObject targetNode)
        {
            if (!targetNode) return;
            
            var connections = soi.Instance.conSo.connections;
            var sources = connections
                .Where(c => c?.endNode == targetNode && c?.startNode != null)
                .Select(c => c.startNode)
                .Distinct()
                .ToList();
            
            // Highlight sources
            foreach (var source in sources)
            {
                var colored = source.GetComponent<ColoredObject>();
                if (colored)
                {
                    colored.Highlight(Color.yellow, duration: 2f, highlightForever: true);
                }
            }
            
            // Highlight target differently
            var targetColored = targetNode.GetComponent<ColoredObject>();
            if (targetColored)
            {
                targetColored.Highlight(Color.cyan, duration: 2f, highlightForever: true);
            }
        }
        
        private void ClearAll()
        {
            var nodes = soi.Instance?.graph?.AllNodes;
            if (nodes == null) return;
            
            foreach (var node in nodes.Where(n => n != null))
            {
                var state = node.GetComponent<NodeSafetyState>();
                if (!state) continue;
                state.InboundReferenceCount = 0;
                state.InboundSubscriptionCount = 0;
                state.UpdateVisual();
            }
        }
        
        public List<(GameObject source, string info)> GetInboundSubscribers(GameObject node)
        {
            var result = new List<(GameObject, string)>();
            var connections = soi.Instance.conSo.connections
                .Where(c => c?.endNode == node && 
                           (c.connectionType?.Contains("event") == true || 
                            c.connectionType == "eventSubscription"));
            
            foreach (var conn in connections)
            {
                var info = "";
                if (conn.codeReference is { HasReference: true })
                {
                    info = $"{conn.codeReference.className}.{conn.codeReference.methodName}";
                }
                result.Add((conn.startNode, info));
            }
            
            return result;
        }
    }
}
