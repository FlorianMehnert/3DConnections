using _3DConnections.Runtime.Events;
using _3DConnections.Runtime.ScriptableObjects;
using UnityEngine;
using soi = _3DConnections.Runtime.ScriptableObjectInventory.ScriptableObjectInventory;

namespace _3DConnections.Runtime.Simulations
{
    public class ComponentBasedSpringSimulation : MonoBehaviour, ILogable
    {
        [SerializeField] private SimulationEvent simulationEvent;
        public void OnEnable()
        {
            simulationEvent.OnSimulationRequested += Simulate;
        }

        public void OnDisable()
        {
            simulationEvent.OnSimulationRequested -= Simulate;
        }

        private static void Simulate(SimulationType simulationType)
        {
            if (simulationType != SimulationType.Default) return;
            soi.Instance?.graph?.NodesAddComponent(typeof(Rigidbody2D));
            // TODO: implement this again using event? NodeConnectionManager.Instance?.AddSpringsToConnections();
        }

        public string GetStatus()
        {
            return "ComponentBasedSpringSimulation is maybe running lol";
        }
        
    }
}