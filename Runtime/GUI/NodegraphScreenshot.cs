namespace _3DConnections.Runtime.GUI._3DConnections.Runtime.GUI
{
    using UnityEngine;

    public class NodegraphScreenshot : MonoBehaviour
    {
        public Camera orthoCamera;
        public int width = 1024;
        public int height = 1024;

        [ContextMenu("Capture Transparent")]
        void Capture()
        {
            // Create RT with alpha support
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();

            // Setup camera
            orthoCamera.targetTexture = rt;
            orthoCamera.clearFlags = CameraClearFlags.SolidColor;
            orthoCamera.backgroundColor = new Color(0, 0, 0, 0); // transparent!

            // Render into RT
            orthoCamera.Render();

            // Copy to Texture2D
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // Reset
            orthoCamera.targetTexture = null;
            RenderTexture.active = null;
            rt.Release();

            // Save to disk
            byte[] bytes = tex.EncodeToPNG();
            string path = Application.dataPath + "/capture.png";
            System.IO.File.WriteAllBytes(path, bytes);
            Debug.Log("Saved capture with transparency to " + path);
        }
    }

}