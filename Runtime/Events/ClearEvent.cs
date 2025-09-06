namespace _3DConnections.Runtime.Events
{
    using UnityEngine;
    using UnityEngine.Events;

    [CreateAssetMenu(fileName = "Clear Nodes Event", menuName = "3DConnections/Events/Clear Nodes Event")]
    public class ClearEvent : ScriptableObject
    {
        public UnityEvent onEventTriggered;

        public void TriggerEvent()
        {
            Debug.Log("ClearEvent.TriggerEvent() called");
            onEventTriggered?.Invoke();
        }
    }
}