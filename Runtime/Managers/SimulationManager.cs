using _3DConnections.Runtime.Events;

namespace _3DConnections.Runtime.Managers
{
    using System.Linq;
    using UnityEngine;
    using ScriptableObjectInventory;
    using Simulations;
    
    public class SimulationManager : MonoBehaviour
    {
        [SerializeField] private Transform rootSimulation;
        
        [SerializeField] private SimulationEvent simulationEvent;

        private void OnEnable()
        {
            if (rootSimulation) return;
            var rootEdgeGameObject = GameObject.Find("Simulations");
            rootSimulation = rootEdgeGameObject.transform
                ? rootEdgeGameObject.transform
                : new GameObject("ParentEdgesObject").transform;
            ScriptableObjectInventory.Instance.simulationRoot = rootSimulation;
        }

        private static void ApplyComponentPhysics()
        {
            ScriptableObjectInventory.Instance.graph.NodesAddComponent(typeof(Rigidbody2D));

            // required to avoid intersections when using components
            foreach (var nodeCollider2D in ScriptableObjectInventory.Instance.graph.AllNodes.Select(node =>
                         node.GetComponent<Collider2D>()))
            {
                try
                {
                    nodeCollider2D.isTrigger = false;
                    if (typeof(BoxCollider2D).IsAssignableFrom(nodeCollider2D.GetType()))
                    {
                        ((BoxCollider2D)nodeCollider2D).size = Vector2.one * 5;
                    }
                    else
                    {
                        // skip cluster nodes that have the circle collider e.g. LOD nodes
                    }
                }
                catch (MissingComponentException e)
                {
                    Debug.LogError(e);
                }
            }

            NodeConnectionManager.Instance.AddSpringsToConnections();
        }

        private void ApplyStaticLayout()
        {
            if (ScriptableObjectInventory.Instance.graph.AllNodes.Count <= 0) return;
            ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
        }

        private void ApplyForceDirectedComponentPhysics()
        {
            var layout = ScriptableObjectInventory.Instance.simulationRoot.gameObject
                .GetComponent<ForceDirectedSimulationV2>();
            if (!layout)
                layout = ScriptableObjectInventory.Instance.simulationRoot.gameObject
                    .AddComponent<ForceDirectedSimulationV2>();

            if (!layout) return;
            if (ScriptableObjectInventory.Instance.graph.AllNodes.Count <= 0) return;
            ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
            layout.Initialize();
        }


        public void Simulate()
        {
            if (ScriptableObjectInventory.Instance.graph.AllNodes.Count <= 0) return;
            ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
            simulationEvent.Raise(ScriptableObjectInventory.Instance.simulationParameters.simulationType);
        }
    }
}