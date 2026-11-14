Shader "Kraken/IndicadorSuave"
{
    Properties
    {
        _BaseColor("Cor", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
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
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;output.color=input.color;return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float borda=saturate(1.0-abs(input.uv.y*2.0-1.0));
                float distancia=input.uv.y*2.0-1.0;
                float suave=exp(-4.0*distancia*distancia)*smoothstep(0.0,0.18,borda);
                half4 cor=_BaseColor*input.color;
                cor.a*=suave*0.72;
                return cor;
            }
            ENDHLSL
        }
    }
}
