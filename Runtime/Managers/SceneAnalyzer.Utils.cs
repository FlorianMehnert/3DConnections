namespace _3DConnections.Runtime.Managers
{
    using System.Linq;
    using UnityEngine;
    using Simulations;

#if UNITY_EDITOR
    using UnityEditor;
#endif
    
    using ScriptableObjectInventory;

    public partial class SceneAnalyzer
    {
        /// <summary>
        /// Delete internal datastructures of <see cref="SceneAnalyzer"/> and delete all children GameObjects (nodes) of the root node 
        /// </summary>
        private void ClearNodes()
        {
            if (!parentNode) Debug.Log("nodeGraph gameObject unknown in ClearNodes for 3DConnections.SceneAnalyzer");
            parentNode = ScriptableObjectInventory.Instance.overlay.GetNodeGraph();
            if (!parentNode)
                Debug.Log("Even after asking the overlay SO for the nodeGraph gameObject it could not be found");

            Debug.Log("about to delete " + parentNode.transform.childCount + " nodes");
            foreach (Transform child in parentNode.transform)
                Destroy(child.gameObject);

            if (NodeConnectionManager.Instance)
                NodeConnectionManager.ClearConnections();
            var springSimulation = GetComponent<SpringSimulation>();
            if (springSimulation) springSimulation.CleanupNativeArrays();

            _instanceIdToNodeLookup.Clear();
            _visitedObjects.Clear();
            _processingObjects.Clear();
            _discoveredMonoBehaviours.Clear();
            _dynamicComponentReferences.Clear();
            _eventPublishers.Clear();
            _eventSubscriptions.Clear();
            _currentNodes = 0;
            ScriptableObjectInventory.Instance.graph.AllNodes.Clear();
        }

        /// <summary>
        /// Checks whether the object has anything to do with prefabs.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
#if UNITY_EDITOR
        private bool IsPrefab(Object obj)
        {
#if UNITY_2018_3_OR_NEWER
            if (!obj) return false;

            // Check if it's a prefab instance in the scene (root or child)
            var status = PrefabUtility.GetPrefabInstanceStatus(obj);
            if (status is PrefabInstanceStatus.Connected or PrefabInstanceStatus.MissingAsset)
                return true;

            // Check if part of any prefab
            if (PrefabUtility.IsPartOfPrefabInstance(obj) || PrefabUtility.IsPartOfAnyPrefab(obj))
                return true;

            // Additional checks for GameObjects
            if (obj is GameObject go)
            {
                // Check root object's prefab status
                var root = go.transform.root.gameObject;
                if (root && PrefabUtility.GetPrefabInstanceStatus(root) == PrefabInstanceStatus.Connected)
                    return true;

                // Check for prefab asset path
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (!string.IsNullOrEmpty(path))
                    return true;
            }

            if (!searchForPrefabsUsingNames) return PrefabUtility.GetPrefabInstanceHandle(obj);
            var gameObjectName = obj.name;

            return _cachedPrefabPaths.Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Any(prefab => prefab && prefab.name == gameObjectName);
#else
	        return PrefabUtility.GetPrefabType(go) != PrefabType.None;
#endif
        }
#else
    private static bool IsPrefab(Object obj)
    {
        return false;
    }
#endif

        private static bool IsAsset(Object obj)
        {
            if (!obj)
                return false;

            // Check if the instance ID is negative (asset) or positive (scene object)
            return obj.GetInstanceID() < 0;
        }
    }
}