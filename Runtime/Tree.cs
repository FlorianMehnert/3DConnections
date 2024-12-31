using UnityEngine;
using System.Collections.Generic;
using System;
using _3DConnections.Runtime.Managers;
using _3DConnections.Runtime.ScriptableObjects;
using Runtime;
using Object = UnityEngine.Object;

namespace _3DConnections.Runtime
{
    [Serializable]
    public class Tree
    {
        public class TreeNode
        {
            public Node Data { get; private set; }
            public List<TreeNode> Children { get; private set; }
            public TreeNode Parent { get; private set; }
            public GameObject GameObjectReference { get; set; }

            public TreeNode(Node data, GameObject gameObject = null)
            {
                Data = data;
                Children = new List<TreeNode>();
                GameObjectReference = gameObject;
            }

            public void AddChild(TreeNode child)
            {
                child.Parent = this;
                Children.Add(child);
            }
        }

        private TreeNode virtualRoot;
        private NodeConnectionManager _connectionManager;
        private List<TreeNode> actualRoots;

        public Tree(string sceneName, NodeConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
            actualRoots = new List<TreeNode>();

            // Create a virtual root node
            Node virtualRootData = new GameObjectNode("VirtualRoot", 0, 0, 0, 0, null);
            virtualRoot = new TreeNode(virtualRootData);

            // Build tree from all root objects in the scene
            BuildTreeFromScene(sceneName);
        }

        private void BuildTreeFromScene(string sceneName)
        {
            // Find all root GameObjects in the scene
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            List<GameObject> rootObjects = new List<GameObject>();

            foreach (GameObject obj in allObjects)
            {
                // Check if this is a root object (no parent) and belongs to the specified scene
                if (obj.scene.name == sceneName && obj.transform.parent == null)
                {
                    rootObjects.Add(obj);
                }
            }

            // Sort root objects by name for consistent ordering
            rootObjects.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            // Process each root object
            float xOffset = 0;
            foreach (GameObject rootObj in rootObjects)
            {
                TreeNode rootNode = CreateNodeFromGameObject(rootObj);
                
                // Adjust position for visualization
                rootNode.Data.X += xOffset;
                xOffset += rootNode.Data.Width + 50; // Add spacing between root nodes

                actualRoots.Add(rootNode);
                virtualRoot.AddChild(rootNode);
                
                // Build subtree for this root
                BuildTreeRecursive(rootObj, rootNode);
            }
        }

        private TreeNode CreateNodeFromGameObject(GameObject gameObject)
        {
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            float width = 150f;
            float height = 30f;

            if (rectTransform != null)
            {
                width = rectTransform.rect.width;
                height = rectTransform.rect.height;
            }

            Node node = new GameObjectNode(
                gameObject.name,
                gameObject.transform.position.x,
                gameObject.transform.position.y,
                width,
                height,
                gameObject
            );

            return new TreeNode(node, gameObject);
        }

        private void BuildTreeRecursive(GameObject gameObject, TreeNode parentNode)
        {
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Transform childTransform = gameObject.transform.GetChild(i);
                GameObject childObject = childTransform.gameObject;

                TreeNode childNode = CreateNodeFromGameObject(childObject);
                parentNode.AddChild(childNode);
                BuildTreeRecursive(childObject, childNode);
            }
        }



        // Traversal methods now start from actual roots instead of virtual root
        public List<Node> GetNodesInOrder(TraversalOrder order = TraversalOrder.PreOrder)
        {
            List<Node> nodes = new List<Node>();
            foreach (var root in actualRoots)
            {
                switch (order)
                {
                    case TraversalOrder.PreOrder:
                        PreOrderTraversal(root, nodes);
                        break;
                    case TraversalOrder.PostOrder:
                        PostOrderTraversal(root, nodes);
                        break;
                    case TraversalOrder.LevelOrder:
                        LevelOrderTraversal(root, nodes);
                        break;
                }
            }
            return nodes;
        }

        private void PreOrderTraversal(TreeNode node, List<Node> nodes)
        {
            if (node == null) return;
            nodes.Add(node.Data);
            foreach (var child in node.Children)
            {
                PreOrderTraversal(child, nodes);
            }
        }

        private void PostOrderTraversal(TreeNode node, List<Node> nodes)
        {
            if (node == null) return;
            foreach (var child in node.Children)
            {
                PostOrderTraversal(child, nodes);
            }
            nodes.Add(node.Data);
        }

        private void LevelOrderTraversal(TreeNode node, List<Node> nodes)
        {
            if (node == null) return;
            Queue<TreeNode> queue = new Queue<TreeNode>();
            queue.Enqueue(node);

            while (queue.Count > 0)
            {
                TreeNode current = queue.Dequeue();
                nodes.Add(current.Data);

                foreach (var child in current.Children)
                {
                    queue.Enqueue(child);
                }
            }
        }
    }

    public enum TraversalOrder
    {
        PreOrder,
        PostOrder,
        LevelOrder
    }
}