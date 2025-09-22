namespace _3DConnections.Runtime.GUI._3DConnections.Runtime.GUI
{
    using UnityEngine;

    public class NodegraphScreenshot : MonoBehaviour
    {
        public Camera orthoCamera;
        public int width = 4096;
        public int height = 4096;

        [ContextMenu("Capture Transparent")]
        private void Capture()
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8
            };
            orthoCamera.targetTexture = rt;

            orthoCamera.clearFlags = CameraClearFlags.SolidColor;
            orthoCamera.backgroundColor = new Color(0, 0, 0, 0);

            orthoCamera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            orthoCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);

            var bytes = tex.EncodeToPNG();
            var path = Application.dataPath + "/../nodegraph_transparent.png";
            System.IO.File.WriteAllBytes(path, bytes);

            Debug.Log("Saved capture with transparency to " + path);
        }
    }

}