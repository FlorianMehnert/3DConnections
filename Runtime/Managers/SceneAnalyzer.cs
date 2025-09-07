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
using Object = System.Object;
using soi = _3DConnections.Runtime.ScriptableObjectInventory.ScriptableObjectInventory;

namespace _3DConnections.Runtime.Managers
{
    public class SceneAnalyzer : MonoBehaviour, ISceneAnalysisService
    {
        [Header("Analysis Data")] [SerializeField]
        private TextAsset analysisData;

        [Header("Node Settings")] [SerializeField]
        private GameObject parentNode;

        [SerializeField] private GameObject nodePrefab;
        [SerializeField] private NodeSettings nodeSettings = new();

        [Header("Analysis Settings")] [SerializeField]
        private ComponentAnalysisSettings componentSettings = new();

        [SerializeField] private TraversalSettings traversalSettings = new();

        // Services
        private INodeGraphManager _nodeManager;
        private IComponentReferenceAnalyzer _componentAnalyzer;
        private IEventAnalyzer _eventAnalyzer;
        private SceneTraversalService _traversalService;
        private IFileLocator _fileLocator;
        private ITypeResolver _typeResolver;
        private ILogger _logger;
        private IProgressReporter _progressReporter;
        public readonly List<Object> IgnoredTypes = new();

        private void Start()
        {
            InitializeServices();
        }

        private void InitializeServices()
        {
            _logger = new UnityLogger();
            _fileLocator = new UnityFileLocator();
            _typeResolver = new UnityTypeResolver();

            // Initialize progress reporter based on context
#if UNITY_EDITOR
            _progressReporter = new UnityProgressReporter();
#else
            _progressReporter = new ConsoleProgressReporter();
#endif

            _nodeManager = new NodeGraphManager(nodePrefab, parentNode, nodeSettings, _logger);
            _componentAnalyzer = new ComponentReferenceAnalyzer(_fileLocator, _typeResolver, _logger, componentSettings,
                _progressReporter);
            _eventAnalyzer = new EventAnalyzer(_fileLocator, _typeResolver, _logger, _progressReporter);
            _traversalService = new SceneTraversalService(_nodeManager, _logger, traversalSettings, _progressReporter);
        }

        public void AnalyzeScene(Action onComplete = null)
        {
            var scenePath = SceneUtility.GetScenePathByBuildIndex(soi.Instance.analyzerConfigurations.sceneIndex);
            if (string.IsNullOrEmpty(scenePath))
            {
                _logger.LogError($"No scene found at build index {soi.Instance.analyzerConfigurations.sceneIndex}");
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
                try
                {
                    scene = SceneManager.GetSceneByName(sceneName);
                    _logger.Log(
                        $"Analyzing scene: {scene.name} (build index {soi.Instance.analyzerConfigurations.sceneIndex})");

                    // Start overall progress tracking
                    int totalSteps = soi.Instance.analyzerConfigurations.lookupDynamicReferences ? 3 : 1;
                    _progressReporter.StartOperation("Complete Scene Analysis", totalSteps);

                    // Clear previous analysis
                    ClearAnalysis();

                    // Step 1: Traverse scene
                    _progressReporter.ReportProgress("Scene Analysis", 1, totalSteps, "Traversing scene hierarchy");
                    _traversalService.TraverseScene();

                    // Analyze dynamic references if enabled
                    if (soi.Instance.analyzerConfigurations.lookupDynamicReferences)
                    {
                        var context = _traversalService.GetContext();
                        _logger.Log(
                            $"Analyzing dynamic references for {context.DiscoveredMonoBehaviours.Count} MonoBehaviour types");

                        // Step 2: Analyze component references
                        _progressReporter.ReportProgress("Scene Analysis", 2, totalSteps,
                            "Analyzing component references");
                        AnalyzeDynamicReferences(context.DiscoveredMonoBehaviours);

                        // Step 3: Analyze events
                        _progressReporter.ReportProgress("Scene Analysis", 3, totalSteps, "Analyzing events");
                        AnalyzeEvents(context.DiscoveredMonoBehaviours);
                    }

                    _progressReporter.CompleteOperation();
                    onComplete?.Invoke();

                    // Update node count
                    soi.Instance.graph.InvokeOnAllCountChanged();
                }
                catch (OperationCanceledException)
                {
                    _logger.Log("Analysis cancelled by user");
                    onComplete?.Invoke();
                }
                catch (Exception e)
                {
                    _logger.LogError($"Analysis failed: {e.Message}");
                    _progressReporter.CompleteOperation();
                    onComplete?.Invoke();
                }
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