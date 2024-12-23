using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _3DConnections.Runtime.ScriptableObjects
{
    [CreateAssetMenu(fileName = "SceneReference", menuName = "3DConnections/ScriptableObjects/SceneReference", order = 1)]
    public class SceneReference : ScriptableObject
    {
        public bool useStaticValues = true;
        public string sceneName;
        public string scenePath; 
        private Scene _scene;

        public Scene scene
        {
            get => _scene.IsValid() ? _scene : SceneManager.GetSceneByPath(scenePath);
            set => _scene = value;
        }

        public string Name => useStaticValues ? sceneName : scene.name;
        public string Path => useStaticValues ? scenePath : scene.path;
    }
}