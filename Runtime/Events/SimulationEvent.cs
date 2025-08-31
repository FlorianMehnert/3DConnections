using System;
using _3DConnections.Runtime.ScriptableObjects;
using UnityEngine;
using UnityEngine.Events;

namespace _3DConnections.Runtime.Events
{
    [CreateAssetMenu(fileName = "SimulationEvent", menuName = "3DConnections/Events/Simulate Nodes Event")]
    public class SimulationEvent : ScriptableObject
    {
        public event Action<SimulationType> OnSimulationRequested;

        public void Raise(SimulationType type)
        {
            OnSimulationRequested?.Invoke(type);
        }
    }
}