Shader "DeadEndy/BolhaRonco"
{
    Properties { _BaseColor ("Cor", Color) = (0.72,0.91,1,0.7) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv; o.color = input.color; return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float r = length(p);
                float borda = smoothstep(0.72,0.86,r) * (1-smoothstep(0.89,0.99,r));
                float reflexo = (1-smoothstep(0.06,0.18,length(p-float2(-0.31,0.39)))) * 0.9;
                float interior = (1-smoothstep(0.8,0.98,r))*0.075;
                half4 cor = input.color * _BaseColor;
                cor.rgb = lerp(cor.rgb,half3(1,1,1),saturate(borda*0.4+reflexo));
                cor.a *= saturate(borda*0.8+reflexo+interior);
                return cor;
            }
            ENDHLSL
        }
    }
}
