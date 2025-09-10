using System;
using System.Collections.Generic;
using System.Linq;
using _3DConnections.Runtime.Managers;
using UnityEngine;
using TMPro;
using _3DConnections.Runtime.Nodes;
using _3DConnections.Runtime.Nodes.Extensions;
using cols = _3DConnections.Runtime.ScriptableObjects.NodeColorsScriptableObject;
using soi = _3DConnections.Runtime.ScriptableObjectInventory.ScriptableObjectInventory;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _3DConnections.Runtime.Analysis
{
    public class NodeGraphManager : INodeGraphManager
    {
        private readonly Dictionary<int, GameObject> _instanceIdToNodeLookup = new();
        private readonly GameObject _nodePrefab;
        private readonly GameObject _parentNode;
        private readonly NodeSettings _settings;
        private readonly ILogger _logger;

        public IReadOnlyDictionary<int, GameObject> NodeLookup => _instanceIdToNodeLookup;

        public NodeGraphManager(GameObject nodePrefab, GameObject parentNode, NodeSettings settings, ILogger logger)
        {
            _nodePrefab = nodePrefab;
            _parentNode = parentNode;
            _settings = settings;
            _logger = logger;
        }

        public GameObject CreateNode(UnityEngine.Object obj, int depth, GameObject parent = null, bool isAsset = false, Type virtualType = null)
        {
            // Handle virtual component nodes
            if (virtualType != null)
            {
                return CreateVirtualNode(virtualType, depth, parent);
            }

            if (obj == null) return null;

            var instanceId = obj.GetInstanceID();

            // Check if node already exists
            if (_instanceIdToNodeLookup.TryGetValue(instanceId, out var existingNode))
            {
                if (existingNode != null)
                {
                    ConnectToParent(existingNode, parent, depth, isAsset, obj);
                    return existingNode;
                }
                _instanceIdToNodeLookup.Remove(instanceId);
            }

            var newNode = SpawnNode(obj, isAsset, virtualType);
            if (newNode != null && parent != null)
            {
                ConnectToParent(newNode, parent, depth, isAsset, obj);
            }

            return newNode;
        }

        public GameObject FindNodeByType(Type componentType)
        {
            return _instanceIdToNodeLookup.Values
                .Where(node => node != null)
                .FirstOrDefault(node =>
                {
                    var nodeType = node.GetComponent<NodeType>();
                    return nodeType?.reference is Component comp && comp.GetType() == componentType;
                });
        }

        public void ClearNodes()
        {
            _logger.Log($"Clearing {_parentNode.transform.childCount} nodes");
            
            foreach (Transform child in _parentNode.transform)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            _instanceIdToNodeLookup.Clear();
            
            if (soi.Instance.graph?.AllNodes != null)
            {
                soi.Instance.graph.AllNodes.Clear();
            }
        }

        private GameObject CreateVirtualNode(Type virtualType, int depth, GameObject parent)
        {
            // Check for existing real component node first
            var existingComponentNode = FindNodeByType(virtualType);
            if (existingComponentNode != null)
            {
                ConnectToParent(existingComponentNode, parent, depth, false, null);
                return existingComponentNode;
            }

            var fakeInstanceId = -virtualType.GetHashCode();

            // Check if virtual node already exists
            if (_instanceIdToNodeLookup.TryGetValue(fakeInstanceId, out var existingVirtualNode))
            {
                if (existingVirtualNode != null)
                {
                    ConnectToParent(existingVirtualNode, parent, depth, false, null);
                    return existingVirtualNode;
                }
                _instanceIdToNodeLookup.Remove(fakeInstanceId);
            }

            var virtualNode = SpawnNode(null, false, virtualType);
            if (virtualNode != null && parent != null)
            {
                ConnectToParent(virtualNode, parent, depth, false, null);
            }

            return virtualNode;
        }

        private void ConnectToParent(GameObject node, GameObject parent, int depth, bool isAsset, UnityEngine.Object obj)
        {
            if (parent == null) return;

            var connectionColor = GetConnectionColor(isAsset, obj);
            var connectionType = GetConnectionType(isAsset, obj);

            parent.ConnectNodes(node, connectionColor, depth, connectionType, cols.MaxWidthHierarchy);
        }

        private Color GetConnectionColor(bool isAsset, UnityEngine.Object obj)
        {
            if (isAsset)
                return new Color(cols.ReferenceConnection.r, cols.ReferenceConnection.g, cols.ReferenceConnection.b, 0.5f);

            return obj switch
            {
                GameObject => new Color(cols.ParentChildConnection.r, cols.ParentChildConnection.g, cols.ParentChildConnection.b, 0.5f),
                Component => new Color(cols.ComponentConnection.r, cols.ComponentConnection.g, cols.ComponentConnection.b, 0.5f),
                _ => new Color(cols.ReferenceConnection.r, cols.ReferenceConnection.g, cols.ReferenceConnection.b, 0.5f)
            };
        }

        private string GetConnectionType(bool isAsset, UnityEngine.Object obj)
        {
            if (isAsset) return "referenceConnection";

            return obj switch
            {
                GameObject => "parentChildConnection",
                Component => "componentConnection",
                _ => "referenceConnection"
            };
        }

        private GameObject SpawnNode(UnityEngine.Object obj, bool isAsset = false, Type virtualType = null)
        {
            var nodeObject = CreateNodeInstance();
            if (nodeObject == null) return null;

            ConfigureNodeType(nodeObject, obj, virtualType);
            AddTextChild(nodeObject, obj, virtualType);

#if UNITY_EDITOR
            if (virtualType == null && _settings.SetIcons)
                AddIcon(nodeObject, obj);
#endif

            ConfigureNodeName(nodeObject, obj, virtualType);
            ApplyNodeColor(nodeObject, obj, isAsset, virtualType);

            // Register the node
            if (virtualType != null)
            {
                var fakeInstanceId = -virtualType.GetHashCode();
                _instanceIdToNodeLookup[fakeInstanceId] = nodeObject;
            }
            else if (obj != null)
            {
                _instanceIdToNodeLookup[obj.GetInstanceID()] = nodeObject;
            }

            // Add to AllNodes collection
            if (soi.Instance.graph?.AllNodes != null)
            {
                soi.Instance.graph.AllNodes.Add(nodeObject);
            }

            return nodeObject;
        }

        private GameObject CreateNodeInstance()
        {
            if (!soi.Instance.overlay.GetCameraOfScene())
            {
                _logger.LogError("No camera while trying to spawn a node");
                return null;
            }

            var nodeObject = UnityEngine.Object.Instantiate(_nodePrefab, _parentNode.transform);
            nodeObject.transform.localPosition = Vector3.zero;
            nodeObject.transform.localScale = new Vector3(_settings.NodeWidth, _settings.NodeHeight, 1f);

            // Handle sprite renderer scaling
            var spriteRenderer = nodeObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer?.sprite != null)
            {
                var sprite = spriteRenderer.sprite;
                var spriteSize = sprite.rect.size / sprite.pixelsPerUnit;
                nodeObject.transform.localScale = new Vector3(
                    _settings.NodeWidth / spriteSize.x,
                    _settings.NodeHeight / spriteSize.y,
                    1f
                );
            }

            nodeObject.layer = LayerMask.NameToLayer("Nodes");
            return nodeObject;
        }

        private static void ConfigureNodeType(GameObject nodeObject, UnityEngine.Object obj, Type virtualType)
        {
            var nodeType = nodeObject.GetComponent<NodeType>();
            if (nodeType == null) return;

            if (virtualType != null)
            {
                nodeType.reference = null;
                nodeType.nodeTypeName = NodeTypeName.Component;
            }
            else
            {
                nodeType.SetNodeType(obj);
                nodeType.reference = obj;
            }
        }

        private static void AddTextChild(GameObject nodeObject, UnityEngine.Object obj, Type virtualType)
        {
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(nodeObject.transform);
            textObj.transform.localPosition = new Vector3(0, 0.12f, -0.3f);

            var text = textObj.AddComponent<TextMeshPro>();
            text.renderer.sortingOrder = 2;

            if (virtualType != null)
            {
                text.text = $"({virtualType.Name})";
            }
            else if (obj is Component component)
            {
                text.text = $"{component.gameObject.name}.{component.GetType().Name}";
            }
            else
            {
                text.text = obj ? obj.name : "null object";
            }

            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 1.5f;
        }

#if UNITY_EDITOR
        private void AddIcon(GameObject nodeObject, UnityEngine.Object obj)
        {
            if (!(obj is Component componentObject)) return;

            var componentIcon = EditorGUIUtility.ObjectContent(null, componentObject.GetType()).image as Texture2D;
            if (componentIcon == null) return;

            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(nodeObject.transform);
            iconObj.transform.localPosition = new Vector3(0, 0, -1f);

            var iconRenderer = iconObj.AddComponent<SpriteRenderer>();
            var sprite = Sprite.Create(componentIcon, new Rect(0, 0, componentIcon.width, componentIcon.height), new Vector2(0.5f, 0.5f), 100f);
            iconRenderer.sprite = sprite;
            iconRenderer.sortingOrder = 1;

            const float desiredHeight = 0.5f;
            var spriteSize = sprite.bounds.size;
            float scaleY = desiredHeight / spriteSize.y;
            float scaleX = scaleY * (spriteSize.x / spriteSize.y) * 0.5f;
            iconObj.transform.localScale = new Vector3(scaleX, scaleY, 1);
        }
#endif

        private void ConfigureNodeName(GameObject nodeObject, UnityEngine.Object obj, Type virtualType)
        {
            var nodeType = nodeObject.GetComponent<NodeType>();
            if (virtualType != null)
            {
                nodeObject.name = $"virtual_co_{virtualType.Name}";
                return;
            }

            if (nodeType == null) return;

            var prefix = nodeType.nodeTypeName switch
            {
                NodeTypeName.GameObject => "go_",
                NodeTypeName.Component => "co_",
                NodeTypeName.ScriptableObject => "so_",
                _ => ""
            };

            if (nodeType.reference == null)
            {
                nodeObject.name = "tfRoot";
                nodeType.nodeTypeName = NodeTypeName.GameObject;
            }
            else
            {
                string postfix = prefix != "go_" ? "_" + nodeType.reference.GetType().Name : string.Empty;
                nodeObject.name = $"{prefix}{obj.name}{postfix}";
            }
        }

        private static void ApplyNodeColor(GameObject nodeObject, UnityEngine.Object obj, bool isAsset, Type virtualType)
        {
            if (virtualType != null)
            {
                var dimmedColor = cols.DynamicComponentConnection;
                nodeObject.SetNodeColor(obj, cols.GameObjectColor, dimmedColor, cols.ScriptableObjectColor,
                    cols.AssetColor, overrideColor: dimmedColor);
            }
            else
            {
                nodeObject.SetNodeColor(obj, cols.GameObjectColor, cols.ComponentColor, cols.ScriptableObjectColor,
                    cols.AssetColor, isAsset);
            }
        }
    }

    [Serializable]
    public class NodeSettings
    {
        public int NodeWidth = 2;
        public int NodeHeight = 1;
        public bool SetIcons = false;
        public bool ScaleNodesUsingMaintainability = false;
    }
}
