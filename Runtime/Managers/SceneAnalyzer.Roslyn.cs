using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

public partial class SceneAnalyzer
{
    /// <summary>
    /// Use Roslyn to analyze source code for component references
    /// </summary>
    /// <param name="sourceCode"></param>
    /// <param name="sourceFile"></param>
    /// <returns></returns>
    private List<ComponentReference> AnalyzeSourceCodeForComponentReferences(string sourceCode, string sourceFile)
    {
        var references = new List<ComponentReference>();

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot();

            // Find all invocation expressions (method calls)
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
                var identifierName = invocation.Expression as IdentifierNameSyntax;

                string methodName = null;

                if (memberAccess != null)
                {
                    methodName = memberAccess.Name.Identifier.ValueText;
                }
                else if (identifierName != null)
                {
                    methodName = identifierName.Identifier.ValueText;
                }

                if (methodName == null) continue;

                // Check for AddComponent or GetComponent calls
                bool isAddComponent = showAddComponentCalls && methodName.StartsWith("AddComponent");
                bool isGetComponent = showGetComponentCalls && methodName.StartsWith("GetComponent");

                if (!isAddComponent && !isGetComponent) continue;

                // Extract the generic type argument or parameter
                Type componentType = ExtractComponentTypeFromInvocation(invocation);
                if (componentType == null) continue;

                var lineNumber = syntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;

                references.Add(new ComponentReference
                {
                    ReferencedComponentType = componentType,
                    MethodName = methodName,
                    LineNumber = lineNumber,
                    SourceFile = sourceFile
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error analyzing source code in {sourceFile}: {e.Message}");
        }

        return references;
    }
    
    /// <summary>
    /// Find the source .cs file for a given Type
    /// </summary>
    /// <param name="type">type to find the source cs file for</param>
    /// <returns></returns>
    private string FindSourceFileForType(Type type)
    {
#if UNITY_EDITOR
        // Try to find the script asset
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