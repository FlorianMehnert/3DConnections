using System.Linq;
using UnityEditor;
using UnityEngine;

public class AddLayerScript : Editor
{
    [MenuItem("Tools/Add OverlayScene Layer")]
    public static void AddPredefinedLayer()
    {
        // Call the method to add the predefined layer
        AddLayer("OverlayScene");
    }

    public static void AddLayer(string layerName)
    {
        // Check if the layer already exists
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layersProp = tagManager.FindProperty("layers");
        string[] layers = Enumerable.Range(0, 32).Select(index => LayerMask.LayerToName(index)).Where(l => !string.IsNullOrEmpty(l)).ToArray();
        // Check if the layer already exists
        foreach (var layer in layers) // Layers start from index 8
        {
            if (layer != layerName) continue;
            Debug.Log("Layer already exists: " + layerName);
            return;
        }

        // Add new layer if there's an empty slot
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