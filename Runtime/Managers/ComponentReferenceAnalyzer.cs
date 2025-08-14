using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Standalone analyzer for finding AddComponent and GetComponent calls in C# source code
/// </summary>
public static class ComponentReferenceAnalyzer
{
    /// <summary>
    /// Represents a component reference found in source code
    /// </summary>
    public struct ComponentReference
    {
        public Type ComponentType;
        public string MethodName;
        public int LineNumber;
        public string SourceFile;
        public string ContextCode;
        
        public override string ToString()
        {
            return $"{MethodName}<{ComponentType?.Name}> in {Path.GetFileName(SourceFile)}:{LineNumber}";
        }
    }

    /// <summary>
    /// Analyze a MonoBehaviour type for component references
    /// </summary>
    public static List<ComponentReference> AnalyzeMonoBehaviourType(Type monoBehaviourType)
    {
        var sourceFile = FindSourceFileForType(monoBehaviourType);
        if (string.IsNullOrEmpty(sourceFile))
        {
            Debug.LogWarning($"Could not find source file for {monoBehaviourType.Name}");
            return new List<ComponentReference>();
        }

        try
        {
            var sourceCode = File.ReadAllText(sourceFile);
            return AnalyzeSourceCode(sourceCode, sourceFile);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error analyzing {monoBehaviourType.Name}: {e.Message}");
            return new List<ComponentReference>();
        }
    }

    /// <summary>
    /// Analyze multiple MonoBehaviour types
    /// </summary>
    public static Dictionary<Type, List<ComponentReference>> AnalyzeMonoBehaviourTypes(IEnumerable<Type> monoBehaviourTypes)
    {
        var results = new Dictionary<Type, List<ComponentReference>>();
        
        foreach (var type in monoBehaviourTypes)
        {
            var references = AnalyzeMonoBehaviourType(type);
            if (references.Count > 0)
            {
                results[type] = references;
            }
        }
        
        return results;
    }

    /// <summary>
    /// Find the source .cs file for a given Type
    /// </summary>
    public static string FindSourceFileForType(Type type)
    {
        if (type == null) return null;

#if UNITY_EDITOR
        // Try to find the script asset in Unity Editor
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

        // Fallback: search in common Unity script directories
        var possiblePaths = new[]
        {
            Path.Combine(Application.dataPath, "Scripts"),
            Application.dataPath,
            Path.Combine(Application.dataPath, "..") // Project root
        };

        foreach (var basePath in possiblePaths)
        {
            if (!Directory.Exists(basePath)) continue;
            
            var files = Directory.GetFiles(basePath, $"{type.Name}.cs", SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                return files[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Analyze C# source code for AddComponent and GetComponent calls
    /// </summary>
    public static List<ComponentReference> AnalyzeSourceCode(string sourceCode, string sourceFile)
    {
        var references = new List<ComponentReference>();
        
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot();

            // Find all method invocations
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            
            foreach (var invocation in invocations)
            {
                var componentRef = AnalyzeInvocation(invocation, syntaxTree, sourceFile);
                if (componentRef.HasValue)
                {
                    references.Add(componentRef.Value);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error parsing source code in {sourceFile}: {e.Message}");
        }

        return references;
    }

    /// <summary>
    /// Analyze a specific method invocation to check if it's a component reference
    /// </summary>
    private static ComponentReference? AnalyzeInvocation(InvocationExpressionSyntax invocation, SyntaxTree syntaxTree, string sourceFile)
    {
        string methodName = ExtractMethodName(invocation);
        if (string.IsNullOrEmpty(methodName)) return null;

        // Check if it's AddComponent or GetComponent call
        bool isAddComponent = methodName.StartsWith("AddComponent");
        bool isGetComponent = methodName.StartsWith("GetComponent") || methodName.StartsWith("TryGetComponent");

        if (!isAddComponent && !isGetComponent) return null;

        // Extract the component type
        var componentType = ExtractComponentType(invocation, methodName);
        if (componentType == null) return null;

        // Get line number and context
        var lineSpan = syntaxTree.GetLineSpan(invocation.Span);
        var lineNumber = lineSpan.StartLinePosition.Line + 1;
        var contextCode = ExtractLineOfCode(sourceFile, lineNumber);

        return new ComponentReference
        {
            ComponentType = componentType,
            MethodName = methodName,
            LineNumber = lineNumber,
            SourceFile = sourceFile,
            ContextCode = contextCode
        };
    }

    /// <summary>
    /// Extract method name from invocation expression
    /// </summary>
    private static string ExtractMethodName(InvocationExpressionSyntax invocation)
    {
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
                return memberAccess.Name.Identifier.ValueText;
            case IdentifierNameSyntax identifierName:
                return identifierName.Identifier.ValueText;
            default:
                return null;
        }
    }

    /// <summary>
    /// Extract component type from AddComponent/GetComponent invocation
    /// </summary>
    private static Type ExtractComponentType(InvocationExpressionSyntax invocation, string methodName)
    {
        // Handle generic method calls like AddComponent<Rigidbody>()
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name is GenericNameSyntax genericName)
        {
            var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
            if (typeArg != null)
            {
                var typeName = typeArg.ToString();
                return FindTypeByName(typeName);
            }
        }
        
        // Handle method calls with Type parameter like AddComponent(typeof(Rigidbody))
        if (invocation.ArgumentList.Arguments.Count > 0)
        {
            var firstArg = invocation.ArgumentList.Arguments[0].Expression;
            if (firstArg is TypeOfExpressionSyntax typeOfExpr)
            {
                var typeName = typeOfExpr.Type.ToString();
                return FindTypeByName(typeName);
            }
        }

        // Handle string-based component addition (legacy)
        if (invocation.ArgumentList.Arguments.Count > 0)
        {
            var firstArg = invocation.ArgumentList.Arguments[0].Expression;
            if (firstArg is LiteralExpressionSyntax literal && literal.Token.IsKind(SyntaxKind.StringLiteralToken))
            {
                var typeName = literal.Token.ValueText;
                return FindTypeByName(typeName);
            }
        }

        return null;
    }

    /// <summary>
    /// Find Type by name with fallbacks for common Unity types
    /// </summary>
    private static Type FindTypeByName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;

        // Clean up generic parameters if any
        typeName = typeName.Split('<')[0].Split('[')[0].Trim();

        // Try direct type resolution first
        var type = Type.GetType(typeName);
        if (type != null) return type;

        // Try with UnityEngine namespace
        type = Type.GetType($"UnityEngine.{typeName}");
        if (type != null) return type;

        // Try to find in all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                // Try exact match
                type = assembly.GetType(typeName);
                if (type != null) return type;
                
                // Try with UnityEngine namespace
                type = assembly.GetType($"UnityEngine.{typeName}");
                if (type != null) return type;

                // Try to find by simple name
                var typesByName = assembly.GetTypes()
                    .Where(t => t.Name == typeName || t.Name.EndsWith("." + typeName))
                    .ToArray();

                if (typesByName.Length == 1)
                    return typesByName[0];
            }
            catch (Exception)
            {
                // Some assemblies might not be accessible, skip them
                continue;
            }
        }

        Debug.LogWarning($"Could not resolve type: {typeName}");
        return null;
    }

