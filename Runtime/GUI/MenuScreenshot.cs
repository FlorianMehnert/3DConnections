namespace _3DConnections.Runtime.GUI
{
    using UnityEngine;

    /// <summary>
    /// Helper class to export the currently visible menu as a screenshot
    /// </summary>
    public class MenuCapture : MonoBehaviour
    {
        public RenderTexture renderTexture;

        public Camera uiCamera;

        [ContextMenu("Capture Menu")]
        private void CaptureMenu()
        {
            // render UI camera into RT
            uiCamera.targetTexture = renderTexture;
            uiCamera.Render();

            var tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            RenderTexture.active = renderTexture;
            tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            tex.Apply();

            // cleanup
            uiCamera.targetTexture = null;
            RenderTexture.active = null;

            var bytes = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes(Application.dataPath + "/../menu.png", bytes);
            Debug.Log("Saved menu.png");
        }
    }
}