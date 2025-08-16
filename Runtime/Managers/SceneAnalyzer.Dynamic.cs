namespace _3DConnections.Runtime.Managers
{
    using System;
    using System.IO;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using UnityEngine;
    
    using ScriptableObjectInventory;

    public partial class SceneAnalyzer : MonoBehaviour
    {
        /// <summary>
        /// Analyze MonoBehaviour scripts for AddComponent/GetComponent calls
        /// </summary>
        private void AnalyzeDynamicComponentReferences()
        {
            foreach (var monoBehaviourType in _discoveredMonoBehaviours)
            {
                try
                {
                    var sourceFile = FindSourceFileForType(monoBehaviourType);
                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    var sourceCode = File.ReadAllText(sourceFile);
                    var references = AnalyzeSourceCodeForComponentReferences(sourceCode, sourceFile);

                    if (references.Count <= 0) continue;
                    _dynamicComponentReferences[monoBehaviourType] = references;
                    Debug.Log($"Found {references.Count} dynamic references in {monoBehaviourType.Name}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not analyze {monoBehaviourType.Name}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Extract component type from AddComponent/GetComponent invocation
        /// </summary>
        /// <param name="invocation"></param>
        /// <returns></returns>
        private Type ExtractComponentTypeFromInvocation(InvocationExpressionSyntax invocation)
        {
            // Handle generic method calls like AddComponent<Rigidbody>()
            if (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName })
            {
                var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                if (typeArg != null)
                {
                    var typeName = typeArg.ToString();
                    return FindTypeByName(typeName);
                }
            }

            // Handle method calls with Type parameter like AddComponent(typeof(Rigidbody))
            if (invocation.ArgumentList.Arguments.Count <= 0) return null;
            {
                var firstArg = invocation.ArgumentList.Arguments[0].Expression;
                if (firstArg is not TypeOfExpressionSyntax typeOfExpr) return null;
                var typeName = typeOfExpr.Type.ToString();
                return FindTypeByName(typeName);
            }

            return null;
        }

        /// <summary>
        /// Find Type by name (with fallback for common Unity types)
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        private Type FindTypeByName(string typeName)
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

        /// <summary>
        /// Create visual connections for dynamic references
        /// </summary>
        private void CreateDynamicConnections()
        {
            foreach (var kvp in _dynamicComponentReferences)
            {
                var sourceType = kvp.Key;
                var references = kvp.Value;

                // Find the source component node
                GameObject sourceNode = FindNodeByComponentType(sourceType);
                if (sourceNode == null) continue;

                foreach (var reference in references)
                {
                    // Find or create target component node
                    GameObject targetNode = FindOrCreateNodeForComponentType(reference.ReferencedComponentType);
                    if (targetNode == null) continue;

                    // Create dynamic connection with orange color
                    var connectionColor = new Color(dynamicComponentConnection.r, dynamicComponentConnection.g,
                        dynamicComponentConnection.b, 0.7f);
                    sourceNode.ConnectNodes(targetNode, connectionColor, 0, "dynamicComponentConnection",
                        ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);

                    Debug.Log(
                        $"Created dynamic connection: {sourceType.Name} -> {reference.ReferencedComponentType.Name} ({reference.MethodName})");
                }
            }
        }
    }
}