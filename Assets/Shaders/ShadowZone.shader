Shader "DoubleForward/ShadowZone"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Shadow Color", Color) = (0.1, 0.05, 0.2, 0.6)
        _EdgeColor ("Edge Color", Color) = (0.3, 0.1, 0.5, 0.8)
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.5
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.15
        _DistortSpeed ("Distort Speed", Range(0, 5)) = 1.0
        _DistortAmount ("Distort Amount", Range(0, 0.1)) = 0.02
        _EdgeWidth ("Edge Width", Range(0, 0.5)) = 0.15
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _EdgeColor;
                half _PulseSpeed;
                half _PulseAmount;
                half _DistortSpeed;
                half _DistortAmount;
                half _EdgeWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                float2 center = float2(0.5, 0.5);
                float dist = distance(uv, center) * 2.0;

                uv.x += sin(uv.y * 10.0 + _Time.y * _DistortSpeed) * _DistortAmount;
                uv.y += cos(uv.x * 10.0 + _Time.y * _DistortSpeed * 0.7) * _DistortAmount;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float circleMask = 1.0 - smoothstep(0.8, 1.0, dist);
                float edgeMask = smoothstep(1.0 - _EdgeWidth, 1.0, dist) * circleMask;

                half4 color = lerp(_Color, _EdgeColor, edgeMask);
                color.a *= circleMask * pulse * tex.a;

                return color;
            }
            ENDHLSL
        }
    }
}
