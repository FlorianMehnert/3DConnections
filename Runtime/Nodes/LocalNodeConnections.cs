using System.Collections.Generic;
using UnityEngine;

public class LocalNodeConnections : MonoBehaviour
{
    [SerializeField] public List<GameObject> inConnections = new();

    [SerializeField] public List<GameObject> outConnections = new();
}