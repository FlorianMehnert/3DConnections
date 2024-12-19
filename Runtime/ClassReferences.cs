using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace _3DConnections.Runtime
{
    /// <summary>
    /// Dataclass to keep track of different kinds of references within the project
    /// </summary>
    public class ClassReferences 
    {
        public List<string> InheritanceReferences { [UsedImplicitly] get; set; }
        public List<string> FieldReferences { [UsedImplicitly] get; set; }
        public List<string> MethodReferences { [UsedImplicitly] get; set; }

        public List<string> References => InheritanceReferences.Union(FieldReferences).Union(MethodReferences).ToList();
    }
}