using UnityEngine;
using UnityEngine.Events;

namespace _3DConnections.Runtime.Events
{
    [CreateAssetMenu(fileName = "SimulationEvent", menuName = "3DConnections/Events/Simulate Nodes Event")]
    public class SimulationEvent : ScriptableObject
    {
        public UnityEvent onEventTriggered;

        public void TriggerEvent()
        {
            onEventTriggered?.Invoke();
        }
    }
}