using _3DConnections.Runtime;
using _3DConnections.Runtime.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace _3DConnections.Editor
{
    public class TreeMenu
    {
        [Header("Output")] private static string _assetPath = "Assets/TreeData.asset"; // Where the tree will be saved
        
        /// <summary>
        /// Saves the TreeData ScriptableObject to a file.
        /// </summary>
        /// <param name="treeData">The tree data to save</param>
        private static void SaveTreeDataAsset(TreeDataSO treeData)
        {
            if (!_assetPath.EndsWith(".asset"))
                _assetPath += ".asset";

            // Save the tree data as an asset in the project
            AssetDatabase.CreateAsset(treeData, _assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"TreeData saved at {_assetPath}");
        }
        
    }

}