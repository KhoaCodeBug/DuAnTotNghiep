Shader "Hidden/IndoorFogSurfaceAtlas"
{
    Properties { _MainTex ("Surface alpha", 2D) = "white" {} }
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off Blend One OneMinusSrcAlpha
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
                // Integrate sprite alpha over the atlas texel when baking. A single
                // binary sample permanently quantizes a diagonal wall into atlas-sized
                // squares; bilinear sampling later cannot recover its true boundary.
                float2 dx = ddx(input.uv), dy = ddy(input.uv);
                float coverage = 0;
                [unroll] for (int y = 0; y < 4; y++)
                [unroll] for (int x = 0; x < 4; x++)
                {
                    float2 uv = input.uv + dx * ((x + 0.5) / 4.0 - 0.5) + dy * ((y + 0.5) / 4.0 - 0.5);
                    coverage += step(0.4, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a);
                }
                coverage *= 1.0 / 16.0;
                clip(coverage - 0.0001);
                float groundY = min(input.world.y, input.foot.y);
                // RGHalf payload: projected ground Y, then authored coverage. Projected
                // X always equals the screen pixel's world X and need not consume a channel.
                // Premultiplied composition preserves the underlying surface through
                // partially covered texels; the overlay normalizes R by G on decode.
                return float4((groundY - _AtlasBounds.y) / _AtlasBounds.w * coverage, coverage, 0, coverage);
            }
            ENDHLSL
        }
    }
}
