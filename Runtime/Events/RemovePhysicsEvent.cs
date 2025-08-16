namespace _3DConnections.Runtime.Events
{
    using UnityEngine;
    using UnityEngine.Events;

    [CreateAssetMenu(fileName = "Remove Physics Event", menuName = "3DConnections/Events/Remove Physics Event")]
    public class RemovePhysicsEvent : ScriptableObject
    {
        public UnityAction OnEventTriggered;

        public void TriggerEvent()
        {
            OnEventTriggered?.Invoke();
        }
    }
}