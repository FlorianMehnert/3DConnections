using UnityEditor;
using UnityEngine;
using WebSocketSharp.Server;
using System.Linq;
using System.Collections.Generic;
using System;

[InitializeOnLoad]
public static class RuntimeDependencyStreamer
{
    private static WebSocketServer wssv;
    private static double lastSendTime;
    private static int port = 5678;

    static RuntimeDependencyStreamer()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private static void OnEditorUpdate()
    {
        if (wssv == null)
        {
            wssv = new WebSocketServer(port);
            wssv.AddWebSocketService<DependencyService>("/dependencies");
            wssv.Start();
            Debug.Log($"[DependencyStreamer] WebSocket server started on ws://localhost:{port}/dependencies");
        }

        if (EditorApplication.timeSinceStartup - lastSendTime >= 1.0)
        {
            var dependencies = GatherDependencies();
            var json = JsonUtility.ToJson(new DependencyPacket { objects = dependencies });

            var serviceHost = wssv.WebSocketServices["/dependencies"];
            if (serviceHost != null) serviceHost.Sessions.Broadcast(json);

            lastSendTime = EditorApplication.timeSinceStartup;
        }
    }

    private static List<SceneObjectInfo> GatherDependencies()
    {
        var list = new List<SceneObjectInfo>();

        foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
        {
            if (!go.scene.IsValid() || go.hideFlags != HideFlags.None) continue;

            var compTypes = go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetType().Name)
                .ToList();

            list.Add(new SceneObjectInfo
            {
                name = go.name,
                components = compTypes
            });
        }

        return list;
    }

    [Serializable]
    private class SceneObjectInfo
    {
        public string name;
        public List<string> components;
    }

    [Serializable]
    private class DependencyPacket
    {
        public List<SceneObjectInfo> objects;
    }

    private class DependencyService : WebSocketBehavior
    {
        protected override void OnMessage(WebSocketSharp.MessageEventArgs e)
        {
            // No-op: we only send data
        }
    }
}