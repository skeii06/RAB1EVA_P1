/* -------------------------------------------------------------------------
 * Copyright © 2026Dave Berg. All rights reserved.
 * 
 * File: BuiltIn_Wind.shader
 * 
 * ------------------------------------------------------------------------- */
Shader "TreeGenerator/BuiltIn_Wind"
{
    Properties
    {
        [Header(Base Layer)]
        _BaseMap("Base Map", 2D) = "white" {}
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        _BaseMult("Base Brightness", Float) = 1.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
 
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaCutoffEnable("Alpha Cutoff Enable", Float) = 0.0
        [Toggle(_BILLBOARD)] _Billboard("Billboard Mode", Float) = 0.0
      
        [Header(Base UV Controls)]
        _UTiling("U Tiling", Float) = 1.0
        _VTiling("V Tiling", Float) = 1.0
        _Twist("Twist (Radians)", Float) = 0.0

        [Header(Wind Settings)]
        [Toggle(_WIND_ON)] _UseWind("Enable Wind", Float) = 1.0
        _PivotMap("Pivot Texture (Hybrid)", 2D) = "black" {}
     
        _PivotCount("Pivot Count", Float) = 0
    
        _PivotMin("Pivot Min Bound", Vector) = (0,0,0,0)
        _PivotRange("Pivot Range", Vector) = (1,1,1,1)
      
        _WindDir("Wind Direction", Vector) = (1,0,0,0)
        _WindBaseLockStart("Wind Base Lock Start", Range(0.0, 5.0)) = 0.25
        _WindBaseLockEnd("Wind Base Lock End", Range(0.01, 10.0)) = 2.50
        [Enum(Off,0,L2 Current Pivot,1,L2 Full Parent Pivot,2,L2 Error Color,3)] _WindL2AttachDebug("Wind L2 Attach Debug", Float) = 0.0

        [Header(Growth Settings)]
        [Toggle(_GROW_ON)] _UseGrow("Enable Growth", Float) = 0.0
        _GrowPhase("Grow Phase", Range(0.0, 1.0)) = 1.0
        _GrowMaskCutoff("Grow Mask Cutoff", Range(0.0, 0.1)) = 0.02
        _GrowRadiusWindow("Grow Radius Window", Range(0.001, 0.5)) = 0.10
        _GrowTipLead("Grow Tip Lead", Range(0.0, 0.5)) = 0.0
        
        [HideInInspector] _PhaseOffset("Global Phase Offset", Float) = 0.0
        
        [Header(Wind L0 Trunk)]
        _Speed_Trunk("Trunk Speed", Float) = 1.0
        _Power_Trunk("Trunk Power", Float) = 0.5

        [Header(Wind L1 Branches)]
        _Speed_L1("Branch Speed", Float) = 1.5
        _Power_L1("Branch Power", Float) = 0.2

        [Header(Wind L2 Twigs)]
        _Speed_L2("Twig Speed", Float) = 3.0
        _Power_L2("Twig Power", Float) = 0.1

        [Header(Height Blend Layer)]
        [Toggle(_HEIGHTBLEND)] _UseHeightBlend("Enable Height Blend", Float) = 0.0
        _HeightMap("Height Layer Map", 2D) = "white" {}
        _HeightBumpMap("Height Layer Normal", 2D) = "bump" {}
        _HeightColor("Height Color", Color) = (0.5, 0.4, 0.3, 1)
    
        _HeightHeight("Height Threshold", Range(0.0, 1.0)) = 0.0
        _HeightFade("Height Fade", Range(0.01, 10.0)) = 1.0
        _HeightRepeatU("Height U Tiling", Float) = 1.0
        _HeightRepeatV("Height V Tiling", Float) = 1.0
        _HeightBaseMult("Height Brightness", Float) = 1.0

        [Header(Top Layer Snow Moss)]
        [Toggle(_TOPLAYER)] _UseTopLayer("Enable Top Layer", Float) = 0.0
        _TopMap("Top Layer Map", 2D) = "white" {}
        _TopBumpMap("Top Layer Normal", 2D) = "bump" {}
        _TopColor("Top Color", Color) = (1, 1, 1, 1)
        _TopBaseMult("Top Brightness", Float) = 1.0
        _TopNoiseScale("Noise Scale", Range(0.0, 10.0)) = 2.0
        _TopBlendMax("Coverage", Range(0.0, 2.0)) = 0.5
        _TopOpacity("Opacity", Range(0.0, 1.0)) = 1.0
        _TopRepeatU("Top U Tiling", Float) = 1.0
        _TopRepeatV("Top V Tiling", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow keepalpha dithercrossfade
        #pragma target 3.0

        #pragma shader_feature _HEIGHTBLEND
        #pragma shader_feature _TOPLAYER
        #pragma shader_feature _ALPHATEST_ON
        #pragma shader_feature _BILLBOARD
        #pragma shader_feature _WIND_ON
        #pragma shader_feature _GROW_ON

        #include "UnityCG.cginc"

        struct appdata_tree {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 tangent : TANGENT;
            float4 color : COLOR;
            float2 texcoord : TEXCOORD0;
            float2 texcoord1 : TEXCOORD1;
            float2 texcoord2 : TEXCOORD2;
            float2 texcoord3 : TEXCOORD3;
            float2 texcoord4 : TEXCOORD4; 
            float2 texcoord5 : TEXCOORD5;
            float2 texcoord6 : TEXCOORD6;
            float2 texcoord7 : TEXCOORD7;
        };

        struct Input
        {
            float2 uv_BaseMap;
            float4 color : COLOR;
            float3 worldPos;
            float3 worldNormal; 
            float3 viewDir;
            INTERNAL_DATA 
            
            float heightOS; 
            float3 restPosWS;
            float growthBirth;
            float growthOpen;
            float windAttachError;
            
            float vface : VFACE;
        };

        sampler2D _BaseMap;
        sampler2D _BumpMap;
        sampler2D _PivotMap;
        float4 _PivotMap_TexelSize;
        sampler2D _HeightMap;
        sampler2D _HeightBumpMap;
        sampler2D _TopMap;
        sampler2D _TopBumpMap;

        float4 _BaseColor;
        float _BaseMult;
        float _Smoothness, _Metallic, _Cutoff;
        float _UTiling, _VTiling, _Twist;
        float4 _PivotMin, _PivotRange;
        float _PivotCount;
        float3 _WindDir;
        float _WindBaseLockStart, _WindBaseLockEnd;
        float _WindL2AttachDebug;
        float _UseGrow, _GrowPhase, _GrowMaskCutoff, _GrowRadiusWindow, _GrowTipLead;
        float _PhaseOffset;
        float _Speed_Trunk, _Power_Trunk;
        float _Speed_L1, _Power_L1;
        float _Speed_L2, _Power_L2;
        
        float4 _HeightColor;
        float _HeightHeight, _HeightFade, _HeightBaseMult, _HeightRepeatU, _HeightRepeatV;
        
        float4 _TopColor;
        float _TopBaseMult, _TopNoiseScale, _TopBlendMax, _TopOpacity, _TopRepeatU, _TopRepeatV;

        float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float4 mod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float4 permute(float4 x) { return mod289(((x*34.0)+1.0)*x); }
        float4 taylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }
        
        float snoise(float3 v) {
            const float2 C = float2(1.0/6.0, 1.0/3.0);
            const float4 D = float4(0.0, 0.5, 1.0, 2.0);
            float3 i  = floor(v + dot(v, C.yyy) );
            float3 x0 = v - i + dot(i, C.xxx) ;
            float3 g = step(x0.yzx, x0.xyz);
            float3 l = 1.0 - g;
            float3 i1 = min( g.xyz, l.zxy );
            float3 i2 = max( g.xyz, l.zxy );
            float3 x1 = x0 - i1 + C.xxx;
            float3 x2 = x0 - i2 + C.yyy;
            float3 x3 = x0 - D.yyy;      
            i = mod289(i);
            float4 p = permute( permute( permute( i.z + float4(0.0, i1.z, i2.z, 1.0 )) + i.y + float4(0.0, i1.y, i2.y, 1.0 )) + i.x + float4(0.0, i1.x, i2.x, 1.0 ));
            float n_ = 0.142857142857;
            float3 ns = n_ * D.wyz - D.xzx;
            float4 j = p - 49.0 * floor(p * ns.z * ns.z);
            float4 x_ = floor(j * ns.z);
            float4 y_ = floor(j - 7.0 * x_ );
            float4 x = x_ *ns.x + ns.yyyy;
            float4 y = y_ *ns.x + ns.yyyy;
            float4 h = 1.0 - abs(x) - abs(y);
            float4 b0 = float4( x.xy, y.xy );
            float4 b1 = float4( x.zw, y.zw );
            float4 s0 = floor(b0)*2.0 + 1.0;
            float4 s1 = floor(b1)*2.0 + 1.0;
            float4 sh = -step(h, float4(0.0, 0.0, 0.0, 0.0));
            float4 a0 = b0.xzyw + s0.xzyw*sh.xxyy ;
            float4 a1 = b1.xzyw + s1.xzyw*sh.zzww ;
            float3 p0 = float3(a0.xy,h.x);
            float3 p1 = float3(a0.zw,h.y);
            float3 p2 = float3(a1.xy,h.z);
            float3 p3 = float3(a1.zw,h.w);
            float4 norm = taylorInvSqrt(float4(dot(p0,p0), dot(p1,p1), dot(p2, p2), dot(p3,p3)));
            p0 *= norm.x; p1 *= norm.y; p2 *= norm.z; p3 *= norm.w;
            float4 m = max(0.6 - float4(dot(x0,x0), dot(x1,x1), dot(x2,x2), dot(x3,x3)), 0.0);
            m = m * m;
            return 42.0 * dot( m*m, float4( dot(p0,x0), dot(p1,x1), dot(p2,x2), dot(p3,x3) ) );
        }

        float4 SamplePackedTexel(float index)
        {
            float totalCount = max(_PivotCount, 1.0);
            float width = max(_PivotMap_TexelSize.z, 1.0);
            float height = max(_PivotMap_TexelSize.w, 1.0);
            float clampedIndex = clamp(index, 0.0, max(totalCount - 1.0, 0.0));
            float u = (fmod(clampedIndex, width) + 0.5) / width;
            float v = (floor(clampedIndex / width) + 0.5) / height;
            return tex2Dlod(_PivotMap, float4(u, v, 0, 0));
        }

        float DecodeUNorm16(float hi, float lo)
        {
            float hiByte = floor(saturate(hi) * 255.0 + 0.5);
            float loByte = floor(saturate(lo) * 255.0 + 0.5);
            return ((hiByte * 256.0) + loByte) / 65535.0;
        }

        float DecodeRange8(float value01, float minValue, float rangeValue)
        {
            return saturate(value01) * rangeValue + minValue;
        }

        float DecodeRange16(float hi, float lo, float minValue, float rangeValue)
        {
            return DecodeUNorm16(hi, lo) * rangeValue + minValue;
        }

        float3 DecodePackedPoint(float4 texelA, float4 texelB)
        {
            return float3(
                DecodeRange16(texelA.r, texelA.g, _PivotMin.x, _PivotRange.x),
                DecodeRange16(texelA.b, texelA.a, _PivotMin.y, _PivotRange.y),
                DecodeRange16(texelB.r, texelB.g, _PivotMin.z, _PivotRange.z)
            );
        }

        float3 FixPackedGrowthPoint(float3 rawPoint, float mode)
        {
            return rawPoint * float3(-1, 1, -1);
        }

        float DecodeBaseIndex(appdata_tree v)
        {
            return max(0.0, floor(v.texcoord3.y + 0.5));
        }

        float DecodeParentBendFactor(appdata_tree v, float fallbackFactor)
        {
            float packedParentFactor = saturate(frac(v.texcoord3.y) * 4.0);
            return (packedParentFactor > 0.0001) ? packedParentFactor : fallbackFactor;
        }

        float WindUv7Present(appdata_tree v)
        {
            return step(0.00001, abs(v.texcoord7.x) + abs(v.texcoord7.y));
        }

        float DecodeWindIndex(appdata_tree v)
        {
            return DecodeBaseIndex(v);
        }

        void ApplyGrowth(inout float3 posOS, appdata_tree v, out float birthOut, out float openOut)
        {
            birthOut = min(saturate(v.texcoord3.x), 0.9999);
            openOut = 1.0;
            if (_UseGrow < 0.5) return;

            float packedBlue = v.color.b;
            float mode = floor(packedBlue * 4.0 + 0.01);
            float3 directGrowthAnchor = FixPackedGrowthPoint(float3(v.texcoord5.x, v.texcoord6.x, v.texcoord5.y), mode);
            float directGrowthValid = 1.0;
            float3 growthAnchor = directGrowthAnchor;

            if (directGrowthValid < 0.5)
            {
                if (_PivotCount < 1.0) return;

                float baseIndex = DecodeBaseIndex(v);
                if (baseIndex + 2.0 >= _PivotCount) return;

                float4 growthTexelA = SamplePackedTexel(baseIndex + 1.0);
                float4 growthTexelB = SamplePackedTexel(baseIndex + 2.0);
                growthAnchor = FixPackedGrowthPoint(DecodePackedPoint(growthTexelA, growthTexelB), mode);
            }

            float birthTime = birthOut;
            birthOut = birthTime;

            float radiusWindow = max(0.0001, _GrowRadiusWindow);
            // Remap the 0-1 slider so the timeline naturally extends to accommodate the final leaves
            float effectivePhase = _GrowPhase * (1.0 + radiusWindow);
            
            // Remove the squish! Every leaf gets its full, natural window to grow.
            float growthWindow = radiusWindow;
            float growthScale = saturate((effectivePhase - birthTime) / growthWindow);
            openOut = growthScale;

            posOS = lerp(growthAnchor, posOS, growthScale);
        }

        float3 GetWindOffset(float3 posOS, inout float3 normal, inout float4 tangent, appdata_tree v, out float attachError)
        {
            attachError = 0.0;
            if (_PivotCount < 0.5) return posOS;

            float packedBlue = v.color.b;
            float Mode = floor(packedBlue * 4.0 + 0.01);
            float Phase = v.color.r;
            float ParentPhase = v.color.g;
            float GPPhase = v.color.a;
            
            float Dampening = saturate((packedBlue - (Mode * 0.25)) / 0.09);

            float highP_ParentY = v.texcoord4.x;
            float Index = DecodeWindIndex(v);
            float4 rootTexel = SamplePackedTexel(Index);
            
            float3 rawPivot = float3(v.texcoord1.x, v.texcoord2.x, v.texcoord1.y);
            float hasWindUv7 = WindUv7Present(v);
            float fallbackParentZ = v.texcoord4.y;
            float windParentZ = lerp(fallbackParentZ, v.texcoord7.x, hasWindUv7);
            float3 rawParent = float3(v.texcoord2.y, highP_ParentY, windParentZ);

            float3 Pivot, ParentPivot;
            if (Mode < 0.5) 
            {
                Pivot = rawPivot * float3(1, 1, -1);
                ParentPivot = rawParent * float3(1, 1, -1);
            }
            else if (Mode < 1.5)
            {
                Pivot = rawPivot * float3(-1, 1, 1);
                ParentPivot = rawParent * float3(1, 1, -1);
            }
            else
            {
                Pivot = rawPivot * float3(-1, 1, 1);
                ParentPivot = rawParent * float3(-1, 1, 1);
            }

            float3 RootPivot = rootTexel.rgb * _PivotRange.xyz + _PivotMin.xyz;
            if (Mode >= 1.5 && hasWindUv7 > 0.5)
            {
                float3 rawHighPrecisionRoot = float3(v.texcoord6.y, RootPivot.y, v.texcoord7.y);
                RootPivot = rawHighPrecisionRoot * float3(1, 1, -1);
            }

            float distFromPivot = length(posOS - Pivot);
            if (Mode >= 0.5)
            {
                float3 centerlinePos = FixPackedGrowthPoint(float3(v.texcoord5.x, v.texcoord6.x, v.texcoord5.y), Mode);
                distFromPivot = length(centerlinePos - Pivot);
            }
            float weldMask = (Mode < 0.5)
                ? saturate(distFromPivot * 30.0)
                : smoothstep(_WindBaseLockStart, max(_WindBaseLockEnd, _WindBaseLockStart + 0.001), distFromPivot);
            float parentBendFactor = (Mode >= 1.5) ? DecodeParentBendFactor(v, Dampening) : Dampening;

            float3 windDir = normalize(length(_WindDir) > 0.01 ? _WindDir : float3(1,0,0));
            float3 windAxisTrunk = normalize(cross(float3(0, 1, 0), windDir));
            if (dot(windAxisTrunk, windAxisTrunk) < 1e-6) windAxisTrunk = float3(0, 0, 1);
            
            float3 windAxisBranch = windAxisTrunk;
            if (dot(windAxisBranch, windAxisBranch) < 1e-6) windAxisBranch = float3(0, 0, 1);

            float3 worldPivotPos = float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);
            float worldPosSeed = (worldPivotPos.x + worldPivotPos.z) * 0.1;
            float n_time = _Time.y + (_PhaseOffset * 100.0) + worldPosSeed;

            float s, c, oc;
            float3 a = windAxisTrunk;
            float seed, freq, noiseVal;
            float3x3 matRot;

            #define MAKE_ROT_MAT(ang) \
                s = sin(ang); \
                c = cos(ang); \
                oc = 1.0 - c; \
                matRot = float3x3( \
                    oc * a.x * a.x + c,        oc * a.x * a.y - a.z * s, oc * a.z * a.x + a.y * s, \
                    oc * a.x * a.y + a.z * s,  oc * a.y * a.y + c,       oc * a.y * a.z - a.x * s, \
                    oc * a.z * a.x - a.y * s,  oc * a.y * a.z + a.x * s, oc * a.z * a.z + c \
                );

            a = windAxisTrunk;
            seed = Phase * 10.0;
            freq = (n_time * _Speed_Trunk) + seed;
            noiseVal = sin(freq) + sin(freq * 1.32) * 0.5;
            float trunkAngle = noiseVal * 0.05 * _Power_Trunk * Dampening;
            MAKE_ROT_MAT(trunkAngle)
            float3x3 matTrunk_Self = matRot;

            a = windAxisTrunk;
            seed = ParentPhase * 10.0;
            freq = (n_time * _Speed_Trunk) + seed;
            noiseVal = sin(freq) + sin(freq * 1.32) * 0.5;
            float pTrunkAngle = noiseVal * 0.05 * _Power_Trunk * Dampening;
            MAKE_ROT_MAT(pTrunkAngle)
            float3x3 matTrunk_Parent = matRot;

            a = windAxisTrunk;
            seed = GPPhase * 10.0;
            freq = (n_time * _Speed_Trunk) + seed;
            noiseVal = sin(freq) + sin(freq * 1.32) * 0.5;
            float gpTrunkAngle = noiseVal * 0.05 * _Power_Trunk * Dampening;
            MAKE_ROT_MAT(gpTrunkAngle)
            float3x3 matTrunk_GP = matRot;

            a = windAxisBranch;
            seed = Phase * 10.0 + dot(Pivot, float3(0.01, 0.05, 0.02));
            freq = (n_time * _Speed_L1) + seed;
            noiseVal = sin(freq) + sin(freq * 1.32) * 0.5;
            float l1Angle = noiseVal * 0.15 * _Power_L1 * Dampening * weldMask;
            MAKE_ROT_MAT(l1Angle)
            float3x3 matL1 = matRot;

            a = windAxisBranch;
            seed = ParentPhase * 10.0 + dot(ParentPivot, float3(0.01, 0.05, 0.02));
            freq = (n_time * _Speed_L1) + seed;
            noiseVal = sin(freq) + sin(freq * 1.32) * 0.5;
            float parentL1Noise = noiseVal;
            float pl1Angle = noiseVal * 0.15 * _Power_L1 * parentBendFactor;
            MAKE_ROT_MAT(pl1Angle)
            float3x3 matParentL1 = matRot;

            a = windAxisBranch;
            float TwigPhase = ParentPhase;
            seed = TwigPhase * 15.0 + dot(Pivot, float3(0.01, 0.05, 0.02));
            freq = (n_time * _Speed_L2) + seed;
            noiseVal = sin(freq * 1.5) + sin(freq * 2.5) * 0.3;
            float l2Angle = noiseVal * 0.15 * _Power_L2 * Dampening * weldMask;
            MAKE_ROT_MAT(l2Angle)
            float3x3 matL2 = matRot;

            float3 transformed = posOS;

            if (Mode < 0.5) 
            {
                transformed = ParentPivot + mul(matTrunk_Self, posOS - ParentPivot);
                normal = mul(matTrunk_Self, normal);
                tangent.xyz = mul(matTrunk_Self, tangent.xyz);
            }
            else if (Mode < 1.5) 
            {
                float3 vecBranchToRoot = Pivot - ParentPivot;
                float3 v1 = mul(matL1, posOS - Pivot);
                float3 v2 = mul(matTrunk_Parent, v1 + vecBranchToRoot);
                transformed = ParentPivot + v2;
                normal = mul(matTrunk_Parent, mul(matL1, normal));
                tangent.xyz = mul(matTrunk_Parent, mul(matL1, tangent.xyz));
            }
            else 
            {
                float3 vecTwigToVert = posOS - Pivot;
                if (dot(ParentPivot, ParentPivot) < 0.001) {
                    ParentPivot = Pivot;
                }
                float3 vecBranchToTwig = Pivot - ParentPivot;
                float3 vecRootToBranch_Stable = float3(0, ParentPivot.y - RootPivot.y, 0);

                float3 vTwig = mul(matL2, vecTwigToVert);
                float3 vBranch = mul(matParentL1, vTwig + vecBranchToTwig);
                float3 vTrunk = mul(matTrunk_GP, vBranch + vecRootToBranch_Stable);
                
                float3 ApproxRoot = float3(ParentPivot.x, RootPivot.y, ParentPivot.z);
                transformed = ApproxRoot + vTrunk;

                normal = mul(matTrunk_GP, mul(matParentL1, mul(matL2, normal)));
                tangent.xyz = mul(matTrunk_GP, mul(matParentL1, mul(matL2, tangent.xyz)));

                if (_WindL2AttachDebug > 0.5)
                {
                    float fullParentAngle = parentL1Noise * 0.15 * _Power_L1 * parentBendFactor;
                    a = windAxisBranch;
                    MAKE_ROT_MAT(fullParentAngle)
                    float3 currentParentPivot = ApproxRoot + mul(matTrunk_GP, mul(matParentL1, vecBranchToTwig) + vecRootToBranch_Stable);
                    float3 fullParentPivot = ApproxRoot + mul(matTrunk_GP, mul(matRot, vecBranchToTwig) + vecRootToBranch_Stable);
                    attachError = length(currentParentPivot - fullParentPivot);

                    if (_WindL2AttachDebug < 1.5)
                    {
                        transformed = currentParentPivot + (posOS - Pivot) * 0.18;
                    }
                    else if (_WindL2AttachDebug < 2.5)
                    {
                        transformed = fullParentPivot + (posOS - Pivot) * 0.18;
                    }
                }
            }
            
            normal = normalize(normal);
            tangent.xyz = normalize(tangent.xyz);
            
            return transformed;
        }

        void vert(inout appdata_tree v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.heightOS = v.vertex.y;
            o.restPosWS = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.growthBirth = 1.0;
            o.growthOpen = 1.0;
            o.windAttachError = 0.0;

            float3 posOS = v.vertex.xyz;
            #if !defined(_BILLBOARD)
                // Removed the hardcoded ALPHATEST_ON bypass. 
                // Now, cutouts (leaves) properly scale up from their root attachment pivot instead of harshly popping.
                ApplyGrowth(posOS, v, o.growthBirth, o.growthOpen);
            #endif

            #if defined(_BILLBOARD) || !defined(_WIND_ON)
                posOS = posOS;
            #else
                posOS = GetWindOffset(posOS, v.normal, v.tangent, v, o.windAttachError);
            #endif
            
            v.vertex.xyz = posOS;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_BaseMap;
            
            float s = sin(_Twist * uv.y);
            float c = cos(_Twist * uv.y);
            uv.x += _Twist * uv.y;
            float2 baseUV = uv * float2(_UTiling, _VTiling);

            half4 albedo = tex2D(_BaseMap, baseUV) * _BaseColor;
            albedo.rgb *= _BaseMult; // Apply brightness to RGB only!
            float3 normalTS = UnpackNormal(tex2D(_BumpMap, baseUV));

            #if !defined(_BILLBOARD)
                if (_UseGrow > 0.5)
                {
                    if (_GrowPhase <= 0.0001)
                    {
                        clip(-1.0);
                    }
                    else if (_GrowPhase < 0.9999)
                    {
                        // Use the exact same remapped timeline for the visibility clip
                        float effectivePhase = _GrowPhase * (1.0 + max(0.0001, _GrowRadiusWindow));
                        clip(effectivePhase - IN.growthBirth);
                        clip(IN.growthOpen - max(_GrowMaskCutoff, 0.001));
                    }
                }
            #endif

            #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
            #endif

            #if defined(_HEIGHTBLEND)
                float normalizedHeight = (IN.heightOS - _PivotMin.y) / max(_PivotRange.y, 0.001);
                float hMask = smoothstep(_HeightHeight, _HeightHeight + _HeightFade, normalizedHeight);
                float2 hUV = uv * float2(_HeightRepeatU, _HeightRepeatV); 
                hUV.x += _Twist * _HeightRepeatU * uv.y;
                half4 hCol = tex2D(_HeightMap, hUV) * _HeightColor * _HeightBaseMult;
                float3 hNorm = UnpackNormal(tex2D(_HeightBumpMap, hUV));
                albedo = lerp(albedo, hCol, hMask);
                normalTS = lerp(normalTS, hNorm, hMask);
            #endif

            #if defined(_TOPLAYER)
                float3 noisePos = IN.restPosWS * _TopNoiseScale;
                float noiseVal = snoise(noisePos);
                
                float3 worldN = WorldNormalVector(IN, float3(0,0,1)); 
                
                float nDotUp = dot(normalize(worldN), float3(0,1,0));
                float snowSignal = nDotUp + (noiseVal * 0.5);
                float threshold = 1.0 - (clamp(_TopBlendMax,0,2) * 0.5);
                float mask = saturate(smoothstep(threshold, threshold+0.2, snowSignal) * _TopOpacity);
                
                float2 tUV = uv * float2(_TopRepeatU, _TopRepeatV);
                half4 tCol = tex2D(_TopMap, tUV) * _TopColor * _TopBaseMult;
                float3 tNorm = UnpackNormal(tex2D(_TopBumpMap, tUV));
                
                albedo = lerp(albedo, tCol, mask);
                normalTS = lerp(normalTS, tNorm, mask);
            #endif

            if (_WindL2AttachDebug > 2.5 && IN.windAttachError > 0.0001)
            {
                float errorMask = saturate(IN.windAttachError * 4.0);
                albedo.rgb = lerp(float3(0.0, 1.0, 0.0), float3(1.0, 0.0, 1.0), errorMask);
                normalTS = float3(0.0, 0.0, 1.0);
            }

            o.Albedo = albedo.rgb;
            // Flip the normal if we are rendering the back side of the polygon
            o.Normal = normalTS * (IN.vface > 0.0 ? 1.0 : -1.0);
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = albedo.a;
        }
        ENDCG
    }
    FallBack "Nature/Tree Creator Bark"
}