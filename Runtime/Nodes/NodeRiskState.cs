using UnityEngine;
using TMPro;

namespace _3DConnections.Runtime.Nodes
{
    public enum NodeRiskState 
    { 
        Orphan,     // Green - safe to delete
        HardLinks,  // Amber - has references
        Subscriptions // Red - has event subscribers
    }

    public class NodeSafetyState : MonoBehaviour
    {
        public int InboundReferenceCount;
        public int InboundSubscriptionCount;
        public NodeRiskState RiskState = NodeRiskState.Orphan;
        
        [SerializeField] private GameObject badgeObject;
        [SerializeField] private SpriteRenderer badgeRenderer;
        [SerializeField] private TextMeshProUGUI badgeText;
        private ColoredObject glowComponent;
        
        private void Awake()
        {
            glowComponent = GetComponent<ColoredObject>();
            if (!glowComponent) glowComponent = gameObject.AddComponent<ColoredObject>();
            
            CreateBadge();
        }
        
        private void CreateBadge()
        {
            if (badgeText) return;
            
            var textObj = new GameObject("BadgeText");
            textObj.transform.SetParent(badgeObject.transform);
            textObj.transform.localPosition = Vector3.zero;
            badgeText = textObj.AddComponent<TextMeshProUGUI>();
            badgeText.text = "";
            badgeText.fontSize = 2;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = Color.white;
            
            badgeObject.SetActive(false);
        }
        
        public void UpdateVisual()
        {
            // Determine risk state
            if (InboundSubscriptionCount > 0)
                RiskState = NodeRiskState.Subscriptions;
            else if (InboundReferenceCount > 0)
                RiskState = NodeRiskState.HardLinks;
            else
                RiskState = NodeRiskState.Orphan;
            
            badgeObject.SetActive(true);
            badgeRenderer.color = RiskState switch
            {
                NodeRiskState.Orphan => Color.green,
                NodeRiskState.HardLinks => new Color(1f, 0.7f, 0f, 1f),
                NodeRiskState.Subscriptions => new Color(1f, 0.2f, 0.2f, 1f),
                _ => badgeRenderer.color
            };
        }
        
        public void OnBadgeClicked()
        {
            if (RiskState == NodeRiskState.HardLinks || RiskState == NodeRiskState.Subscriptions)
            {
                SafeDeleteService.Instance.HighlightInboundSources(gameObject);
            }
        }
    }
}
