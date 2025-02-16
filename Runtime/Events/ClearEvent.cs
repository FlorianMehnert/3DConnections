using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "Clear Nodes Event", menuName = "3DConnections/Events/Clear Nodes Event")]
public class ClearEvent : ScriptableObject
{
    public UnityAction OnEventTriggered;

    public void TriggerEvent()
    {
        OnEventTriggered?.Invoke();
    }
}