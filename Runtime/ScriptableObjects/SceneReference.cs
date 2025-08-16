namespace _3DConnections.Runtime.ScriptableObjects
{
    using System;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [CreateAssetMenu(fileName = "SceneReference", menuName = "3DConnections/ScriptableObjects/SceneReference",
        order = 1)]
    public class SceneReference : ScriptableObject
    {
        public bool useStaticValues = true;
        public string sceneName;
        public string scenePath;
        private Scene _scene;

        public SceneReference(Scene scene)
        {
            _scene = scene;
            sceneName = scene.name;
            scenePath = scene.path;
        }

        public Scene? scene
        {
            get => _scene.IsValid() ? _scene : TryResolveScene();
            set
            {
                if (value != null) _scene = (Scene)value;
                sceneName = _scene.name;
                scenePath = _scene.path;
            }
        }

        [CanBeNull]
        private Scene? TryResolveScene()
        {
            var resolvedScene = SceneManager.GetSceneByName(sceneName);
            bool found = false;
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                if (!SceneManager.GetSceneAt(i).IsValid() || SceneManager.GetSceneAt(i).name != sceneName) continue;
                resolvedScene = SceneManager.GetSceneAt(i);
                found = true;
                break;
            }

            if (!found || resolvedScene.IsValid()) return resolvedScene;
            Debug.Log("trying to resolve the following sceneName: " + sceneName);
            Debug.Log("path of invalid scene is: " + resolvedScene.path + " resolvedScene.name is: " +
                      resolvedScene.name);
            Debug.Log("could not resolve scene by name or path");
            return null;
        }

        /// <summary>
        /// considering UseStaticValues
        /// </summary>
        public string Name
        {
            get
            {
                if (scene != null) return useStaticValues ? sceneName : scene.HasValue ? scene.Value.name : sceneName;
                if (scene == null && sceneName != null) return sceneName;
                return "";
            }
        }

        /// <summary>
        /// considering UseStaticValues
        /// </summary>
        public string Path => useStaticValues ? scenePath : scene.HasValue ? scene.Value.path : scenePath;
    }
}