Shader "DeadEndy/Pedra Caverna Triplanar"
{
    Properties
    {
        _BaseMap("Textura de pedra", 2D) = "white" {}
        [Normal] _BumpMap("Relevo da pedra", 2D) = "bump" {}
        _BaseColor("Cor", Color) = (0.57,0.57,0.57,1)
        _TileSize("Tamanho da textura em metros", Float) = 42
        _BumpScale("Intensidade do relevo", Range(0,2)) = 0.65
        _Smoothness("Brilho", Range(0,1)) = 0.08
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _TileSize;
                half _BumpScale, _Smoothness, _Cull, _Cutoff;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionWS = p.positionWS; o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, o.lightmapUV);
                OUTPUT_SH(o.normalWS, o.vertexSH);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n = normalize(i.normalWS);
                half3 weights = pow(abs(n), 4); weights /= max(dot(weights, half3(1,1,1)), .0001);
                float3 uv = i.positionWS / max(_TileSize, .1);
                half3 color = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv.zy).rgb * weights.x
                    + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv.xz).rgb * weights.y
                    + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv.xy).rgb * weights.z;
                // Retain the stone pattern while removing the brown tint from the source texture.
                half stoneVariation = clamp(dot(color, half3(.2126,.7152,.0722)) / .18, .6, 1.4);
                color = stoneVariation.xxx;
                half3 nx = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv.zy));
                half3 ny = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv.xz));
                half3 nz = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv.xy));
                half3 detail = half3(0,nx.y,nx.x)*weights.x + half3(ny.x,0,ny.y)*weights.y + half3(nz.x,nz.y,0)*weights.z;
                n = normalize(n + detail * _BumpScale);
                InputData input = (InputData)0;
                input.positionWS = i.positionWS; input.normalWS = n;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.bakedGI = SAMPLE_GI(i.lightmapUV, i.vertexSH, n);
                input.vertexLighting = VertexLighting(i.positionWS,n);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = SAMPLE_SHADOWMASK(i.lightmapUV);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = color * _BaseColor.rgb; surface.alpha = 1;
                surface.metallic = 0; surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1); surface.occlusion = 1;
                half4 lit = UniversalFragmentPBR(input,surface);
                lit.rgb = MixFog(lit.rgb,i.fog);
                return lit;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
