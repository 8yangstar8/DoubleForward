Shader "DoubleForward/LightBridge"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Bridge Color", Color) = (0.8, 0.9, 1, 0.7)
        _GlowColor ("Glow Color", Color) = (0.6, 0.8, 1, 1)
        _Speed ("Flow Speed", Range(0, 10)) = 3.0
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 1.5
        _FadeProgress ("Fade Progress", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha One
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
                half4 _GlowColor;
                half _Speed;
                half _GlowIntensity;
                half _FadeProgress;
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
                uv.x += _Time.y * _Speed;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                float edgeFade = 1.0 - abs(IN.uv.y - 0.5) * 2.0;
                edgeFade = smoothstep(0.0, 0.3, edgeFade);

                float scanline = sin(IN.uv.x * 20.0 + _Time.y * _Speed * 2.0) * 0.5 + 0.5;

                half4 color = lerp(_Color, _GlowColor, scanline * 0.5);
                color.rgb *= _GlowIntensity;
                color.a *= edgeFade * tex.a * (1.0 - _FadeProgress);

                return color;
            }
            ENDHLSL
        }
    }
}
