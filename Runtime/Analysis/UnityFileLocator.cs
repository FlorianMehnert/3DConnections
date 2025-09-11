using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor.Compilation;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace _3DConnections.Runtime.Analysis
{
    public class UnityFileLocator : IFileLocator
    {
#if UNITY_EDITOR
        private readonly HashSet<string> _allowedAssemblies;
#endif
        public UnityFileLocator(IEnumerable<string> allowedAssemblyNames = null)
        {
#if UNITY_EDITOR
            _allowedAssemblies = allowedAssemblyNames != null
                ? new HashSet<string>(allowedAssemblyNames.Where(s => !string.IsNullOrWhiteSpace(s)))
                : new HashSet<string>();
#endif
        }

        public string FindSourceFileForType(Type type)
        {
#if UNITY_EDITOR
            if (type == null) return null;
            var asmName = type.Assembly.GetName().Name;
            if (_allowedAssemblies.Count > 0 && !_allowedAssemblies.Contains(asmName))
                return null;
            var asm = CompilationPipeline.GetAssemblies().FirstOrDefault(a => a.name == asmName);
            if (asm == null || asm.sourceFiles == null || asm.sourceFiles.Length == 0)
                return null;

            // Heuristic 1: only consider files matching the class name (fast filter)
            var candidates = asm.sourceFiles.Where(p =>
            {
                try
                {
                    return string.Equals(Path.GetFileNameWithoutExtension(p), type.Name, StringComparison.Ordinal);
                }
                catch
                {
                    return false;
                }
            });

            foreach (var fullPath in candidates)
            {
                var assetPath = ToAssetDatabasePath(fullPath);
                if (string.IsNullOrEmpty(assetPath))
                    continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (script && script.GetClass() == type)
                {
                    // Return an absolute, normalized path
                    return Path.GetFullPath(assetPath);
                }
            }

            // If class name != file name (partial classes, nested types), fall back to scanning all files in the assembly
            foreach (var fullPath in asm.sourceFiles)
            {
                var assetPath = ToAssetDatabasePath(fullPath);
                if (string.IsNullOrEmpty(assetPath))
                    continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (script && script.GetClass() == type)
                    return Path.GetFullPath(assetPath);
            }
#endif
            return null;
        }
#if UNITY_EDITOR // Convert absolute filesystem path to an AssetDatabase path ("Assets/...")
        private static string ToAssetDatabasePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return null;
            try
            {
                var full = Path.GetFullPath(absolutePath).Replace('\'', '/');
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                    .Replace('\'', '/');
                if (!full.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                    return null;
                var relative = full.Substring(projectRoot.Length).TrimStart('/');
                // Expect "Assets/..." or "Packages/..." here. AssetDatabase supports both.
                if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                {
                    return relative;
                }
                // If it's under the project but not under Assets/ or Packages/, it's not importable by AssetDatabase.
                return null;
            }
            catch
            {
                return null;
            }
        }
#endif
    }
    

    public class UnityTypeResolver : ITypeResolver { 
        public Type FindTypeByName(string typeName) { 
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            
            // Normalize Roslyn’s fully-qualified tokens
            var cleaned = typeName.Replace("global::", string.Empty).Trim();
            var candidates = new List<string> { cleaned };
            // If it’s a short name, try common Unity namespaces too.
            var looksQualified = cleaned.Contains(".");
            if (!looksQualified)
            {
                var prefixes = new[]
                {
                    "",
                    "UnityEngine.",
                    "UnityEngine.UI.",
                    "UnityEngine.EventSystems.",
                    "TMPro.",
                    "UnityEngine.Rendering.",
                    "UnityEngine.AI.",
                    "UnityEngine.InputSystem."
                };
                candidates.AddRange(prefixes.Select(p => p + cleaned));
            }
            // 1) Direct Type.GetType on candidates
            foreach (var t in candidates.Select(Type.GetType).Where(t => t != null))
            {
                return t;
            }
            // 2) Search all loaded assemblies using full name first
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));
            
            foreach (var asm in assemblies)
            {
                foreach (var t in candidates.Select(c => asm.GetType(c)).Where(t => t != null))
                {
                    return t;
                }
            }
            
            // 3) If still unresolved and the original was a short name, fall back to a slow short-name scan
            if (looksQualified) return null;
            {
                foreach (var asm in assemblies)
                {
                    Type t = null;
                    try
                    {
                        t = asm.GetTypes().FirstOrDefault(x => x.Name == cleaned);
                    }
                    catch
                    {
                        // Some assemblies might throw during GetTypes; ignore and continue
                    }
                    if (t != null) return t;
                }
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