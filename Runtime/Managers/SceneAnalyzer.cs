namespace _3DConnections.Runtime.Managers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

#if UNITY_EDITOR
    using UnityEditor;
#endif
    using UnityEngine;
    using UnityEngine.SceneManagement;
    
    using ScriptableObjectInventory;
    using Scene;

    public partial class SceneAnalyzer : MonoBehaviour
    {
        private void Start()
        {
#if UNITY_EDITOR
            _cachedPrefabPaths = AssetDatabase.FindAssets("t:Prefab").ToList();
#endif
        }


        /// <summary>
        /// Function that is invoked by calling "Analyze scene" in the GUI. Entrypoint into scene analysis and graph building.
        /// </summary>
        /// <param name="onComplete"></param>
        public void AnalyzeScene(Action onComplete = null)
        {
            _currentNodes = 0;
            _visitedObjects.Clear();
            _processingObjects.Clear();
            _instanceIdToNodeLookup.Clear();
            _discoveredMonoBehaviours.Clear();
            _dynamicComponentReferences.Clear();

#if UNITY_EDITOR
            _cachedPrefabPaths = AssetDatabase.FindAssets("t:Prefab").ToList();
#endif

            var scenePath = SceneUtility.GetScenePathByBuildIndex(toAnalyzeScene.sceneIndex);
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError($"No scene found at build index {toAnalyzeScene.sceneIndex}");
                return;
            }

            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            var scene = SceneManager.GetSceneByName(sceneName);

            void Analyze()
            {
                scene = SceneManager.GetSceneByName(sceneName);
                Debug.Log($"{scene.name} (build index {toAnalyzeScene.sceneIndex})");

                LoadComplexityMetrics(analysisData.ToString());
                _cachedPrefabPaths.Clear();
                TraverseScene(scene.GetRootGameObjects());

                // Analyze dynamic component references after scene traversal
                if (analyzeDynamicReferences)
                {
                    Debug.Log(
                        $"Analyzing dynamic references for {_discoveredMonoBehaviours.Count} MonoBehaviour types");
                    AnalyzeDynamicComponentReferences();
                    CreateDynamicConnections();
                    AnalyzeEventSubscriptions();
                    CreateEventConnections();
                }

                onComplete?.Invoke();
            }

            if (!scene.isLoaded)
            {
                Debug.Log($"Scene '{sceneName}' is not loaded. Loading additively...");
                StartCoroutine(SceneHandler.LoadSceneAndInvokeAfterCoroutine(sceneName, Analyze));
                return;
            }

            // Scene is already loaded
            StartCoroutine(RunNextFrame(Analyze));
        }

        private IEnumerator RunNextFrame(Action action)
        {
            yield return null;
            action();
        }

        private void OnEnable()
        {
            if (ScriptableObjectInventory.Instance.clearEvent)
                ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered += HandleEvent;
            if (ScriptableObjectInventory.Instance.removePhysicsEvent)
                ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered += HandleRemovePhysicsEvent;
        }

        private void OnDisable()
        {
            if (!ScriptableObjectInventory.InstanceExists) return;
            if (ScriptableObjectInventory.Instance.clearEvent)
                ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered -= HandleEvent;
            if (ScriptableObjectInventory.Instance.removePhysicsEvent)
                ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered -= HandleRemovePhysicsEvent;
        }

        private void TraverseScene(GameObject[] rootGameObjects)
        {
            if (rootGameObjects == null)
            {
                Debug.Log("In traverse scene, however there are not gameobjects in the scene");
                return;
            }

            GameObject rootNode = null;
            if (spawnRootNode)
            {
                rootNode = SpawnNode(null);
                if (!rootNode)
                {
                    Debug.Log("Root Node could not be spawned");
                    return;
                }
            }

            foreach (var rootObject in rootGameObjects)
                TraverseGameObject(rootObject, parentNodeObject: rootNode, depth: 0);

            if (_instanceIdToNodeLookup != null && ScriptableObjectInventory.Instance.graph &&
                ScriptableObjectInventory.Instance.graph.AllNodes is { Count: 0 })
                ScriptableObjectInventory.Instance.graph.AllNodes = _instanceIdToNodeLookup.Values.ToList();

            if (ScriptableObjectInventory.Instance.graph.AllNodes is { Count: > 0 } && rootNode)
                ScriptableObjectInventory.Instance.graph.AllNodes.Add(rootNode);
        }


        private void HandleEvent()
        {
            if (!ScriptableObjectInventory.InstanceExists) return;
            ClearNodes();
            ScriptableObjectInventory.Instance.applicationState.spawnedNodes = false;
            ScriptableObjectInventory.Instance.graph.Initialize();
        }

        private void HandleRemovePhysicsEvent()
        {
            var types = new List<Type>
            {
                typeof(SpringJoint2D),
                typeof(Rigidbody2D)
            };
            var parentObject = SceneHandler.GetParentObject();
            if (!parentObject)
                return;
            ScriptableObjectInventory.Instance.graph.NodesRemoveComponents(types,
                SceneHandler.GetNodesUsingTheNodegraphParentObject());
        }
    }
}