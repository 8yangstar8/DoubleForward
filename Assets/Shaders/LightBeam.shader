Shader "DoubleForward/LightBeam"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Beam Color", Color) = (1, 0.95, 0.6, 0.8)
        _CoreColor ("Core Color", Color) = (1, 1, 0.9, 1)
        _Intensity ("Intensity", Range(0, 5)) = 2.0
        _Width ("Width", Range(0, 2)) = 0.3
        _Speed ("Scroll Speed", Range(0, 10)) = 2.0
        _Falloff ("Edge Falloff", Range(0.1, 5)) = 2.0
        _Flicker ("Flicker Amount", Range(0, 0.5)) = 0.1
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
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _CoreColor;
                half _Intensity;
                half _Width;
                half _Speed;
                half _Falloff;
                half _Flicker;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                uv.x += _Time.y * _Speed;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                float distFromCenter = abs(IN.uv.y - 0.5) * 2.0;
                float edgeFade = 1.0 - pow(distFromCenter, _Falloff);
                edgeFade = saturate(edgeFade);

                float lengthFade = 1.0 - IN.uv.x;
                lengthFade = saturate(lengthFade);

                float flicker = 1.0 - _Flicker * sin(_Time.w * 13.7 + IN.uv.x * 5.0);

                float coreGlow = smoothstep(_Width, 0.0, distFromCenter);
                half4 color = lerp(_Color, _CoreColor, coreGlow);

                color.a *= edgeFade * lengthFade * flicker * _Intensity * tex.a;
                color.rgb *= _Intensity * flicker;

                return color * IN.color;
            }
            ENDHLSL
        }
    }
}
