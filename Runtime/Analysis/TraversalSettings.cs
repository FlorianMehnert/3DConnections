using System;
using System.Collections.Generic;

namespace _3DConnections.Runtime.Analysis
{
    [Serializable]
    public class TraversalSettings
    {
        public int  MaxNodes        = 1000;
        public bool SpawnRootNode   = true;
        public bool IgnoreTransforms = false;
        public List<Type> IgnoredTypes = new();

        public SceneTraversalMode Mode = SceneTraversalMode.Hierarchy; 
    }

}