Shader "Kraken/BolhaSabaoURP"
{
    Properties { _Cor ("Cor", Color) = (0.65,0.92,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Cor;
            CBUFFER_END
            V Vert(A v)
            {
                V o; o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(v.normalOS); return o;
            }
            half4 Frag(V i) : SV_Target
            {
                float3 n=normalize(i.normalWS), view=normalize(_WorldSpaceCameraPos-i.positionWS);
                float borda=pow(1-saturate(dot(n,view)),2.4);
                float faixa=.5+.5*sin(dot(n,view)*20+_Time.y*.7+i.positionWS.y*.18);
                half3 arco=lerp(half3(.58,.9,1),half3(1,.66,.9),faixa);
                float brilho=pow(saturate(dot(n,normalize(float3(-.4,.8,.5)))),65);
                return half4(lerp(_Cor.rgb,arco,.8)+brilho*.9, .035+borda*.7+brilho*.35);
            }
            ENDHLSL
        }
    }
}
