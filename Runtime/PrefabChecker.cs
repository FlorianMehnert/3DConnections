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

            if (PrefabUtility.IsPartOfPrefabInstance(obj))
            {
                Debug.Log($"{obj.name} is a prefab instance in the scene.");
                return PrefabType.PrefabInstance;
            }

            Debug.Log($"{obj.name} is a regular GameObject (not part of a prefab).");
            return PrefabType.GameObject;
        }
    }
#endif
