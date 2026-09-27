Shader "Hidden/Hegemonia/MapaMBaseMilitar"
{
    Properties
    {
        _MainTex ("Map UV", 2D) = "white" {}
        _AuthorityTex ("Geography Authority", 2D) = "white" {}
        _AppearanceTex ("Existing Relief Reference", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _AuthorityTex;
            sampler2D _AppearanceTex;
            float4 _AuthorityTex_TexelSize;
            float4 _AppearanceTex_TexelSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            bool IsLandColor(float3 color)
            {
                float maximum = max(color.r, max(color.g, color.b));
                float minimum = min(color.r, min(color.g, color.b));
                bool magentaBoundary = color.r > 150.0 / 255.0
                    && color.b > 95.0 / 255.0
                    && color.g < 185.0 / 255.0
                    && color.r > color.g + 25.0 / 255.0;
                bool blueOcean = color.r < 60.0 / 255.0
                    && color.g > 75.0 / 255.0
                    && color.b > 145.0 / 255.0
                    && color.b > color.g;
                bool neutralIsland = minimum > 90.0 / 255.0 && maximum - minimum <= 22.0 / 255.0;
                return maximum > 55.0 / 255.0
                    && (maximum - minimum > 22.0 / 255.0 || neutralIsland)
                    && !magentaBoundary
                    && !blueOcean;
            }

            float LandSignalAt(float2 uv)
            {
                return IsLandColor(tex2D(_AuthorityTex, uv).rgb) ? 1.0 : 0.0;
            }

            bool IsLandIncludingSourceCoast(float2 uv)
            {
                float3 color = tex2D(_AuthorityTex, uv).rgb;
                float maximum = max(color.r, max(color.g, color.b));
                if (IsLandColor(color)) return true;

                // GlobalWorldDefinition closes only the source's dark coastal stroke.
                // Match that one-pixel rule so the visual shoreline follows its land mask.
                if (maximum > 55.0 / 255.0) return false;
                float2 texel = _AuthorityTex_TexelSize.xy;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x == 0 && y == 0) continue;
                        if (IsLandColor(tex2D(_AuthorityTex, uv + float2(x, y) * texel).rgb)) return true;
                    }
                }
                return false;
            }

            float CoastNearby(float2 uv, float radius)
            {
                float2 offset = _AuthorityTex_TexelSize.xy * radius;
                float nearby = 0.0;
                nearby = max(nearby, LandSignalAt(uv + float2( offset.x, 0)));
                nearby = max(nearby, LandSignalAt(uv + float2(-offset.x, 0)));
                nearby = max(nearby, LandSignalAt(uv + float2(0,  offset.y)));
                nearby = max(nearby, LandSignalAt(uv + float2(0, -offset.y)));
                nearby = max(nearby, LandSignalAt(uv + float2( offset.x,  offset.y)));
                nearby = max(nearby, LandSignalAt(uv + float2(-offset.x,  offset.y)));
                nearby = max(nearby, LandSignalAt(uv + float2( offset.x, -offset.y)));
                nearby = max(nearby, LandSignalAt(uv + float2(-offset.x, -offset.y)));
                return nearby;
            }

            float Luminance(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;
                bool land = IsLandIncludingSourceCoast(uv);
                float3 color;

                if (land)
                {
                    float3 relief = tex2D(_AppearanceTex, uv).rgb;
                    float brightness = smoothstep(0.12, 0.74, Luminance(relief));
                    float greenWeight = saturate((relief.g - max(relief.r, relief.b)) * 3.2 + 0.12);
                    float sandWeight = saturate((relief.r - relief.g) * 2.4 + 0.08) * (1.0 - greenWeight);
                    float rockWeight = saturate(1.0 - greenWeight - sandWeight);

                    float3 olive = lerp(float3(0.14, 0.19, 0.14), float3(0.42, 0.43, 0.32), brightness);
                    float3 sand = lerp(float3(0.31, 0.27, 0.20), float3(0.66, 0.58, 0.44), brightness);
                    float3 stone = lerp(float3(0.28, 0.30, 0.29), float3(0.58, 0.56, 0.48), brightness);
                    float3 palette = olive * greenWeight + sand * sandWeight + stone * rockWeight;

                    float2 reliefStep = _AppearanceTex_TexelSize.xy * 1.5;
                    float left = Luminance(tex2D(_AppearanceTex, uv - float2(reliefStep.x, 0)).rgb);
                    float right = Luminance(tex2D(_AppearanceTex, uv + float2(reliefStep.x, 0)).rgb);
                    float down = Luminance(tex2D(_AppearanceTex, uv - float2(0, reliefStep.y)).rgb);
                    float up = Luminance(tex2D(_AppearanceTex, uv + float2(0, reliefStep.y)).rgb);
                    float hillshade = clamp(0.94 + (left - right) * 0.8 + (down - up) * 0.65, 0.72, 1.16);
                    color = palette * hillshade;
                }
                else
                {
                    float shallow = CoastNearby(uv, 3.0);
                    float shelf = CoastNearby(uv, 8.0);
                    float farShelf = CoastNearby(uv, 18.0);
                    float contourTexture = sin((uv.x * 37.0 + sin(uv.y * 19.0) * 0.8) * 6.28318) * 0.003;
                    color = float3(0.075, 0.13, 0.18) + contourTexture;
                    color += farShelf * float3(0.004, 0.018, 0.025);
                    color += shelf * float3(0.010, 0.045, 0.055);
                    color += shallow * float3(0.018, 0.092, 0.102);
                }

                // A restrained 6-by-6 chart graticule, independent of geography.
                float2 gridPosition = uv * 6.0;
                float2 gridDistance = min(frac(gridPosition), 1.0 - frac(gridPosition));
                float gridLine = 1.0 - smoothstep(0.003, 0.007, min(gridDistance.x, gridDistance.y));
                float3 gridColor = land ? float3(0.11, 0.14, 0.13) : float3(0.20, 0.27, 0.29);
                color = lerp(color, gridColor, gridLine * (land ? 0.13 : 0.19));

                return fixed4(color, 1.0) * input.color;
            }
            ENDCG
        }
    }
    Fallback Off
}
