using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace _3DConnections.Runtime
{
    public class SceneSerializer : MonoBehaviour
    {
        public static List<GameObject> SerializeSceneHierarchy(Scene? scene)
        {
            var allObjects = new List<GameObject>();
            scene ??= SceneManager.GetActiveScene();

            // Get all root objects in the scene
            GameObject[] rootObjects = ((Scene) scene).GetRootGameObjects();

            // Add root objects and traverse their children
            foreach (GameObject root in rootObjects)
            {
                // Add the root object itself
                allObjects.Add(root);

                // Get all children using recursive function
                TraverseHierarchy(root.transform, allObjects);
            }

            return allObjects;
        }

        private static void  TraverseHierarchy(Transform parent, List<GameObject> objectList)
        {
            // Iterate through each child of the parent
            foreach (Transform child in parent)
            {
                // Add the child gameObject to our list
                objectList.Add(child.gameObject);

                // Recursively traverse this child's children
                if (child.childCount > 0)
                {
                    TraverseHierarchy(child, objectList);
                }
            }
        }

        // Optional: Get a formatted string representation of the hierarchy
        public string GetHierarchyString(Scene? scene)
        {
            List<GameObject> objects = SerializeSceneHierarchy(scene);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (GameObject obj in objects)
            {
                // Calculate the depth by counting parents
                int depth = 0;
                Transform parent = obj.transform.parent;
                while (parent != null)
                {
                    depth++;
                    parent = parent.parent;
                }

                // Add indentation based on depth
                sb.AppendLine(new string('-', depth) + obj.name);
            }

            return sb.ToString();
        }
    }
}