namespace _3DConnections.Runtime.Events
{
    using UnityEngine;
    using UnityEngine.Events;

    [CreateAssetMenu(fileName = "ToggleOverlayEvent", menuName = "3DConnections/Events/ToggleOverlayEvent")]
    public class ToggleOverlayEvent : ScriptableObject
    {
        public UnityAction OnEventTriggered;

        public void TriggerEvent()
        {
            OnEventTriggered?.Invoke();
        }
    }
}