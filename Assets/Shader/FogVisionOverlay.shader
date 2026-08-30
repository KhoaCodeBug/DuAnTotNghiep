Shader "ProjectZomboid/FogVisionOverlay"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (0.72, 0.75, 0.77, 1)
        _IndoorAmbientColor ("Indoor Ambient Color", Color) = (0.025, 0.03, 0.04, 1)
        _IndoorExteriorColor ("Indoor Exterior Color", Color) = (0.008, 0.01, 0.014, 1)
        _IndoorAmbientOpacity ("Indoor Ambient Opacity", Range(0, 1)) = 0.88
        _IndoorExteriorOpacity ("Indoor Exterior Opacity", Range(0, 1)) = 0.94
        _IndoorWallOccludedOpacity ("Indoor Wall Occluded Opacity", Range(0, 1)) = 1.0
        _IndoorOcclusionEdgeSoftness ("Indoor Occlusion Edge Softness", Range(0.01, 0.5)) = 0.08
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
                float4 _IndoorPoints[32];
                float4 _IndoorBounds;
                float _IndoorAmbientOpacity;
                float _IndoorExteriorOpacity;
                float _IndoorExitAwarenessClearance;
                float _IndoorExitAwarenessRadius;
                float _IndoorExteriorFlashlightClearance;
                float _IndoorOcclusionActive;
                float _IndoorOcclusionRayCount;
                float _IndoorOcclusionDistances[180];
                float _IndoorPortalDistances[180];
                float _IndoorOcclusionEdgeSoftness;
                float _IndoorWallOccludedOpacity;
                float _LineOfSightActive;
                float _LineOfSightRayCount;
                float _LineOfSightDistances[180];
                float _LineOfSightEdgeSoftness;
                float _LineOfSightBlockedOpacity;
                float _QuestBoundaryActive;
                float2 _QuestBoundaryOrigin;
                float2 _QuestBoundaryRight;
                float2 _QuestBoundaryUp;
                float _QuestBoundaryFade;
                float _QuestBoundaryOpacity;
                float2 _FogWorldBottomLeft;
                float2 _FogWorldRight;
                float2 _FogWorldUp;
            CBUFFER_END

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            Texture2D _FogBankTex;
            SamplerState sampler_FogBankTex;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 id = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float b = lerp(Hash21(id), Hash21(id + float2(1.0, 0.0)), f.x);
                float t = lerp(Hash21(id + float2(0.0, 1.0)), Hash21(id + float2(1.0, 1.0)), f.x);
                return lerp(b, t, f.y);
            }

            float FogField(float2 worldPosition)
            {
                float2 sampleA = (worldPosition + _FogSeed) * 0.085;
                float2 sampleB = (worldPosition - _FogSeed * 0.65) * 0.17;
                float2 sampleC = (worldPosition + float2(_FogSeed.y, -_FogSeed.x)) * 0.38;

                float broadA = ValueNoise(sampleA);
                float broadB = ValueNoise(sampleB);
                float detail = ValueNoise(sampleC);

                float broad = smoothstep(0.16, 0.80, broadA * 0.62 + broadB * 0.38);
                float softDetail = smoothstep(0.18, 0.78, detail);
                return lerp(0.38, 1.0, saturate(broad * 0.72 + softDetail * 0.28));
            }

            float IsInsideIndoorPolygon(float2 worldPosition)
            {
                int pointCount = (int)_IndoorPointCount;
                if (pointCount < 3)
                {
                    if (worldPosition.x >= _IndoorBounds.x && worldPosition.x <= _IndoorBounds.z &&
                        worldPosition.y >= _IndoorBounds.y && worldPosition.y <= _IndoorBounds.w)
                        return 1.0;
                    return 0.0;
                }

                float inside = 0.0;
                [unroll]
                for (int i = 0; i < 32; i++)
                {
                    if (i >= pointCount) break;

                    int previousIndex = i == 0 ? pointCount - 1 : i - 1;
                    float2 current = _IndoorPoints[i].xy;
                    float2 previous = _IndoorPoints[previousIndex].xy;
                    bool crossesEdge = (current.y > worldPosition.y) != (previous.y > worldPosition.y);
                    float edgeX = (previous.x - current.x) * (worldPosition.y - current.y) /
                                  (previous.y - current.y) + current.x;

                    if (crossesEdge && worldPosition.x < edgeX)
                        inside = 1.0 - inside;
                }

                return inside;
            }

            float RayFanDistance(float distances[180], float rayCount, float2 directionToPixel)
            {
                if (rayCount < 2.0) return 20.0;

                float angle = atan2(directionToPixel.y, directionToPixel.x);
                if (angle < 0.0) angle += 6.28318530718;
                float samplePosition = angle * rayCount / 6.28318530718;
                int firstIndex = (int)floor(samplePosition);
                int secondIndex = firstIndex + 1;
                if (secondIndex >= (int)rayCount) secondIndex = 0;

                float d1 = max(0.1, distances[firstIndex]);
                float d2 = max(0.1, distances[secondIndex]);
                float f = frac(samplePosition);

                float minD = min(d1, d2);
                float maxD = max(d1, d2);

                // Continuous wall surface: exact harmonic depth interpolation (1/d = lerp(1/d1, 1/d2, f))
                if (maxD < minD * 1.8 + 2.0)
                {
                    float denom = lerp(d2, d1, f);
                    return (denom > 0.001) ? (d1 * d2 / denom) : lerp(d1, d2, f);
                }

                // Depth discontinuity (corner occluder opening to distant background):
                if (d1 < d2)
                {
                    return (f < 0.05) ? d1 : d2;
                }
                else
                {
                    return (f > 0.95) ? d2 : d1;
                }
            }

            float IndoorOcclusionVisibility(float2 directionToPixel, float distanceFromPlayer)
            {
                if (_IndoorOcclusionActive < 0.5 || _IndoorOcclusionRayCount < 2.0)
                    return 1.0;

                float wallDistance = RayFanDistance(_IndoorOcclusionDistances, _IndoorOcclusionRayCount, directionToPixel);
                float feather = max(_IndoorOcclusionEdgeSoftness, 0.04);
                return 1.0 - smoothstep(wallDistance - feather, wallDistance + feather,
                                        distanceFromPlayer);
            }

            float LineOfSightVisibility(float2 directionToPixel, float distanceFromPlayer)
            {
                if (_LineOfSightActive < 0.5 || _LineOfSightRayCount < 2.0)
                    return 1.0;

                float blockerDistance = RayFanDistance(_LineOfSightDistances, _LineOfSightRayCount, directionToPixel);
                float feather = max(_LineOfSightEdgeSoftness, 0.04);
                return 1.0 - smoothstep(blockerDistance - feather,
                                        blockerDistance + feather,
                                        distanceFromPlayer);
            }

            float IndoorPortalVisibility(float2 directionToPixel, float distanceFromPlayer)
            {
                if (_IndoorOcclusionActive < 0.5 || _IndoorOcclusionRayCount < 2.0)
                    return 0.0;

                float angle = atan2(directionToPixel.y, directionToPixel.x);
                if (angle < 0.0) angle += 6.28318530718;
                float samplePosition = angle * _IndoorOcclusionRayCount / 6.28318530718;
                int firstIndex = (int)floor(samplePosition);
                int secondIndex = firstIndex + 1;
                if (secondIndex >= (int)_IndoorOcclusionRayCount) secondIndex = 0;

                float p1 = _IndoorPortalDistances[firstIndex];
                float p2 = _IndoorPortalDistances[secondIndex];
                float f = frac(samplePosition);

                if (p1 <= 0.05 && p2 <= 0.05) return 0.0;

                float portalDistance = lerp(p1, p2, f);
                if (portalDistance <= 0.05) return 0.0;

                float feather = max(_IndoorOcclusionEdgeSoftness * 1.5, 0.08);
                return 1.0 - smoothstep(portalDistance - feather, portalDistance + feather,
                                        distanceFromPlayer);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 worldPosition = _FogWorldBottomLeft + input.uv.x * _FogWorldRight + input.uv.y * _FogWorldUp;
                float2 offsetFromPlayer = worldPosition - _VisionWorldCenter;
                float distanceFromPlayer = length(offsetFromPlayer);
                float2 directionToPixel = distanceFromPlayer > 0.0001 ? offsetFromPlayer / distanceFromPlayer : _VisionDirection;
                float indoorOcclusionVisibility = IndoorOcclusionVisibility(directionToPixel, distanceFromPlayer);
                float lineOfSightVisibility = LineOfSightVisibility(directionToPixel, distanceFromPlayer);

                float angleDot = dot(directionToPixel, normalize(_VisionDirection));
                float coneFeather = max(_VisionEdgeSoftness, 0.20);
                float rawConeVisibility = smoothstep(_VisionCosHalfAngle - coneFeather,
                                                     _VisionCosHalfAngle + coneFeather,
                                                     angleDot);
                float coneVisibility = rawConeVisibility;
                coneVisibility *= 1.0 - smoothstep(_PlayerBubbleRadius * 0.55, _PlayerBubbleRadius, distanceFromPlayer);
                coneVisibility *= indoorOcclusionVisibility;

                float flashlightReach = 1.0 - smoothstep(_FlashlightRadius * 0.40,
                                                          _FlashlightRadius,
                                                          distanceFromPlayer);
                float flashlightVisibility = rawConeVisibility * flashlightReach * _FlashlightActive * indoorOcclusionVisibility;

                float insideIndoor = IsInsideIndoorPolygon(worldPosition);
                if (_IndoorActive > 0.5)
                {
                    float visibleIndoor = insideIndoor * indoorOcclusionVisibility;
                    float indoorPortalVisibility = IndoorPortalVisibility(directionToPixel, distanceFromPlayer);
                    float exteriorPortalVisibility = indoorPortalVisibility * (1.0 - insideIndoor);

                    float indoorBubble = 1.0 - smoothstep(_PlayerBubbleRadius * 0.35, _PlayerBubbleRadius, distanceFromPlayer);

                    float indoorFlashlight = flashlightVisibility * visibleIndoor;
                    float exteriorFlashlight = flashlightVisibility * exteriorPortalVisibility;

                    float indoorClearance = saturate(indoorBubble * _PlayerBubbleClearance * insideIndoor + indoorFlashlight * _FlashlightClearance);
                    float indoorOpacity = _IndoorAmbientOpacity * (1.0 - indoorClearance);

                    // Doorway Portal exterior reveal:
                    // When a door is open, the open aperture reveals the exterior hallway/yard completely clear
                    float portalLight = exteriorPortalVisibility;
                    float exteriorOpacity = lerp(_IndoorExteriorOpacity, 0.0, portalLight);

                    float indoorFinalOpacity = lerp(_IndoorWallOccludedOpacity, indoorOpacity, visibleIndoor);
                    float exteriorFinalOpacity = lerp(_IndoorWallOccludedOpacity, exteriorOpacity, exteriorPortalVisibility);
                    float finalOpacity = lerp(exteriorFinalOpacity, indoorFinalOpacity, insideIndoor);

                    float3 indoorColor = lerp(_IndoorExteriorColor.rgb, _IndoorAmbientColor.rgb, insideIndoor);
                    float3 flashlightFogTint = float3(0.24, 0.22, 0.16);
                    float flashlightColorVisibility = flashlightVisibility *
                        (insideIndoor + exteriorPortalVisibility);
                    indoorColor = lerp(indoorColor, flashlightFogTint,
                                       saturate(flashlightColorVisibility) * _FlashlightIllumination);

                    return half4(indoorColor, saturate(finalOpacity));
                }

                float bubbleVisibility = 1.0 - smoothstep(_PlayerBubbleRadius * 0.30,
                                                           _PlayerBubbleRadius * 1.28,
                                                           distanceFromPlayer);
                float localDensity = FogField(worldPosition);
                float opacity = _FogDensity * localDensity;
                opacity *= 1.0 - bubbleVisibility * _PlayerBubbleClearance;
                opacity *= 1.0 - flashlightVisibility * _FlashlightClearance;

                float blockedVisibility = saturate(1.0 - lineOfSightVisibility);
                opacity = max(opacity,
                              lerp(opacity, _LineOfSightBlockedOpacity, blockedVisibility));

                float3 fogColor = _FogColor.rgb * lerp(0.90, 1.05, localDensity);
                float3 outdoorFlashlightTint = float3(0.42, 0.45, 0.43);
                fogColor = lerp(fogColor, outdoorFlashlightTint,
                                flashlightVisibility * _FlashlightIllumination * 0.48);

                float3 blindSpotColor = float3(0.01, 0.012, 0.016);
                fogColor = lerp(fogColor, blindSpotColor, blockedVisibility);

                if (_QuestBoundaryActive > 0.5)
                {
                    float2 relative = worldPosition - _QuestBoundaryOrigin;
                    float determinant = _QuestBoundaryRight.x * _QuestBoundaryUp.y -
                                        _QuestBoundaryRight.y * _QuestBoundaryUp.x;
                    float safeDeterminant = abs(determinant) < 0.0001 ? 0.0001 : determinant;
                    float2 zonePosition = float2(
                        (relative.x * _QuestBoundaryUp.y - relative.y * _QuestBoundaryUp.x) / safeDeterminant,
                        (_QuestBoundaryRight.x * relative.y - _QuestBoundaryRight.y * relative.x) / safeDeterminant);

                    float area = abs(determinant);
                    float rightEdgeSpacing = area / max(length(_QuestBoundaryUp), 0.0001);
                    float upEdgeSpacing = area / max(length(_QuestBoundaryRight), 0.0001);
                    float outsideDistance = max(
                        max(max(-zonePosition.x, zonePosition.x - 1.0) * rightEdgeSpacing,
                            max(-zonePosition.y, zonePosition.y - 1.0) * upEdgeSpacing),
                        0.0);
                    float boundaryFog = smoothstep(0.0, max(_QuestBoundaryFade, 0.1), outsideDistance);
                    opacity = lerp(opacity, _QuestBoundaryOpacity, boundaryFog);
                    fogColor = lerp(fogColor, float3(0.004, 0.007, 0.009), boundaryFog);
                }
                return half4(fogColor, saturate(opacity));
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
