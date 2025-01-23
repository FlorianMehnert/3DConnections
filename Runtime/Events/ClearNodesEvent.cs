using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "Clear Nodes Event", menuName = "3DConnections/Events/ClearNodesEvent")]
public class ClearNodesEvent : ScriptableObject
{
    public UnityAction OnEventTriggered;

    public void TriggerEvent()
    {
        OnEventTriggered?.Invoke();
    }
}