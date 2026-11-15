/* -------------------------------------------------------------------------
 * Copyright © 2026Dave Berg. All rights reserved.
 * 
 * File: /HD_Wind.shader
 * 
 * ------------------------------------------------------------------------- */
Shader "TreeGenerator/HD_Wind"
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
        // Default set to 0.0 so bark naturally collapses into pencil tips instead of revealing open tubes
        [Toggle(_ALPHATEST_ON)] _AlphaCutoffEnable("Alpha Cutoff Enable (Leaves Only)", Float) = 0.0
        [Toggle(_BILLBOARD)] _Billboard("Billboard Mode", Float) = 0.0
        
        // HDRP Specifics
        [ToggleUI] _DoubleSidedEnable("Double Sided Enable", Float) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Float) = 0 

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
        Tags 
        { 
            "RenderPipeline" = "HDRP" 
            "RenderType" = "HDLitShader" 
            "Queue" = "AlphaTest" 
        }
        
        Cull [_CullMode]

        HLSLINCLUDE
        // Native HDRP Library Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
        
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _BaseMult;
            float _Smoothness, _Metallic, _Cutoff;
            float _AlphaCutoffEnable;
            float _Billboard;
            float _UTiling, _VTiling, _Twist;
            
            float _UseWind;
            float4 _PivotMin, _PivotRange;
            float _PivotCount;
            float3 _WindDir;
            float _WindBaseLockStart, _WindBaseLockEnd;
            float _WindL2AttachDebug;
            float _PhaseOffset;
            float _Speed_Trunk, _Power_Trunk;
            float _Speed_L1, _Power_L1;
            float _Speed_L2, _Power_L2;

            float _UseGrow, _GrowPhase, _GrowMaskCutoff, _GrowRadiusWindow, _GrowTipLead;
            
            float4 _HeightColor;
            float _HeightHeight, _HeightFade, _HeightBaseMult, _HeightRepeatU, _HeightRepeatV;
            
            float4 _TopColor;
            float _TopBaseMult, _TopNoiseScale, _TopBlendMax, _TopOpacity, _TopRepeatU, _TopRepeatV;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        TEXTURE2D(_PivotMap); SAMPLER(sampler_PivotMap); float4 _PivotMap_TexelSize;
        TEXTURE2D(_HeightMap); SAMPLER(sampler_HeightMap);
        TEXTURE2D(_HeightBumpMap);
        TEXTURE2D(_TopMap); SAMPLER(sampler_TopMap);
        TEXTURE2D(_TopBumpMap);

        struct Attributes_Tree {
            float4 positionOS   : POSITION;
            float3 normalOS     : NORMAL;
            float4 tangentOS    : TANGENT;
            float4 color        : COLOR;
            float2 uv           : TEXCOORD0;
            float2 texcoord1    : TEXCOORD1;
            float2 texcoord2    : TEXCOORD2;
            float2 texcoord3    : TEXCOORD3;
            float2 texcoord4    : TEXCOORD4; 
            float2 texcoord5    : TEXCOORD5;
            float2 texcoord6    : TEXCOORD6;
            float2 texcoord7    : TEXCOORD7;
        };

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
            return SAMPLE_TEXTURE2D_LOD(_PivotMap, sampler_PivotMap, float2(u, v), 0);
        }

        float DecodeUNorm16(float hi, float lo) {
            float hiByte = floor(saturate(hi) * 255.0 + 0.5);
            float loByte = floor(saturate(lo) * 255.0 + 0.5);
            return ((hiByte * 256.0) + loByte) / 65535.0;
        }

        float DecodeRange8(float value01, float minValue, float rangeValue) {
            return saturate(value01) * rangeValue + minValue;
        }

        float DecodeRange16(float hi, float lo, float minValue, float rangeValue) {
            return DecodeUNorm16(hi, lo) * rangeValue + minValue;
        }

        float3 DecodePackedPoint(float4 texelA, float4 texelB) {
            return float3(
                DecodeRange16(texelA.r, texelA.g, _PivotMin.x, _PivotRange.x),
                DecodeRange16(texelA.b, texelA.a, _PivotMin.y, _PivotRange.y),
                DecodeRange16(texelB.r, texelB.g, _PivotMin.z, _PivotRange.z)
            );
        }

        float3 FixPackedGrowthPoint(float3 rawPoint, float mode) {
            return rawPoint * float3(-1, 1, -1);
        }

        float DecodeBaseIndex(Attributes_Tree v) {
            return max(0.0, floor(v.texcoord3.y + 0.5));
        }

        float DecodeParentBendFactor(Attributes_Tree v, float fallbackFactor) {
            float packedParentFactor = saturate(frac(v.texcoord3.y) * 4.0);
            return (packedParentFactor > 0.0001) ? packedParentFactor : fallbackFactor;
        }

        float WindUv7Present(Attributes_Tree v) {
            return step(0.00001, abs(v.texcoord7.x) + abs(v.texcoord7.y));
        }

        void ApplyGrowth(inout float3 posOS, Attributes_Tree v, out float birthOut, out float openOut)
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
            float effectivePhase = _GrowPhase * (1.0 + radiusWindow);
            float growthWindow = radiusWindow;
            float growthScale = saturate((effectivePhase - birthTime) / growthWindow);
            openOut = growthScale;

            posOS = lerp(growthAnchor, posOS, growthScale);
        }

        float3 GetWindOffset(float3 posOS, inout float3 normal, inout float4 tangent, Attributes_Tree v, out float attachError)
        {
            attachError = 0.0;
            if (_PivotCount < 0.5) return posOS;

            // --- 1. DETERMINE MODE & UNPACK COLORS ---
            float packedBlue = v.color.b;
            float Mode = floor(packedBlue * 4.0 + 0.01);
            float Phase = v.color.r;
            float ParentPhase = v.color.g;
            float GPPhase = v.color.a;
            float Dampening = saturate((packedBlue - (Mode * 0.25)) / 0.09);

            // --- 2. DECODE RAW PIVOTS ---
            float highP_ParentY = v.texcoord4.x;
            float Index = DecodeBaseIndex(v);
            float4 rootTexel = SamplePackedTexel(Index);
            
            float3 rawPivot = float3(v.texcoord1.x, v.texcoord2.x, v.texcoord1.y);
            float hasWindUv7 = WindUv7Present(v);
            float fallbackParentZ = v.texcoord4.y;
            float windParentZ = lerp(fallbackParentZ, v.texcoord7.x, hasWindUv7);
            float3 rawParent = float3(v.texcoord2.y, highP_ParentY, windParentZ);

            // --- 3. APPLY COORDINATE FIXES ---
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

            // --- 4. TEXTURE LOOKUP (ROOT PIVOT) ---
            float3 RootPivot = rootTexel.rgb * _PivotRange.xyz + _PivotMin.xyz;
            if (Mode >= 1.5 && hasWindUv7 > 0.5)
            {
                float3 rawHighPrecisionRoot = float3(v.texcoord6.y, RootPivot.y, v.texcoord7.y);
                RootPivot = rawHighPrecisionRoot * float3(1, 1, -1);
            }

            // --- WELD MASK ---
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

            // --- AXIS ---
            float3 windDir = normalize(length(_WindDir) > 0.01 ? _WindDir : float3(1,0,0));
            float3 windAxisTrunk = normalize(cross(float3(0, 1, 0), windDir));
            if (dot(windAxisTrunk, windAxisTrunk) < 1e-6) windAxisTrunk = float3(0, 0, 1);
            
            float3 windAxisBranch = windAxisTrunk;
            if (dot(windAxisBranch, windAxisBranch) < 1e-6) windAxisBranch = float3(0, 0, 1);

            // --- RANDOMIZED PHASE & CAMERA FIX ---
            // In HDRP, TransformObjectToWorld is Relative to the Camera.
            // We use GetAbsolutePositionWS to resolve true world space to prevent the wind seed from stuttering[cite: 1].
            float3 pivotRWS = TransformObjectToWorld(float3(0,0,0));
            float3 worldPivotPos = GetAbsolutePositionWS(pivotRWS);
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

            // 1. TRUNK
            a = windAxisTrunk;
            seed = Phase * 10.0;
            freq = (n_time * _Speed_Trunk) + seed;
            noiseVal = sin(freq) + sin(freq * 1.32) * 0.5;
            float trunkAngle = noiseVal * 0.05 * _Power_Trunk * Dampening;
            MAKE_ROT_MAT(trunkAngle)
            float3x3 matTrunk_Self = matRot;

            // Parent Trunk
            a = windAxisTrunk;
            seed = ParentPhase * 10.0;
            freq = (n_time * _Speed_Trunk) + seed;
            noiseVal = sin(freq) + sin(freq * 1.32) * 0.5;
            float pTrunkAngle = noiseVal * 0.05 * _Power_Trunk * Dampening;
            MAKE_ROT_MAT(pTrunkAngle)
            float3x3 matTrunk_Parent = matRot;

            // GP Trunk
            a = windAxisTrunk;
            seed = GPPhase * 10.0;
            freq = (n_time * _Speed_Trunk) + seed;
            noiseVal = sin(freq) + sin(freq * 1.32) * 0.5;
            float gpTrunkAngle = noiseVal * 0.05 * _Power_Trunk * Dampening;
            MAKE_ROT_MAT(gpTrunkAngle)
            float3x3 matTrunk_GP = matRot;

            // 2. BRANCH
            a = windAxisBranch;
            seed = Phase * 10.0 + dot(Pivot, float3(0.01, 0.05, 0.02));
            freq = (n_time * _Speed_L1) + seed;
            noiseVal = sin(freq) + sin(freq * 1.32) * 0.5;
            float l1Angle = noiseVal * 0.15 * _Power_L1 * Dampening * weldMask;
            MAKE_ROT_MAT(l1Angle)
            float3x3 matL1 = matRot;

            // Parent Branch
            a = windAxisBranch;
            seed = ParentPhase * 10.0 + dot(ParentPivot, float3(0.01, 0.05, 0.02));
            freq = (n_time * _Speed_L1) + seed;
            noiseVal = sin(freq) + sin(freq * 1.32) * 0.5;
            float parentL1Noise = noiseVal;
            float pl1Angle = noiseVal * 0.15 * _Power_L1 * parentBendFactor;
            MAKE_ROT_MAT(pl1Angle)
            float3x3 matParentL1 = matRot;

            // 3. TWIG
            a = windAxisBranch;
            float TwigPhase = ParentPhase;
            seed = TwigPhase * 15.0 + dot(Pivot, float3(0.01, 0.05, 0.02));
            freq = (n_time * _Speed_L2) + seed;
            noiseVal = sin(freq * 1.5) + sin(freq * 2.5) * 0.3;
            float l2Angle = noiseVal * 0.15 * _Power_L2 * Dampening * weldMask;
            MAKE_ROT_MAT(l2Angle)
            float3x3 matL2 = matRot;

            // --- HIERARCHY ---
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

                    if (_WindL2AttachDebug < 1.5) {
                        transformed = currentParentPivot + (posOS - Pivot) * 0.18;
                    } else if (_WindL2AttachDebug < 2.5) {
                        transformed = fullParentPivot + (posOS - Pivot) * 0.18;
                    }
                }
            }
            normal = normalize(normal);
            tangent.xyz = normalize(tangent.xyz);
            return transformed;
        }
        ENDHLSL

        // ------------------------------------------------------------------
        // DEPTH FORWARD ONLY PASS (Depth Prepass)
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode" = "DepthForwardOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthPassVertex
            #pragma fragment DepthPassFragment

            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _GROW_ON
            #pragma shader_feature_local _BILLBOARD
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            struct Varyings_Depth
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float growthBirth   : TEXCOORD1;
                float growthOpen    : TEXCOORD2;
            };

            Varyings_Depth DepthPassVertex(Attributes_Tree input)
            {
                Varyings_Depth output = (Varyings_Depth)0;
                
                output.growthBirth = 1.0;
                output.growthOpen = 1.0;
                float dummyAttachError = 0.0;
                float3 posOS = input.positionOS.xyz;

                #if !defined(_BILLBOARD)
                    // Removed the ALPHATEST_ON bypass so leaves properly scale instead of popping!
                    ApplyGrowth(posOS, input, output.growthBirth, output.growthOpen);
                #endif

                #if defined(_BILLBOARD) || !defined(_WIND_ON)
                    posOS = posOS;
                #else
                    posOS = GetWindOffset(posOS, input.normalOS, input.tangentOS, input, dummyAttachError);
                #endif

                // In HDRP, TransformObjectToWorld gives Camera Relative WS. TransformWorldToHClip expects Camera Relative WS.
                float3 posRWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(posRWS);
                output.uv = input.uv;
                
                return output;
            }

            void DepthPassFragment(Varyings_Depth input)
            {
                #ifdef LOD_FADE_CROSSFADE
                    float fade = unity_LODFade.x < 0.0 ? 1.0 + unity_LODFade.x : unity_LODFade.x;
                    clip(fade - InterleavedGradientNoise(input.positionCS.xy, 0));
                #endif

                #if !defined(_BILLBOARD)
                    if (_UseGrow > 0.5)
                    {
                        if (_GrowPhase <= 0.0001) { clip(-1.0); }
                        else if (_GrowPhase < 0.9999) {
                            float effectivePhase = _GrowPhase * (1.0 + max(0.0001, _GrowRadiusWindow));
                            clip(effectivePhase - input.growthBirth);
                            clip(input.growthOpen - max(_GrowMaskCutoff, 0.001));
                        }
                    }
                #endif

                if (_AlphaCutoffEnable > 0.5) 
                {
                    float2 uv = input.uv;
                    uv.x += _Twist * uv.y;
                    float2 baseUV = uv * float2(_UTiling, _VTiling);
                    float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV).a * _BaseColor.a;
                    
                    // In HDRP, clipping must be explicit in primary visual passes[cite: 1]
                    clip(alpha - _Cutoff);
                }
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // FORWARD ONLY PASS (Main Lighting)
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "ForwardOnly" } 

            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ForwardPassVertex
            #pragma fragment ForwardPassFragment
            
            // Include HDRP light definitions to access the real scene directional lights
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightDefinition.cs.hlsl"
            
            #pragma shader_feature_local _HEIGHTBLEND
            #pragma shader_feature_local _TOPLAYER
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _GROW_ON
            #pragma shader_feature_local _BILLBOARD
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 tangentWS    : TEXCOORD2;
                float2 uv           : TEXCOORD3;
                float3 restPosWS    : TEXCOORD4; 
                float  heightOS     : TEXCOORD5;
                float  growthBirth  : TEXCOORD6;
                float  growthOpen   : TEXCOORD7;
                float  windError    : TEXCOORD8;
            };

            Varyings ForwardPassVertex(Attributes_Tree input)
            {
                Varyings output = (Varyings)0;
                
                output.heightOS = input.positionOS.y;
                output.restPosWS = GetAbsolutePositionWS(TransformObjectToWorld(input.positionOS.xyz));
                output.growthBirth = 1.0;
                output.growthOpen = 1.0;
                output.windError = 0.0;

                float3 posOS = input.positionOS.xyz;
                
                #if !defined(_BILLBOARD)
                    // Bypass removed! Leaves will properly scale instead of popping.
                    ApplyGrowth(posOS, input, output.growthBirth, output.growthOpen);
                #endif

                #if defined(_BILLBOARD) || !defined(_WIND_ON)
                    posOS = posOS;
                #else
                    posOS = GetWindOffset(posOS, input.normalOS, input.tangentOS, input, output.windError);
                #endif
                
                float3 posRWS = TransformObjectToWorld(posOS);
                output.positionWS = posRWS;
                output.positionCS = TransformWorldToHClip(posRWS);

                // Normal/Tangent extraction for HDRP
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
                output.uv = input.uv;

                return output;
            }

            struct FragOutput {
                float4 color : SV_Target0;
            };

            FragOutput ForwardPassFragment(Varyings input, bool isFrontFace : SV_IsFrontFace)
            {
                #ifdef LOD_FADE_CROSSFADE
                    float fade = unity_LODFade.x < 0.0 ? 1.0 + unity_LODFade.x : unity_LODFade.x;
                    clip(fade - InterleavedGradientNoise(input.positionCS.xy, 0));
                #endif

                #if !defined(_BILLBOARD)
                    if (_UseGrow > 0.5)
                    {
                        if (_GrowPhase <= 0.0001) { clip(-1.0); }
                        else if (_GrowPhase < 0.9999) {
                            float effectivePhase = _GrowPhase * (1.0 + max(0.0001, _GrowRadiusWindow));
                            clip(effectivePhase - input.growthBirth);
                            clip(input.growthOpen - max(_GrowMaskCutoff, 0.001));
                        }
                    }
                #endif

                float2 uv = input.uv;
                float s = sin(_Twist * uv.y);
                float c = cos(_Twist * uv.y);
                uv.x += _Twist * uv.y;
                float2 baseUV = uv * float2(_UTiling, _VTiling);

                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV) * _BaseColor;
                albedo.rgb *= _BaseMult; // Apply brightness to RGB only!
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, baseUV));

                if (_AlphaCutoffEnable > 0.5) {
                    clip(albedo.a - _Cutoff);
                }

                #if defined(_HEIGHTBLEND)
                    float normalizedHeight = (input.heightOS - _PivotMin.y) / max(_PivotRange.y, 0.001);
                    float hMask = smoothstep(_HeightHeight, _HeightHeight + _HeightFade, normalizedHeight);
                    float2 hUV = uv * float2(_HeightRepeatU, _HeightRepeatV); 
                    hUV.x += _Twist * _HeightRepeatU * uv.y;
                    
                    float4 hCol = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, hUV) * _HeightColor * _HeightBaseMult;
                    float3 hNorm = UnpackNormal(SAMPLE_TEXTURE2D(_HeightBumpMap, sampler_HeightMap, hUV));
                    
                    albedo = lerp(albedo, hCol, hMask);
                    normalTS = lerp(normalTS, hNorm, hMask);
                #endif

                #if defined(_TOPLAYER)
                    // RestPosWS is Absolute here, so no Camera offset needed
                    float3 noisePos = input.restPosWS * _TopNoiseScale;
                    float noiseVal = snoise(noisePos);
                    float nDotUp = dot(normalize(input.normalWS), float3(0,1,0));
                    float snowSignal = nDotUp + (noiseVal * 0.5);
                    float threshold = 1.0 - (clamp(_TopBlendMax,0,2) * 0.5);
                    float mask = saturate(smoothstep(threshold, threshold+0.2, snowSignal) * _TopOpacity);
                    float2 tUV = uv * float2(_TopRepeatU, _TopRepeatV);
                    float4 tCol = SAMPLE_TEXTURE2D(_TopMap, sampler_TopMap, tUV) * _TopColor * _TopBaseMult;
                    float3 tNorm = UnpackNormal(SAMPLE_TEXTURE2D(_TopBumpMap, sampler_TopMap, tUV));
                    albedo = lerp(albedo, tCol, mask);
                    normalTS = lerp(normalTS, tNorm, mask);
                #endif

                if (_WindL2AttachDebug > 2.5 && input.windError > 0.0001)
                {
                    float errorMask = saturate(input.windError * 4.0);
                    albedo.rgb = lerp(float3(0.0, 1.0, 0.0), float3(1.0, 0.0, 1.0), errorMask);
                    normalTS = float3(0.0, 0.0, 1.0);
                }

                float3 worldNormal;
                if (_Billboard > 0.5) {
                    worldNormal = normalize(input.normalWS);
                } else {
                    // Simple Tangent Space Unpacking for lightweight Forward Lit override
                    float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                    worldNormal = normalTS.x * input.tangentWS.xyz + normalTS.y * bitangent + normalTS.z * input.normalWS;
                    worldNormal = normalize(worldNormal);
                    if (!isFrontFace) worldNormal = -worldNormal;
                }

                // Grab the actual main directional light from HDRP's light buffer
                float3 realSunDir = normalize(float3(0.5, 1.0, 0.25)); // Fallback
                float3 realSunColor = float3(1.0, 1.0, 1.0);           // Fallback
                
                if (_DirectionalLightCount > 0)
                {
                    // _DirectionalLightDatas[0].forward points ALONG the light ray. 
                    // We negate it to get the vector pointing TOWARDS the light source.
                    realSunDir = normalize(-_DirectionalLightDatas[0].forward.xyz);
                    
                    // HDRP lights use physical Lux (e.g. 100,000 intensity). 
                    // We must normalize this to extract ONLY the color tint, 
                    // otherwise the tree becomes a massive lightbulb and obliterates shadows!
                    float3 rawColor = _DirectionalLightDatas[0].color;
                    float maxIntensity = max(rawColor.r, max(rawColor.g, rawColor.b));
                    realSunColor = rawColor / max(maxIntensity, 0.0001);
                }

                float NdotL = saturate(dot(worldNormal, realSunDir));
                
                // Multiply by realSunColor so the tree respects sunset/sunrise tinting
                float3 lighting = albedo.rgb * ((realSunColor * NdotL) + 0.3); 
                
                FragOutput output;
                output.color = float4(lighting, albedo.a);
                return output;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // SHADOW CASTER PASS
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _GROW_ON
            #pragma shader_feature_local _BILLBOARD
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            struct Varyings_Shadow
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float growthBirth   : TEXCOORD1;
                float growthOpen    : TEXCOORD2;
            };

            Varyings_Shadow ShadowPassVertex(Attributes_Tree input)
            {
                Varyings_Shadow output = (Varyings_Shadow)0;
                
                output.growthBirth = 1.0;
                output.growthOpen = 1.0;
                float dummyAttachError = 0.0;
                float3 posOS = input.positionOS.xyz;

                #if !defined(_BILLBOARD)
                    #if defined(_ALPHATEST_ON)
                        output.growthBirth = min(saturate(input.texcoord3.x), 0.9999);
                        output.growthOpen = 1.0;
                    #else
                        ApplyGrowth(posOS, input, output.growthBirth, output.growthOpen);
                    #endif
                #endif

                #if defined(_BILLBOARD) || !defined(_WIND_ON)
                    posOS = posOS;
                #else
                    // FIX 1: Pass the normal and tangent to fix the compile error
                    posOS = GetWindOffset(posOS, input.normalOS, input.tangentOS, input, dummyAttachError);
                #endif

                // In HDRP, TransformObjectToWorld gives Camera Relative WS. TransformWorldToHClip expects Camera Relative WS.
                float3 posRWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(posRWS);
                
                // FIX 2: Clamp the clip space Z coordinate to the near plane to prevent cascade clipping
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.uv = input.uv;
                
                return output;
            }

            void ShadowPassFragment(Varyings_Shadow input)
            {
                #ifdef LOD_FADE_CROSSFADE
                    float fade = unity_LODFade.x < 0.0 ? 1.0 + unity_LODFade.x : unity_LODFade.x;
                    clip(fade - InterleavedGradientNoise(input.positionCS.xy, 0));
                #endif

                #if !defined(_BILLBOARD)
                    if (_UseGrow > 0.5)
                    {
                        float phase = saturate(_GrowPhase);
                        if (phase <= 0.0001) { clip(-1.0); }
                        else if (phase < 0.9999) {
                            clip(phase - input.growthBirth);
                            clip(input.growthOpen - max(_GrowMaskCutoff, 0.001));
                        }
                    }
                #endif

                if (_AlphaCutoffEnable > 0.5) 
                {
                    float2 uv = input.uv;
                    uv.x += _Twist * uv.y;
                    float2 baseUV = uv * float2(_UTiling, _VTiling);
                    float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV).a * _BaseColor.a;
                    clip(alpha - _Cutoff);
                }
            }
            ENDHLSL
        }
    }
}