Shader "DeadEndy/BrilhoColetavel"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        ZWrite Off
        ZTest LEqual
        Cull Off
        Blend SrcAlpha One
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Vertice { float3 posicao : POSITION; float2 uv : TEXCOORD0; };
            struct Dados { float4 posicao : SV_POSITION; float2 uv : TEXCOORD0; };

            Dados vert(Vertice v)
            {
                Dados o;
                o.posicao = TransformObjectToHClip(v.posicao);
                o.uv = v.uv;
                return o;
            }

            float Cintilar(float2 p, float raio)
            {
                p = abs(p) / raio;
                float raios = min(p.x * 6 + p.y, p.x + p.y * 6);
                return 1 - smoothstep(0, 1, min(raios, length(p) * 2.7));
            }

            half4 frag(Dados i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float pulso = 0.5 + 0.5 * sin(_Time.y * 2.7);
                float halo = saturate(1 - length(p) * 2.1);
                float brilho = halo * halo * (0.2 + 0.17 * pulso);
                float estrela = Cintilar(p - float2(0.20, 0.19), 0.10 + 0.10 * pulso) * pulso;
                estrela += Cintilar(p - float2(-0.23, 0.07), 0.14) * (1 - pulso);
                return half4(lerp(half3(1.0, 0.59, 0.16), half3(1.6, 1.25, 0.75), estrela),
                    saturate(brilho + estrela));
            }
            ENDHLSL
        }
    }
}
