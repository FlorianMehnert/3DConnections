namespace _3DConnections.Editor.NodeGraph
{
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor.Experimental.GraphView;
    using UnityEditor;
    using System.IO;

    public class AssetReferenceNode : Node
    {
        private Object m_Asset;
        private SceneGraphView m_GraphView;
        
        public Object Asset => m_Asset;
        public Port ReferenceInputPort { get; private set; }
        
        public AssetReferenceNode(Object asset, SceneGraphView graphView)
        {
            m_Asset = asset;
            m_GraphView = graphView;
            
            SetupNode();
            CreatePorts();
            CreateContent();
            
            RefreshExpandedState();
        }
        
        private void SetupNode()
        {
            title = GetAssetDisplayName();
            
            // Add appropriate CSS class based on asset type
            if (m_Asset is ScriptableObject)
                AddToClassList("scriptableobject-node");
            else if (m_Asset is Sprite)
                AddToClassList("sprite-node");
            else if (m_Asset is Material)
                AddToClassList("material-node");
            else if (m_Asset is Mesh)
                AddToClassList("mesh-node");
            else if (m_Asset is AudioClip)
                AddToClassList("audioclip-node");
            else if (m_Asset is Texture)
                AddToClassList("texture-node");
            else if (PrefabUtility.IsPartOfPrefabAsset(m_Asset))
                AddToClassList("prefab-node");
            else
                AddToClassList("asset-node");
        }
        
        private string GetAssetDisplayName()
        {
            if (m_Asset == null) return "Missing Asset";
            
            string assetPath = AssetDatabase.GetAssetPath(m_Asset);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            string typeName = m_Asset.GetType().Name;
            
            return $"{fileName} ({typeName})";
        }
        
        private void CreatePorts()
        {
            // Reference input port (for components that reference this asset)
            ReferenceInputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(Object));
            ReferenceInputPort.portName = "Referenced By";
            ReferenceInputPort.AddToClassList("asset-reference-port");
            inputContainer.Add(ReferenceInputPort);
        }
        
        private void CreateContent()
        {
            var contentContainer = new VisualElement();
            contentContainer.AddToClassList("asset-content-container");
            
            // Asset type icon
            var iconContainer = new VisualElement();
            iconContainer.AddToClassList("asset-icon-container");
            
            var icon = new VisualElement();
            icon.AddToClassList("asset-icon");
            icon.AddToClassList(GetAssetIconClass());
            iconContainer.Add(icon);
            
            contentContainer.Add(iconContainer);
            
            // Asset info
            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("asset-info-container");
            
            var typeLabel = new Label(m_Asset.GetType().Name);
            typeLabel.AddToClassList("asset-type-label");
            infoContainer.Add(typeLabel);
            
            if (m_Asset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(m_Asset);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var pathLabel = new Label(assetPath);
                    pathLabel.AddToClassList("asset-path-label");
                    infoContainer.Add(pathLabel);
                }
            }
            
            contentContainer.Add(infoContainer);
            
            // Preview (for supported asset types)
            CreateAssetPreview(contentContainer);
            
            extensionContainer.Add(contentContainer);
        }
        
        private string GetAssetIconClass()
        {
            if (m_Asset is ScriptableObject) return "scriptableobject-icon";
            if (m_Asset is Sprite) return "sprite-icon";
            if (m_Asset is Material) return "material-icon";
            if (m_Asset is Mesh) return "mesh-icon";
            if (m_Asset is AudioClip) return "audioclip-icon";
            if (m_Asset is Texture) return "texture-icon";
            if (PrefabUtility.IsPartOfPrefabAsset(m_Asset)) return "prefab-icon";
            return "asset-icon-default";
        }
        
        private void CreateAssetPreview(VisualElement container)
        {
            // Create preview for supported asset types
            if (m_Asset is Texture2D texture)
            {
                var preview = new Image();
                preview.image = texture;
                preview.AddToClassList("asset-preview");
                preview.style.maxWidth = 64;
                preview.style.maxHeight = 64;
                container.Add(preview);
            }
            else if (m_Asset is Sprite sprite)
            {
                var preview = new Image();
                preview.image = sprite.texture;
                preview.AddToClassList("asset-preview");
                preview.style.maxWidth = 64;
                preview.style.maxHeight = 64;
                container.Add(preview);
            }
        }
        
        public override void OnSelected()
        {
            base.OnSelected();
            Selection.activeObject = m_Asset;
            EditorGUIUtility.PingObject(m_Asset);
        }
    }
}
