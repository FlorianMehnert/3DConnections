namespace _3DConnections.Runtime.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    
    using Utils;

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
            var palette = Colorpalette.GeneratePaletteFromBaseColor(gameObjectColor, colorPreset, generateColors);
            gameObjectColor = palette[0];
            componentColor = palette[1];
            scriptableObjectColor = palette[2];
            assetColor = palette[3];
            parentChildConnection = palette[4];
            componentConnection = palette[5];
            referenceConnection = palette[6];
        }
    }
}