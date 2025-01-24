using System.Linq;
using UnityEditor;
using UnityEngine;

public class AddLayerScript : Editor
{
    [MenuItem("Tools/Add OverlayScene Layer")]
    public static void AddPredefinedLayer()
    {
        AddLayer("OverlayScene");
    }

    private static void AddLayer(string layerName)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layersProp = tagManager.FindProperty("layers");
        var layers = Enumerable.Range(0, 32).Select(LayerMask.LayerToName).Where(l => !string.IsNullOrEmpty(l)).ToArray();
        if (layers.Any(layer => layer == layerName))
        {
            Debug.Log("Layer already exists: " + layerName);
            return;
        }

        for (var i = 16; i < layersProp.arraySize; i++)
        {
            var layer = layersProp.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue)) continue;
            layer.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            Debug.Log("Layer added: " + layerName);
            return;
        }

        Debug.LogWarning("No available layer slots to add: " + layerName);
    }
}