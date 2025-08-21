namespace _3DConnections.Runtime.ScriptableObjectInventory
{
    using Events;
    using UnityEngine;
    using ScriptableObjects;

    public sealed class ScriptableObjectInventory : MonoBehaviour
    {
        private static ScriptableObjectInventory _instance;

        [Header("Component based physics sim")]
        public PhysicsSimulationConfiguration simConfig;

        public NodeConnectionsScriptableObject conSo;

        public NodeGraphScriptableObject graph;

        public NodeColorsScriptableObject nodeColors;

        public MenuState menuState;

        public LayoutParameters layout;

        public OverlaySceneScriptableObject overlay;

        public ApplicationState applicationState;

        public AnalyzerConfigurations analyzerConfigurations;

        public RemovePhysicsEvent removePhysicsEvent;

        public ClearEvent clearEvent;

        public ToggleOverlayEvent toggleOverlayEvent;

        public UpdateLOD updateLOD;

        private static bool _isShuttingDown;

        public Transform simulationRoot;
        public Transform managerRoot;
        public Transform nodeRoot;
        public Transform edgeRoot;

        /// <summary>
        /// If this is used in OnDestroy make sure to delete this object again
        /// or even better check if this exists using InstanceExists
        /// </summary>
        public static ScriptableObjectInventory Instance
        {
            get
            {
                if (_isShuttingDown)
                {
                    return null;
                }

                if (_instance) return _instance;

                _instance = FindFirstObjectByType<ScriptableObjectInventory>();
                if (_instance) return _instance;

                var singletonObject = new GameObject("ScriptableObjectInventory");
                _instance = singletonObject.AddComponent<ScriptableObjectInventory>();
                return _instance;
            }
        }

        public static bool InstanceExists => _instance;


        private void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;

            // Making sure this is not recreated it during shutdown
            _isShuttingDown = true;
        }
        
        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }
    }
}