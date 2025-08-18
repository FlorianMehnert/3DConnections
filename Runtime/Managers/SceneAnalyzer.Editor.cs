namespace _3DConnections.Runtime.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    
    using Utils;
    using cols = ScriptableObjects.NodeColorsScriptableObject;
    
    public partial class SceneAnalyzer : MonoBehaviour
    {
        /// <summary>
        /// Used in the CustomEditor. Types that will be ignored when traversing the scene. For example, Transform could be ignored,
        /// resulting in a cleaner graph.
        /// </summary>
        /// <returns>List of ignored component types</returns>
        private List<Type> GetIgnoredTypes()
        {
            return ignoredTypes.Select(Type.GetType).Where(type => type != null).ToList();
        }

        private void OnValidate()
        {
            var palette = Colorpalette.GeneratePaletteFromBaseColor(cols.GameObjectColor, cols.ColorPreset, cols.GenerateColors);
            cols.GameObjectColor = palette[0];
            cols.ComponentColor = palette[1];
            cols.ScriptableObjectColor = palette[2];
            cols.AssetColor = palette[3];
            cols.ParentChildConnection = palette[4];
            cols.ComponentConnection = palette[5];
            cols.ReferenceConnection = palette[6];
        }
    }
}