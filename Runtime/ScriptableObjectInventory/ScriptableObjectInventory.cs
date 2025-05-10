using UnityEngine;

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
        
        public ToAnalyzeScene toAnalyzeScene;

        public RemovePhysicsEvent removePhysicsEvent;
        
        public ClearEvent clearEvent;
        
        public ToggleOverlayEvent toggleOverlayEvent;

        private static bool _isShuttingDown;

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
        }

}