    /// <summary>
    /// Extract a specific line of code from a source file
    /// </summary>
    private static string ExtractLineOfCode(string sourceFile, int lineNumber)
    {
        try
        {
            var lines = File.ReadAllLines(sourceFile);
            if (lineNumber > 0 && lineNumber <= lines.Length)
            {
                return lines[lineNumber - 1].Trim();
            }
        }
        catch (Exception)
        {
            // File might be locked or inaccessible
        }
        return "";
    }

    /// <summary>
    /// Get all component references in a project (Editor only)
    /// </summary>
#if UNITY_EDITOR
    public static Dictionary<Type, List<ComponentReference>> AnalyzeAllMonoBehavioursInProject()
    {
        var allMonoBehaviours = new List<Type>();
        
        // Find all MonoScript assets
        var scriptGuids = AssetDatabase.FindAssets("t:MonoScript");
        foreach (var guid in scriptGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            
            if (script && script.GetClass() != null && 
                typeof(MonoBehaviour).IsAssignableFrom(script.GetClass()))
            {
                allMonoBehaviours.Add(script.GetClass());
            }
        }

        Debug.Log($"Found {allMonoBehaviours.Count} MonoBehaviour scripts to analyze");
        return AnalyzeMonoBehaviourTypes(allMonoBehaviours);
    }
#endif

    /// <summary>
    /// Generate a report of all component references
    /// </summary>
    public static string GenerateAnalysisReport(Dictionary<Type, List<ComponentReference>> analysisResults)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("Component Reference Analysis Report");
        report.AppendLine("=====================================");
        report.AppendLine();

        var totalReferences = analysisResults.Values.SelectMany(refs => refs).Count();
        report.AppendLine($"Total MonoBehaviour types analyzed: {analysisResults.Count}");
        report.AppendLine($"Total component references found: {totalReferences}");
        report.AppendLine();

        foreach (var kvp in analysisResults.OrderBy(x => x.Key.Name))
        {
            var sourceType = kvp.Key;
            var references = kvp.Value;

            report.AppendLine($"Class: {sourceType.Name}");
            report.AppendLine($"  Found {references.Count} component reference(s):");

            foreach (var reference in references.OrderBy(r => r.LineNumber))
            {
                report.AppendLine($"    • {reference.MethodName}<{reference.ComponentType.Name}> at line {reference.LineNumber}");
                if (!string.IsNullOrEmpty(reference.ContextCode))
                {
                    report.AppendLine($"      Code: {reference.ContextCode}");
                }
            }
            report.AppendLine();
        }

        return report.ToString();
    }
}