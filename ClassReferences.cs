using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace _3DConnections
{
    public class ClassReferences
    {
        public List<string> InheritanceReferences { [UsedImplicitly] get; set; }
        public List<string> FieldReferences { [UsedImplicitly] get; set; }
        public List<string> MethodReferences { [UsedImplicitly] get; set; }

        public List<string> References => InheritanceReferences.Union(FieldReferences).Union(MethodReferences).ToList();
    }
}