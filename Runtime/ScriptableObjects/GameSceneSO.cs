namespace _3DConnections.Runtime.ScriptableObjects
{
    using UnityEngine;

    /// <summary>
    /// This class is a base class which contains what is common to all game scenes (Locations or Menus)
    /// See Unity open-project-1
    /// <see href="https://github.com/UnityTechnologies/open-project-1/blob/devlogs/2-scriptable-objects/UOP1_Project/Assets/Scripts/SceneManagement/ScriptableObjects/GameSceneSO.cs"/>
    /// </summary>

    // ReSharper disable once InconsistentNaming
    public class GameSceneSO : ScriptableObject
    {
        [Header("Information")]
        public string sceneName;
        public string shortDescription;
    }
}