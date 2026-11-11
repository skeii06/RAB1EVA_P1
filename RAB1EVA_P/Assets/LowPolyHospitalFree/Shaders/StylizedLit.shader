// The line's look in URP and Built-in: the texture times the baked occlusion in the vertex colours, lit by the
// scene's lights with a soft wrap (shadows, fog), and the glow map's texels lighting themselves.
// URP's SubShader is skipped where URP isn't installed (PackageRequirements), so a Built-in project compiles the second.
Shader "LowPolyHospitalFree/Stylized Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _GlowTex ("Glow Map", 2D) = "black" {}
        _Glow ("Glow Strength", Range(0, 4)) = 1.5
        _Wrap ("Light Wrap", Range(0, 1)) = 0.3
        _Occlusion ("Baked Occlusion", Range(0, 1)) = 1
    }

    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal": "14.0" }
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        half4 _Color;
        half _Glow, _Wrap, _Occlusion;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }  // drawn on Forward, Forward+ and Deferred alike; a "UniversalForward" pass without a GBuffer one draws nothing on Deferred
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_GlowTex); SAMPLER(sampler_GlowTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 color : COLOR;
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = half4(SRGBToLinear(v.color.rgb), v.color.a); // the occlusion was baked in sRGB
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 Wrapped(Light l, half3 n)
            {
                half d = saturate((dot(n, l.direction) + _Wrap) / (1 + _Wrap));
                return l.color * d * l.distanceAttenuation * l.shadowAttenuation;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Color.rgb;
                half occlusion = lerp(1, i.color.r, _Occlusion);
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);
                half3 light = SampleSH(n) + Wrapped(GetMainLight(TransformWorldToShadowCoord(i.positionWS), i.positionWS, shadowMask), n);
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint count = GetAdditionalLightsCount();
                #if defined(_CLUSTER_LIGHT_LOOP)
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    light += Wrapped(GetAdditionalLight(lightIndex, i.positionWS, shadowMask), n);
                #endif
                LIGHT_LOOP_BEGIN(count)
                    light += Wrapped(GetAdditionalLight(lightIndex, i.positionWS, shadowMask), n);
                LIGHT_LOOP_END
                #endif
                half3 c = albedo * occlusion * light + SAMPLE_TEXTURE2D(_GlowTex, sampler_GlowTex, i.uv).rgb * _Glow;
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 dir = normalize(_LightPosition - p);
                #else
                float3 dir = _LightDirection;
                #endif
                float4 c = TransformWorldToHClip(ApplyShadowBias(p, n, dir));
                #if UNITY_REVERSED_Z
                c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
                #else
                c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return c;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(Attributes v) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(v); return TransformObjectToHClip(v.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        CGPROGRAM
        #pragma surface surf Wrapped vertex:vert fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex, _GlowTex;
        fixed4 _Color;
        half _Glow, _Wrap, _Occlusion;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void vert(inout appdata_full v)
        {
            #ifndef UNITY_COLORSPACE_GAMMA
            v.color.rgb = GammaToLinearSpace(v.color.rgb); // the occlusion was baked in sRGB
            #endif
        }

        half4 LightingWrapped(SurfaceOutput s, half3 lightDir, half atten)
        {
            half d = saturate((dot(s.Normal, lightDir) + _Wrap) / (1 + _Wrap));
            return half4(s.Albedo * _LightColor0.rgb * d * atten, 1);
        }

        void surf(Input i, inout SurfaceOutput o)
        {
            o.Albedo = tex2D(_MainTex, i.uv_MainTex).rgb * _Color.rgb * lerp(1, i.color.r, _Occlusion);
            o.Emission = tex2D(_GlowTex, i.uv_MainTex).rgb * _Glow;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
