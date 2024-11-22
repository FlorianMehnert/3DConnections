using UnityEngine;
using UnityEngine.UI;

namespace Runtime
{
    public class MainScript : MonoBehaviour
    {
        public ScriptVisualization visualizer;

        private void Start()
        {
            var classReferences = ClassParser.GetAllClassReferencesParallel("/home/florian/gamedev/my-own-platformer/Assets");
            visualizer.VisualizeDependencies(classReferences);
            GetComponent<Button>().onClick.AddListener(TriggerVisualization);
        }

        private void TriggerVisualization()
        {
            var classReferences = ClassParser.GetAllClassReferencesParallel("Assets/Scripts");
            visualizer.VisualizeDependencies(classReferences);
        }
    }
}