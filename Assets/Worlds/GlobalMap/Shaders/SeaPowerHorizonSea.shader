Shader "GlobalMap/SeaPowerHorizonSea"
{
    Properties
    {
        _BaseColor ("Deep Sea Color", Color) = (0.026, 0.134, 0.198, 1)
        _ReflectionColor ("Reflection Color", Color) = (0.38, 0.48, 0.56, 1)
        _HorizonColor ("Horizon Mist Color", Color) = (0.64, 0.75, 0.84, 1)
        _FogStart ("Fog Start", Float) = 18000
        _FogEnd ("Fog End", Float) = 160000
    }
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        Cull Back ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _BaseColor;
            half4 _ReflectionColor;
            half4 _HorizonColor;
            float _FogStart;
            float _FogEnd;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = wp.xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 eyeVec = i.worldPos - _WorldSpaceCameraPos;
                float dist = length(eyeVec);
                float3 eye = eyeVec / max(1.0, dist);

                // Mesma resposta angular de fresnel calmo que o OceanAdvanced a distancia
                float fresnel = saturate(1.0 - max(0.0, -eye.y));
                fresnel = pow(fresnel, 2.4) * 0.52;

                half3 seaCol = lerp(_BaseColor.rgb, _ReflectionColor.rgb, fresnel) * 0.82;

                // Nevoa por pixel (nunca por vertice!) para fundir 100% com o ceu no horizonte
                float fogFactor = saturate((dist - _FogStart) / max(1000.0, _FogEnd - _FogStart));
                fogFactor = smoothstep(0.0, 1.0, fogFactor);

                half3 finalCol = lerp(seaCol, _HorizonColor.rgb, fogFactor);
                return half4(finalCol, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
