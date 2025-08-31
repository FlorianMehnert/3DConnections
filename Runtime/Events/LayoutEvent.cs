using UnityEngine;
using UnityEngine.Events;

namespace _3DConnections.Runtime.Events
{
    [CreateAssetMenu(fileName = "LayoutEvent", menuName = "3DConnections/Events/Layout Nodes Event")]
    public class LayoutEvent : ScriptableObject
    {
        public UnityEvent onEventTriggered;

        public void TriggerEvent()
        {
            onEventTriggered?.Invoke();
        }
    }
}