using System;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _3DConnections.Runtime.Analysis
{
    public class UnityFileLocator : IFileLocator
    {
        public string FindSourceFileForType(Type type)
        {
#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets($"t:MonoScript {type.Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script && script.GetClass() == type)
                {
                    return Path.GetFullPath(path);
                }
            }
#endif
            return null;
        }
    }

    public class UnityTypeResolver : ITypeResolver
    {
        public Type FindTypeByName(string typeName)
        {
            // Try direct type resolution first
            var type = Type.GetType(typeName);
            if (type != null) return type;

            // Try with UnityEngine namespace
            type = Type.GetType($"UnityEngine.{typeName}");
            if (type != null) return type;

            // Try to find in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;

                type = assembly.GetType($"UnityEngine.{typeName}");
                if (type != null) return type;
            }

            return null;
        }
    }

    public class UnityLogger : ILogger
    {
        public void Log(string message) => UnityEngine.Debug.Log(message);
        public void LogWarning(string message) => UnityEngine.Debug.LogWarning(message);
        public void LogError(string message) => UnityEngine.Debug.LogError(message);
    }
}