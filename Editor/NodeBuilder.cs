using UnityEngine;
using UnityEngine.SceneManagement;

namespace com.florian_mehnert._3d_connections.Editor
{
    public class NodeBuilder : MonoBehaviour
    {
        private static void AddNodeAtGameObjectPosition(GameObject referenceObject, Scene scene)
        {
            SceneHandler.GetOverlayedScene();

            var targetPosition = referenceObject.transform.position;

            var newNode = new GameObject("NewNode")
            {
                transform = { position = targetPosition }
            };

            // Move the new node to the second scene
            SceneManager.MoveGameObjectToScene(newNode, scene);
        }

        public static void Execute(Scene scene, int x = 20, int y = 60)
        {
            if (!GUI.Button(new Rect(x, y, 150, 30), "Other Scene Additive")) return;
            //AddNodeAtGameObjectPosition();
        }
    }
}