Shader "DeadEndy/PedraRunica"
{
    Properties
    {
        _BaseMap("Textura da pedra", 2D) = "white" {}
        _BaseColor("Cor da pedra", Color) = (1,1,1,1)
        _EmissionMap("Mascara do simbolo", 2D) = "black" {}
        [HDR] _EmissionColor("Brilho do simbolo", Color) = (0,0,0,1)
        _Metallic("Metal", Range(0,1)) = 0
        _Smoothness("Suavidade", Range(0,1)) = 0.2
        _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Normal scale", Float) = 1
        _OcclusionStrength("Occlusion", Range(0,1)) = 1
        _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Surface("Surface", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #define InitializeStandardLitSurfaceData InitializeOriginalStoneSurfaceData
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #undef InitializeStandardLitSurfaceData

            void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
            {
                InitializeOriginalStoneSurfaceData(uv, surface);
                half3 palette = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
                // A paleta possui simbolos vermelhos, azuis, etc. Seu maior canal
                // define a mascara sem cancelar ou misturar a cor escolhida no puzzle.
                half mask = max(palette.r, max(palette.g, palette.b));
                surface.emission = mask * _EmissionColor.rgb;
            }
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
