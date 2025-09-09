using System;
using System.Collections.Generic;
using _3DConnections.Runtime.Managers;
using UnityEngine;

namespace _3DConnections.Runtime.Analysis
{
    public interface ISceneAnalysisService
    {
        void AnalyzeScene(Action onComplete = null);
        void ClearAnalysis();
        IReadOnlyList<GameObject> GetAllNodes();
    }

    public interface INodeGraphManager
    {
        GameObject CreateNode(UnityEngine.Object obj, int depth, GameObject parent = null, bool isAsset = false, Type virtualType = null);
        GameObject FindNodeByType(Type componentType);
        void ClearNodes();
        IReadOnlyDictionary<int, GameObject> NodeLookup { get; }
    }

    public interface IComponentReferenceAnalyzer
    {
        List<ComponentReference> AnalyzeComponentReferences(Type monoBehaviourType);
        void CreateDynamicConnections(INodeGraphManager nodeManager, AnalysisFilterSettings filterSettings);
    }

    public interface IEventAnalyzer
    {
        void AnalyzeEvents(IEnumerable<Type> monoBehaviourTypes);
        void CreateEventConnections(INodeGraphManager nodeManager, AnalysisFilterSettings filterSettings);
    }

    public interface IFileLocator
    {
        string FindSourceFileForType(Type type);
    }

    public interface ITypeResolver
    {
        Type FindTypeByName(string typeName);
    }

    public interface ILogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }
}