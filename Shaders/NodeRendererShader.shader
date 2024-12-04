Shader "Custom/NodeRenderer" {
    Properties {
        _PointSize ("Point Size", Range(0.01, 1)) = 0.1
    }
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct NodeData {
                float3 position;
                float3 velocity;
                float4 color;
                float size;
                int nodeType;
                int customDataIndex;
            };

            StructuredBuffer<NodeData> NodeBuffer;
            float _PointSize;

            struct v2f {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float size : PSIZE;
            };

            v2f vert(uint id : SV_VertexID) {
                v2f o;
                NodeData node = NodeBuffer[id];
                o.pos = UnityObjectToClipPos(float4(node.position, 1.0));
                o.color = node.color;
                o.size = _PointSize * node.size;
                return o;
            }

            float4 frag(v2f i) : SV_Target {
                return i.color;
            }
            ENDCG
        }
    }
}