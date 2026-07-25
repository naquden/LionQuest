Shader "LionQuest/StylizedWater"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.34, 0.72, 0.76, 0.65)
        _DeepColor("Deep Color", Color) = (0.06, 0.24, 0.42, 0.95)
        _DepthFadeDistance("Depth Fade Distance", Float) = 4.0

        _FoamColor("Foam Color", Color) = (1, 1, 1, 1)
        _FoamDistance("Foam Distance", Float) = 0.6
        _FoamCutoff("Foam Cutoff", Range(0, 1)) = 0.55

        _RippleScale("Ripple Scale", Float) = 14.0
        _RippleSpeed("Ripple Speed", Float) = 0.22
        _RippleStrength("Ripple Strength", Range(0, 1)) = 0.35
        _RefractionStrength("Refraction Strength", Range(0, 0.2)) = 0.03
        _Smoothness("Smoothness", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVert
            #pragma fragment WaterFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _DepthFadeDistance;
                float4 _FoamColor;
                float _FoamDistance;
                float _FoamCutoff;
                float _RippleScale;
                float _RippleSpeed;
                float _RippleStrength;
                float _RefractionStrength;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
            };

            float WaterHash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float WaterNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = WaterHash(i);
                float b = WaterHash(i + float2(1, 0));
                float c = WaterHash(i + float2(0, 1));
                float d = WaterHash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Two counter-scrolling layers so ripples never read as a single sliding texture.
            float RippleField(float2 uv, float t)
            {
                float a = WaterNoise(uv + float2(t, t * 0.6));
                float b = WaterNoise(uv * 1.7 - float2(t * 0.8, t * 0.4));
                return a * 0.6 + b * 0.4;
            }

            Varyings WaterVert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.screenPos = ComputeScreenPos(pos.positionCS);
                OUT.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 WaterFrag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEye = IN.positionCS.w;
                float waterDepth = max(sceneEye - surfaceEye, 0.0);

                float t = _Time.y * _RippleSpeed;
                float ripple = RippleField(IN.positionWS.xz / max(_RippleScale, 0.0001), t);

                // Perturb the refraction lookup, scaled by depth so shallow edges stay stable.
                float2 offset = (ripple - 0.5) * _RefractionStrength * saturate(waterDepth);
                float3 refracted = SampleSceneColor(screenUV + offset);

                float depthT = saturate(waterDepth / max(_DepthFadeDistance, 0.0001));
                half4 water = lerp(_ShallowColor, _DeepColor, depthT);

                // Shore foam: a narrow band where the water is barely deeper than the bed.
                float shore = 1.0 - saturate(waterDepth / max(_FoamDistance, 0.0001));
                float foam = step(_FoamCutoff, shore * (0.75 + ripple * 0.5));

                float3 normalWS = normalize(IN.normalWS + float3((ripple - 0.5) * _RippleStrength, 0,
                                                                 (RippleField(IN.positionWS.zx / max(_RippleScale, 0.0001), t) - 0.5) * _RippleStrength));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float3 lit = water.rgb * (0.35 + 0.65 * ndotl * mainLight.shadowAttenuation) * mainLight.color;

                float3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(normalWS, halfDir)), lerp(16.0, 256.0, _Smoothness));
                lit += spec * mainLight.color * mainLight.shadowAttenuation;

                float3 blended = lerp(refracted, lit, water.a);
                blended = lerp(blended, _FoamColor.rgb, foam);

                float alpha = max(water.a, foam);
                blended = MixFog(blended, IN.fogCoord);

                return half4(blended, alpha);
            }
            ENDHLSL
        }
    }
}
