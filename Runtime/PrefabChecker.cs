namespace _3DConnections.Runtime
{
#if UNITY_EDITOR
    using UnityEngine;
    using UnityEditor;

    public class PrefabChecker
    {
        public static PrefabType CheckPrefabStatus(GameObject obj)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(obj))
            {
                Debug.Log($"{obj.name} is a prefab asset.");
                return PrefabType.PrefabAsset;
            }
            else if (PrefabUtility.IsPartOfPrefabInstance(obj))
            {
                Debug.Log($"{obj.name} is a prefab instance in the scene.");
                return PrefabType.PrefabInstance;
            }
            else
            {
                Debug.Log($"{obj.name} is a regular GameObject (not part of a prefab).");
                return PrefabType.GameObject;
            }
        }
    }
#endif
}