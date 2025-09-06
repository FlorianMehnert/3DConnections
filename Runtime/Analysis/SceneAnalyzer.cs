// SceneAnalyzer.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _3DConnections.Runtime.Analysis;
using _3DConnections.Runtime.Managers.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
using ILogger = _3DConnections.Runtime.Analysis.ILogger;
using soi = _3DConnections.Runtime.ScriptableObjectInventory.ScriptableObjectInventory;

namespace _3DConnections.Runtime.Managers
{
    public class SceneAnalyzer : MonoBehaviour, ISceneAnalysisService
    {
        [Header("Analysis Data")]
        [SerializeField] private TextAsset analysisData;
        
        [Header("Node Settings")]
        [SerializeField] private GameObject parentNode;
        [SerializeField] private GameObject nodePrefab;
        [SerializeField] private NodeSettings nodeSettings = new();
        
        [Header("Analysis Settings")]
        [SerializeField] private ComponentAnalysisSettings componentSettings = new();
        [SerializeField] private TraversalSettings traversalSettings = new();

        // Services
        private INodeGraphManager _nodeManager;
        private IComponentReferenceAnalyzer _componentAnalyzer;
        private IEventAnalyzer _eventAnalyzer;
        private SceneTraversalService _traversalService;
        private IFileLocator _fileLocator;
        private ITypeResolver _typeResolver;
        private ILogger _logger;

        private void Start()
        {
            InitializeServices();
        }

        private void InitializeServices()
        {
            _logger = new UnityLogger();
            _fileLocator = new UnityFileLocator();
            _typeResolver = new UnityTypeResolver();
            
            _nodeManager = new NodeGraphManager(nodePrefab, parentNode, nodeSettings, _logger);
            _componentAnalyzer = new ComponentReferenceAnalyzer(_fileLocator, _typeResolver, _logger, componentSettings);
            _eventAnalyzer = new EventAnalyzer(_fileLocator, _typeResolver, _logger);
            _traversalService = new SceneTraversalService(_nodeManager, _logger, traversalSettings);
        }

        public void AnalyzeScene(int sceneIndex, Action onComplete = null)
        {
            var scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
            if (string.IsNullOrEmpty(scenePath))
            {
                _logger.LogError($"No scene found at build index {sceneIndex}");
                return;
            }

            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            var scene = SceneManager.GetSceneByName(sceneName);

            if (!scene.isLoaded)
            {
                _logger.Log($"Scene '{sceneName}' is not loaded. Loading additively...");
                StartCoroutine(SceneHandler.LoadSceneAndInvokeAfterCoroutine(sceneName, Analyze));
                return;
            }

            StartCoroutine(RunNextFrame(Analyze));
            return;

            void Analyze()
            {
                scene = SceneManager.GetSceneByName(sceneName);
                _logger.Log($"Analyzing scene: {scene.name} (build index {sceneIndex})");

                // Clear previous analysis
                ClearAnalysis();

                // Traverse scene
                _traversalService.TraverseScene(scene.GetRootGameObjects());

                // Analyze dynamic references if enabled
                if (soi.Instance.analyzerConfigurations.lookupDynamicReferences)
                {
                    var context = _traversalService.GetContext();
                    _logger.Log($"Analyzing dynamic references for {context.DiscoveredMonoBehaviours.Count} MonoBehaviour types");
                    
                    AnalyzeDynamicReferences(context.DiscoveredMonoBehaviours);
                    AnalyzeEvents(context.DiscoveredMonoBehaviours);
                }

                onComplete?.Invoke();
                
                // Update node count
                soi.Instance.graph.InvokeOnAllCountChanged();
            }
        }

        public void ClearAnalysis()
        {
            _nodeManager.ClearNodes();
            soi.Instance.applicationState.spawnedNodes = false;
            soi.Instance.graph.Initialize();
        }

        public IReadOnlyList<GameObject> GetAllNodes()
        {
            return _nodeManager.NodeLookup.Values.Where(node => node != null).ToList();
        }

        private void AnalyzeDynamicReferences(IEnumerable<Type> monoBehaviourTypes)
        {
            foreach (var type in monoBehaviourTypes)
            {
                _componentAnalyzer.AnalyzeComponentReferences(type);
            }
            
            _componentAnalyzer.CreateDynamicConnections(_nodeManager);
        }

        private void AnalyzeEvents(IEnumerable<Type> monoBehaviourTypes)
        {
            _eventAnalyzer.AnalyzeEvents(monoBehaviourTypes);
            _eventAnalyzer.CreateEventConnections(_nodeManager);
        }

        private IEnumerator RunNextFrame(Action action)
        {
            yield return null;
            action();
        }

        private void OnEnable()
        {
            if (soi.Instance.clearEvent != null)
                soi.Instance.clearEvent.onEventTriggered.AddListener(HandleClearEvent);
            if (soi.Instance.removePhysicsEvent != null)
                soi.Instance.removePhysicsEvent.OnEventTriggered += HandleRemovePhysicsEvent;
        }

        private void OnDisable()
        {
            if (!soi.Instance) return;
            if (soi.Instance.clearEvent != null)
                soi.Instance.clearEvent.onEventTriggered.RemoveListener(HandleClearEvent);
            if (soi.Instance.removePhysicsEvent != null)
                soi.Instance.removePhysicsEvent.OnEventTriggered -= HandleRemovePhysicsEvent;
        }

        private void HandleClearEvent()
        {
            ClearAnalysis();
        }

        private static void HandleRemovePhysicsEvent()
        {
            var types = new List<Type> { typeof(SpringJoint2D), typeof(Rigidbody2D) };
            var parentObject = SceneHandler.GetParentObject();
            if (parentObject)
            {
                soi.Instance.graph.NodesRemoveComponents(types,
                    SceneHandler.GetNodesUsingTheNodegraphParentObject());
            }
        }
    }
}
