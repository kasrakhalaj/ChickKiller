// Unlit sprite shader for the 2D URP renderer with two extras:
//  _FlashAmount / _FlashColor : lerps the sprite toward a solid colour (white hit flash), driven per
//                               renderer through a MaterialPropertyBlock by SpriteFlasher.
//  _Intensity                 : HDR multiplier. Values > 1 push projectiles / effects above the bloom
//                               threshold so ONLY they glow (chickens and backgrounds stay crisp).
Shader "ChickenShooter/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        _Intensity ("HDR Intensity", Range(0, 8)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _FlashColor;
                half _FlashAmount;
                half _Intensity;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                // Unity 6 sprite batching passes SpriteRenderer.color and flipX/flipY per draw
                // (unity_SpriteColor / unity_SpriteProps) instead of baking them into the mesh.
                float3 pos = float3(v.positionOS.xy * unity_SpriteProps.xy, v.positionOS.z);
                o.positionCS = TransformObjectToHClip(pos);
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _FlashAmount);
                c.rgb *= _Intensity;
                return c;
            }
            ENDHLSL
        }
    }
}
