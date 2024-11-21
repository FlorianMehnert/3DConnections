using System.Collections.Generic;
using UnityEngine;

namespace _3DConnections
{
    public class ScriptVisualization : MonoBehaviour
    {
        public GameObject cubePrefab;

        public void VisualizeDependencies(Dictionary<string, ClassReferences> classReferences)
        {
            foreach (var (scriptName, references) in classReferences)
            {
                var scriptNode = Instantiate(cubePrefab, transform);
                scriptNode.name = scriptName;
                scriptNode.transform.position = new Vector3(references.InheritanceReferences.Count * 2, references.FieldReferences.Count * 2, references.MethodReferences.Count * 2);

                // Draw connections between script nodes based on dependency information
                foreach (var inheritanceRef in references.InheritanceReferences)
                {
                    DrawConnection(scriptName, inheritanceRef);
                }
                foreach (var fieldRef in references.FieldReferences)
                {
                    DrawConnection(scriptName, fieldRef);
                }
                foreach (var methodRef in references.MethodReferences)
                {
                    DrawConnection(scriptName, methodRef);
                }
            }
        }

        private void DrawConnection(string from, string to)
        {
            var fromNode = transform.Find(from);
            var toNode = transform.Find(to);
            if (fromNode != null && toNode != null)
            {
                Debug.DrawLine(fromNode.position, toNode.position, Color.white, 10f);
            }
        }
    }
}