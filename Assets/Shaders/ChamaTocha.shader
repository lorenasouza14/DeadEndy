Shader "DeadEndy/ChamaTocha"
{
    Properties
    {
        _BaseColor("Cor", Color) = (1,1,1,1)
        _Forma("Chama (0) ou brasa (1)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Forma;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float y = input.uv.y;
                float curva = sin(y * 10 - _Time.y * 8) * 0.06 * y;
                float largura = 0.43 * pow(saturate(1-y), 0.72);
                float borda = 1-smoothstep(largura * 0.65, largura, abs(input.uv.x - 0.5 - curva));
                float chama = borda * smoothstep(0, 0.12, y) * (1-smoothstep(0.83, 1, y));
                float brasa = 1-smoothstep(0.15, 0.5, length(input.uv - 0.5));
                half3 quente = lerp(half3(1.7, 1.1, 0.18), half3(1.1, 0.12, 0.015), y);
                return half4(input.color.rgb * lerp(quente, half3(1,1,1), _Forma),
                    input.color.a * lerp(chama, brasa, _Forma));
            }
            ENDHLSL
        }
    }
}
