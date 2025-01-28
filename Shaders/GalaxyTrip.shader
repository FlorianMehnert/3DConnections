Shader "Custom/GalaxyTrip"
{
//  This code is taken from https://www.shadertoy.com/view/lslSDS and converted to a unity shader
    Properties
    {
        _Brightness ("Brightness", Float) = 2.5
        _ParticleSize ("Particle Size", Float) = 0.015
        _ParticleLength ("Particle Length", Float) = 0.0083
        _MinDist ("Min Distance", Float) = 0.8
        _MaxDist ("Max Distance", Float) = 5.0
        _RepeatMin ("Repeat Min", Float) = 1.0
        _RepeatMax ("Repeat Max", Float) = 2.0
        _DepthFade ("Depth Fade", Float) = 0.8
        _Steps ("Steps", Float) = 121.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define PASS_COUNT 4

            float _Brightness;
            float _ParticleSize;
            float _ParticleLength;
            float _MinDist;
            float _MaxDist;
            float _RepeatMin;
            float _RepeatMax;
            float _DepthFade;
            float _Steps;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 rayDir : TEXCOORD1;
            };

            float Random(float x)
            {
                return frac(sin(x * 123.456) * 23.4567 + sin(x * 345.678) * 45.6789 + sin(x * 456.789) * 56.789);
            }

            float3 GetParticleColour(float3 vParticlePos, float fParticleSize, float3 vRayDir)
            {
                float2 vNormDir = normalize(vRayDir.xy);
                float d1 = dot(vParticlePos.xy, vNormDir.xy) / length(vRayDir.xy);
                float3 vClosest2d = vRayDir * d1;

                float3 vClampedPos = vParticlePos;
                vClampedPos.z = clamp(vClosest2d.z, vParticlePos.z - _ParticleLength, vParticlePos.z + _ParticleLength);

                float d = dot(vClampedPos, vRayDir);
                float3 vClosestPos = vRayDir * d;
                float3 vDeltaPos = vClampedPos - vClosestPos;

                float fClosestDist = length(vDeltaPos) / fParticleSize;
                float fShade = clamp(1.0 - fClosestDist, 0.0, 1.0);

                if (d < 3.0)
                {
                    fClosestDist = max(abs(vDeltaPos.x), abs(vDeltaPos.y)) / fParticleSize;
                    float f = clamp(1.0 - 0.8 * fClosestDist, 0.0, 1.0);
                    fShade += f * f * f * f;
                    fShade *= fShade;
                }

                fShade = fShade * exp2(-d * _DepthFade) * _Brightness;
                return float3(fShade, fShade, fShade);
            }

            float3 GetParticlePos(float3 vRayDir, float fZPos, float fSeed)
            {
                float fAngle = atan2(vRayDir.x, vRayDir.y);
                float fAngleFraction = frac(fAngle / (3.14 * 2.0));

                float fSegment = floor(fAngleFraction * _Steps + fSeed) + 0.5 - fSeed;
                float fParticleAngle = fSegment / _Steps * (3.14 * 2.0);

                float fSegmentPos = fSegment / _Steps;
                float fRadius = _MinDist + Random(fSegmentPos + fSeed) * (_MaxDist - _MinDist);

                float tunnelZ = vRayDir.z / length(vRayDir.xy / fRadius);
                tunnelZ += fZPos;

                float fRepeat = _RepeatMin + Random(fSegmentPos + 0.1 + fSeed) * (_RepeatMax - _RepeatMin);
                float fParticleZ = (ceil(tunnelZ / fRepeat) - 0.5) * fRepeat - fZPos;

                return float3(sin(fParticleAngle) * fRadius, cos(fParticleAngle) * fRadius, fParticleZ);
            }

            float3 Starfield(float3 vRayDir, float fZPos, float fSeed)
            {
                float3 vParticlePos = GetParticlePos(vRayDir, fZPos, fSeed);
                return GetParticleColour(vParticlePos, _ParticleSize, vRayDir);
            }

            float3 RotateX(float3 vPos, float fAngle)
            {
                float s = sin(fAngle);
                float c = cos(fAngle);
                return float3(vPos.x, c * vPos.y + s * vPos.z, -s * vPos.y + c * vPos.z);
            }

            float3 RotateY(float3 vPos, float fAngle)
            {
                float s = sin(fAngle);
                float c = cos(fAngle);
                return float3(c * vPos.x + s * vPos.z, vPos.y, -s * vPos.x + c * vPos.z);
            }

            float3 RotateZ(float3 vPos, float fAngle)
            {
                float s = sin(fAngle);
                float c = cos(fAngle);
                return float3(c * vPos.x + s * vPos.y, -s * vPos.x + c * vPos.y, vPos.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.rayDir = normalize(worldPos - _WorldSpaceCameraPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 q = 2.0 * uv - 1.0;
                q.y *= _ScreenParams.y / _ScreenParams.x;

                float3 rd = normalize(i.rayDir);
                float3 euler = float3(
                    sin(_Time.y * 0.2) * 0.625,
                    cos(_Time.y * 0.1) * 0.625,
                    _Time.y * 0.1 + sin(_Time.y * 0.3) * 0.5);

                rd = RotateX(rd, euler.x);
                rd = RotateY(rd, euler.y);
                rd = RotateZ(rd, euler.z);

                float fZPos = 5.0;
                float fSeed = 0.0;
                float3 vResult = float3(0,0,0);

                float3 red = float3(0.7, 0.4, 0.3);
                float3 blue = float3(0.3, 0.4, 0.7);
                float3 tint = float3(0,0,0);
                float ti = 1.0 / float(PASS_COUNT - 1);
                float t = 0.0;

                for (int i = 0; i < PASS_COUNT; i++)
                {
                    tint = lerp(red, blue, t);
                    vResult += 1.1 * tint * Starfield(rd, fZPos, fSeed);
                    t += ti;
                    fSeed += 1.234;
                    rd = RotateX(rd, 0.25 * euler.x);
                }

                float3 col = sqrt(vResult);

                float2 r = -1.0 + 2.0 * (uv);
                float vb = max(abs(r.x), abs(r.y));
                col *= (0.15 + 0.85 * (1.0 - exp(-(1.0 - vb) * 30.0)));

                return float4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}