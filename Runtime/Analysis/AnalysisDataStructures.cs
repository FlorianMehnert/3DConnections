using System;
using System.Collections.Generic;

namespace _3DConnections.Runtime.Analysis
{
    public struct ComponentReference
    {
        public Type ReferencedComponentType;
        public string MethodName;
        public int LineNumber;
        public string SourceFile;
    }

    public struct EventInfo
    {
        public string EventName;
        public string FieldName;
        public EventType Type;
        public bool HasInvokeMethod;
        public string InvokeMethodName;
    }

    public struct EventInvocation
    {
        public Type InvokerType;
        public string EventName;
        public string MethodPattern;
        public Type TargetType;
        public int LineNumber;
        public string SourceFile;
        public bool IsIndirect;
        public string AccessPath;
    }

    public struct EventSubscription
    {
        public Type SubscriberType;
        public string EventFieldName;
        public Type PublisherType;
        public string AccessPattern;
        public SubscriptionType Type;
        public string IntermediateField;
    }

    public enum EventType
    {
        UnityAction,
        Action,
        UnityEvent,
        CustomDelegate,
        SystemEventHandler,
        FuncDelegate
    }

    public enum SubscriptionType
    {
        DirectSubscription,
        InstanceAccess,
        ConditionalAccess,
        MethodInvocation
    }

    public class AnalysisContext
    {
        public HashSet<UnityEngine.Object> VisitedObjects { get; } = new();
        public HashSet<UnityEngine.Object> ProcessingObjects { get; } = new();
        public HashSet<Type> DiscoveredMonoBehaviours { get; } = new();
        public int CurrentNodeCount { get; set; }
        public int MaxNodes { get; set; } = 1000;
    }
}