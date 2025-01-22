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
            get => _scene.IsValid() ? _scene : TryResolveScene();
            set => _scene = value;
        }

        private Scene TryResolveScene()
        {
            var resolvedScene = SceneManager.GetSceneByPath(scenePath);
            if (!resolvedScene.IsValid())
            {
                resolvedScene = SceneManager.GetSceneByName(sceneName);
            }
            if (!resolvedScene.IsValid())
            {
                Debug.Log("could not resolve scene by name or path");
            }

            return resolvedScene;
        }

        /// <summary>
        /// considering UseStaticValues
        /// </summary>
        public string Name => useStaticValues ? sceneName : scene.name;
        
        /// <summary>
        /// considering UseStaticValues
        /// </summary>
        public string Path => useStaticValues ? scenePath : scene.path;
    }
}