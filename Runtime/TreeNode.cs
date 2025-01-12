using System.Collections.Generic;
using UnityEngine;

namespace _3DConnections.Runtime
{
    public class TreeNode
    {
        public GameObject GameObject;
        public List<TreeNode> Children = new();
        public List<TreeNode> Parents = new();
    }
}