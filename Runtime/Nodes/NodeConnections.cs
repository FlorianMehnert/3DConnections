using System.Collections.Generic;
using UnityEngine;

public class NodeConnections : MonoBehaviour
{
    [SerializeField] public List<GameObject> inConnections = new();

    [SerializeField] public List<GameObject> outConnections = new();
}