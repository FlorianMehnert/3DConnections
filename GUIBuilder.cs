using UnityEngine;

namespace _3DConnections
{
    public class GUIBuilder : MonoBehaviour
    {
        public void OnGui()
        {
            LoadSceneAdditive.Execute(20,30);
        }
    }
}