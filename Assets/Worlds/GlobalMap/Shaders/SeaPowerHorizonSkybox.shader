Shader "GlobalMap/SeaPowerHorizonSkybox"
{
    Properties
    {
        _SkyTint ("Sky Zenith Color", Color) = (0.24, 0.45, 0.68, 1)
        _HorizonColor ("Horizon Mist Color", Color) = (0.64, 0.75, 0.84, 1)
        _GroundColor ("Distant Sea Color", Color) = (0.04, 0.16, 0.25, 1)
        _Exposure ("Exposure", Range(0.2, 2.0)) = 1.0
        _SunSize ("Sun Size", Range(0.005, 0.1)) = 0.025
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            half4 _SkyTint;
            half4 _HorizonColor;
            half4 _GroundColor;
            half _Exposure;
            half _SunSize;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.viewDir);
                float y = dir.y;

                half3 col;
                if (y >= 0.0)
                {
                    // Gradiente suave do horizonte nebuloso (y=0) ate o zenite (y=1), sem faixa esverdeada
                    float t = pow(saturate(y), 0.42);
                    col = lerp(_HorizonColor.rgb, _SkyTint.rgb, t);

                    // Disco solar suave
                    float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);
                    float sunDot = saturate(dot(dir, sunDir));
                    float sunDisc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.15, sunDot);
                    float sunGlow = pow(sunDot, 24.0) * 0.18;
                    col += _LightColor0.rgb * (sunDisc * 0.9 + sunGlow);
                }
                else
                {
                    // Abaixo do horizonte matematico (y < 0): funde suavemente da nevoa do horizonte
                    // para a cor profunda do mar, eliminando qualquer faixa preta/escura no far clip
                    float t = pow(saturate(-y * 8.0), 0.55);
                    col = lerp(_HorizonColor.rgb, _GroundColor.rgb, t);
                }

                col *= _Exposure;
                return half4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
