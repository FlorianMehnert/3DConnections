using System.Collections.Generic;
using _3DConnections.Runtime.ScriptableObjects;
using UnityEngine;

namespace _3DConnections.Runtime
{
    public class NodeConnections : MonoBehaviour
    {
        [SerializeField]
        public List<GameObject> inConnections = new();

        [SerializeField]
        public List<GameObject> outConnections = new();
    }
}