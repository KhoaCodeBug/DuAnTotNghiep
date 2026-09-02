Shader "Hidden/IndoorFogSurfaceAtlas"
{
    Properties { _MainTex ("Surface alpha", 2D) = "white" {} }
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _AtlasBounds;
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; float2 foot : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; float2 foot : TEXCOORD2; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS).xy;
                output.foot = TransformObjectToWorld(float3(input.foot, 0)).xy;
                float2 clip = (output.world - _AtlasBounds.xy) / _AtlasBounds.zw * 2 - 1;
                #if UNITY_UV_STARTS_AT_TOP
                clip.y = -clip.y;
                #endif
                output.positionCS = float4(clip, 0, 1);
                output.uv = input.uv;
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a - 0.4);
                float groundY = min(input.world.y, input.foot.y);
                // RGHalf payload: projected ground Y, then authored coverage. Projected
                // X always equals the screen pixel's world X and need not consume a channel.
                return float4((groundY - _AtlasBounds.y) / _AtlasBounds.w, 1, 0, 0);
            }
            ENDHLSL
        }
    }
}
