using UnityEngine;

namespace _3DConnections.Runtime.ScriptableObjects
{
    [Tooltip("Holds the current simulation type")]
    [CreateAssetMenu(fileName = "Simulation Parameters",
        menuName = "3DConnections/ScriptableObjects/Simulation Parameters", order = 1)]
    public class SimulationParameters : ScriptableObject
    {
        public SimulationType simulationType = SimulationType.Static;
    }
}