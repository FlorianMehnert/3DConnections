using UnityEngine;

namespace _3DConnections.Runtime.Events
{
    [Tooltip("Holds all events that are triggered by the Analyze Scene button")]
    [CreateAssetMenu(fileName = "AnalyzeEventChannel", menuName = "3DConnections/Events/Analyze Scene Event Channel")]
    public class AnalyzeEventChannel : ScriptableObject
    {
        public LayoutEvent layoutEvent;
        public SimulationEvent simulationEvent;

        public void TriggerEvent()
        {
            layoutEvent.TriggerEvent();
            simulationEvent.TriggerEvent();
        }
    }
}