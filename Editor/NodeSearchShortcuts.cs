using UnityEditor;
using UnityEditor.Search;

namespace _3DConnections.Editor
{
    public static class NodeSearchShortcuts
    {
        // Useless because currently hasref only checks for node is connected to gameobject
        // [MenuItem("Tools/3DConnections/Search/Highlight GameObjects")]
        // public static void HighlightActiveGameObjects()
        // {
        //     SearchService.ShowWindow(SearchService.CreateContext("node: highlight{nodetype:GameObject}"));
        // }
    
        /// <summary>
        /// fuzzy search for edgetypes
        /// </summary>
        [MenuItem("Tools/3DConnections/Search/Highlight Edges of type Event")]
        public static void FocusColoredObjects()
        {
            SearchService.ShowWindow(SearchService.CreateContext("node: highlight{edges: edgetype:virtu}}"));
        }
        
        /// <summary>
        /// find all in/outgoing references for one object
        /// </summary>
        [MenuItem("Tools/3DConnections/Search/Highlight objects that have an outgoing connection to Manager")]
        public static void FocusOutgoingToManager()
        {
            SearchService.ShowWindow(SearchService.CreateContext("node: highlight{in:GameManager}"));
        }
        
        // Useless because empty cannot be expressed
        // [MenuItem("Tools/3DConnections/Search/Find disconnected nodes")]
        // public static void FindDisconnectedNodes()
        // {
        //     SearchService.ShowWindow(SearchService.CreateContext("node: highlight{in: out:}"));
        // }
        
        /// <summary>
        /// find all elements of one specific layer
        /// </summary>
        [MenuItem("Tools/3DConnections/Search/Select All UI Elements")]
        public static void SelectAllUIElements()
        {
            SearchService.ShowWindow(SearchService.CreateContext("node: select{layer:UI}"));
        }
        
        /// <summary>
        /// Search using properties
        /// </summary>
        [MenuItem("Tools/3DConnections/Search/Property search for nodeType Component")]
        public static void PropertySearchForNodeTypeComponent()
        {
            SearchService.ShowWindow(SearchService.CreateContext("node: highlight{#NodeType.nodeTypeName:\"Component\"}"));
        }
    }
}