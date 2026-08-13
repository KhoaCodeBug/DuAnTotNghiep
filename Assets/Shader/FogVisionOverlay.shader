Shader "ProjectZomboid/FogVisionOverlay"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (0.72, 0.75, 0.77, 1)
        _IndoorAmbientColor ("Indoor Ambient Color", Color) = (0.025, 0.03, 0.04, 1)
        _IndoorExteriorColor ("Indoor Exterior Color", Color) = (0.008, 0.01, 0.014, 1)
        _FogDensity ("Fog Density", Range(0, 1)) = 0.8
        _FogBankTex ("Fog Bank Density", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float4 _IndoorAmbientColor;
                float4 _IndoorExteriorColor;
                float _FogDensity;
                float _FogDayPhase;
                float2 _FogSeed;
                float _PlayerBubbleClearance;
                float _PlayerBubbleRadius;
                float2 _VisionWorldCenter;
                float2 _VisionDirection;
                float _VisionCosHalfAngle;
                float _VisionEdgeSoftness;
                float _FlashlightActive;
                float _FlashlightClearance;
                float _FlashlightRadius;
                float _FlashlightIllumination;
                float _IndoorActive;
                float _IndoorPointCount;
                float4 _IndoorPoints[16];
                float _IndoorAmbientOpacity;
                float _IndoorExteriorOpacity;
                float _IndoorExitAwarenessClearance;
                float _IndoorExitAwarenessRadius;
                float _IndoorExteriorFlashlightClearance;
                float2 _FogWorldBottomLeft;
                float2 _FogWorldRight;
                float2 _FogWorldUp;
            CBUFFER_END

            TEXTURE2D(_FogBankTex);
            SAMPLER(sampler_FogBankTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float FogField(float2 worldPosition)
            {
                // Each layer completes a whole tiled cycle in one game day. Because the
                // phase is derived from Fusion time, all clients see the same drift and
                // the day rollover has no visual pop.
                float2 broadUvA = worldPosition * 0.012 + _FogSeed * 0.011 + _FogDayPhase * float2(1.0, -1.0);
                float2 broadUvB = worldPosition * float2(-0.019, 0.016) + _FogSeed * 0.019 + _FogDayPhase * float2(-2.0, 1.0);
                float2 detailUv = worldPosition * float2(0.036, -0.032) + _FogSeed * 0.031 + _FogDayPhase * float2(3.0, -2.0);

                float broadA = SAMPLE_TEXTURE2D(_FogBankTex, sampler_FogBankTex, broadUvA).r;
                float broadB = SAMPLE_TEXTURE2D(_FogBankTex, sampler_FogBankTex, broadUvB).r;
                float detail = SAMPLE_TEXTURE2D(_FogBankTex, sampler_FogBankTex, detailUv).r;

                // The base haze never drops to zero. Texture only modulates it, avoiding
                // separate smoke puffs and making the whole landscape lose contrast.
                // More contrast between banks gives fog recognisable volume, while the
                // non-zero floor keeps every bank connected to the same atmosphere.
                float broad = smoothstep(0.16, 0.80, broadA * 0.62 + broadB * 0.38);
                float softDetail = smoothstep(0.18, 0.78, detail);
                return lerp(0.38, 1.0, saturate(broad * 0.72 + softDetail * 0.28));
            }

            float IsInsideIndoorPolygon(float2 worldPosition)
            {
                float inside = 0.0;
                int pointCount = (int)_IndoorPointCount;

                [unroll]
                for (int i = 0; i < 16; i++)
                {
                    if (i >= pointCount) break;

                    int previousIndex = i == 0 ? pointCount - 1 : i - 1;
                    float2 current = _IndoorPoints[i].xy;
                    float2 previous = _IndoorPoints[previousIndex].xy;
                    bool crossesEdge = (current.y > worldPosition.y) != (previous.y > worldPosition.y);
                    // A horizontal edge cannot cross the ray, so crossesEdge guarantees
                    // this denominator is non-zero and preserves its required sign.
                    float edgeX = (previous.x - current.x) * (worldPosition.y - current.y) /
                                  (previous.y - current.y) + current.x;

                    if (crossesEdge && worldPosition.x < edgeX)
                        inside = 1.0 - inside;
                }

                return inside;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 worldPosition = _FogWorldBottomLeft + input.uv.x * _FogWorldRight + input.uv.y * _FogWorldUp;
                float2 offsetFromPlayer = worldPosition - _VisionWorldCenter;
                float distanceFromPlayer = length(offsetFromPlayer);
                float2 directionToPixel = distanceFromPlayer > 0.0001 ? offsetFromPlayer / distanceFromPlayer : _VisionDirection;

                float angleDot = dot(directionToPixel, normalize(_VisionDirection));
                // A deliberately broad angular feather prevents the hard, fake
                // vision border that is most visible at night.
                float coneFeather = max(_VisionEdgeSoftness, 0.20);
                float rawConeVisibility = smoothstep(_VisionCosHalfAngle - coneFeather,
                                                     _VisionCosHalfAngle + coneFeather,
                                                     angleDot);
                float coneVisibility = rawConeVisibility;
                coneVisibility *= 1.0 - smoothstep(_PlayerBubbleRadius * 0.55, _PlayerBubbleRadius, distanceFromPlayer);

                float flashlightReach = 1.0 - smoothstep(_FlashlightRadius * 0.34,
                                                          _FlashlightRadius * 1.08,
                                                          distanceFromPlayer);
                float flashlightVisibility = rawConeVisibility * flashlightReach * _FlashlightActive;

                float insideIndoor = IsInsideIndoorPolygon(worldPosition);
                if (_IndoorActive > 0.5)
                {
                    float indoorOpacity = lerp(_IndoorExteriorOpacity, _IndoorAmbientOpacity, insideIndoor);
                    float3 indoorColor = lerp(_IndoorExteriorColor.rgb, _IndoorAmbientColor.rgb, insideIndoor);
                    // Do not turn the exterior into a 96%-opaque black wall as
                    // soon as the player crosses an indoor trigger. A small soft
                    // area around the player keeps doorways and exits navigable.
                    float exitAwareness = 1.0 - smoothstep(_IndoorExitAwarenessRadius * 0.28,
                                                            _IndoorExitAwarenessRadius,
                                                            distanceFromPlayer);
                    float exteriorMask = 1.0 - insideIndoor;
                    indoorOpacity *= 1.0 - exitAwareness * exteriorMask * _IndoorExitAwarenessClearance;

                    // The real illumination is a URP Light2D. This mask only lets
                    // that light remain visible through doors by opening the dark
                    // indoor-exterior cover along the same softly feathered cone.
                    float indoorFlashlight = flashlightVisibility * insideIndoor;
                    float exteriorFlashlight = flashlightVisibility * exteriorMask;
                    indoorOpacity *= 1.0 - coneVisibility * insideIndoor * 0.16;
                    indoorOpacity *= 1.0 - indoorFlashlight * _FlashlightClearance;
                    indoorOpacity *= 1.0 - exteriorFlashlight * _IndoorExteriorFlashlightClearance;

                    // A tiny warm tint blends fog color with the Light2D, but is
                    // intentionally too weak to act as a fake light overlay.
                    float3 flashlightFogTint = float3(0.22, 0.20, 0.15);
                    indoorColor = lerp(indoorColor, flashlightFogTint,
                                       flashlightVisibility * _FlashlightIllumination);
                    return half4(indoorColor, saturate(indoorOpacity));
                }

                float bubbleVisibility = 1.0 - smoothstep(_PlayerBubbleRadius * 0.30,
                                                           _PlayerBubbleRadius * 1.28,
                                                           distanceFromPlayer);
                float localDensity = FogField(worldPosition);
                float opacity = _FogDensity * localDensity;
                // A circular awareness bubble only thins fog. It never creates a clean
                // directional wedge, so looking around cannot erase weather.
                opacity *= 1.0 - bubbleVisibility * _PlayerBubbleClearance;

                // Flashlight does not cut a perfectly clean wedge through the
                // weather. Both the cone and its reach fade gradually, leaving
                // a believable thin haze at the edge.
                opacity *= 1.0 - flashlightVisibility * _FlashlightClearance;

                float3 fogColor = _FogColor.rgb * lerp(0.90, 1.05, localDensity);
                float3 outdoorFlashlightTint = float3(0.42, 0.45, 0.43);
                fogColor = lerp(fogColor, outdoorFlashlightTint,
                                flashlightVisibility * _FlashlightIllumination * 0.48);
                return half4(fogColor, saturate(opacity));
            }
            ENDHLSL
        }
    }
}
