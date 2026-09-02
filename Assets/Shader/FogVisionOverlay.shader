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
        [HideInInspector] _IndoorSurfaceAtlas ("Indoor Surface Projection", 2D) = "black" {}
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
                float _IndoorSurfaceActive;
                float4 _IndoorSurfaceBounds;
                float4 _IndoorSurfaceLighting;
                float _IndoorSurfaceProbe;
                float _IndoorPointCount;
                float4 _IndoorPoints[16];
                float _IndoorAmbientOpacity;
                float _IndoorExteriorOpacity;
                float _IndoorExitAwarenessClearance;
                float _IndoorExitAwarenessRadius;
                float _IndoorExteriorFlashlightClearance;
                float _IndoorOcclusionActive;
                float _IndoorOcclusionRayCount;
                float _IndoorOcclusionDistances[180];
                float2 _IndoorOcclusionOrigin;
                float _IndoorOcclusionEdgeSoftness;
                float _IndoorWallOccludedOpacity;
                float _IndoorFlashlightBoundaryFade;
                float _IndoorShadowEdgeCount;
                float4 _IndoorShadowEdges[32];
                float4 _IndoorShadowEdgeMeta[32];
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

            TEXTURE2D(_FogBankTex);
            SAMPLER(sampler_FogBankTex);
            TEXTURE2D(_IndoorSurfaceAtlas);
            SAMPLER(sampler_IndoorSurfaceAtlas);

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

            float IndoorOcclusionVisibility(float2 worldPosition, float distanceInset, float surfaceProjection)
            {
                if (_IndoorOcclusionActive < 0.5 || _IndoorOcclusionRayCount < 2.0)
                    return 1.0;

                // Distances belong to the origin used by the last physics scan.
                // Keeping this origin paired with its samples prevents the mask from
                // sliding against walls while the Player moves between scan updates.
                float2 offsetFromScan = worldPosition - _IndoorOcclusionOrigin;
                float distanceFromScan = length(offsetFromScan);
                float2 directionToPixel = distanceFromScan > 0.0001
                    ? offsetFromScan / distanceFromScan : float2(1, 0);
                float angle = atan2(directionToPixel.y, directionToPixel.x);
                if (angle < 0.0) angle += 6.28318530718;
                float samplePosition = angle * _IndoorOcclusionRayCount / 6.28318530718;
                int firstIndex = (int)floor(samplePosition);
                int secondIndex = firstIndex + 1;
                if (secondIndex >= (int)_IndoorOcclusionRayCount) secondIndex = 0;
                float feather = max(_IndoorOcclusionEdgeSoftness, 0.01);
                float testedDistance = max(0, distanceFromScan - distanceInset);
                float firstVisibility = 1.0 - smoothstep(
                    _IndoorOcclusionDistances[firstIndex] - feather,
                    _IndoorOcclusionDistances[firstIndex] + feather, testedDistance);
                float secondVisibility = 1.0 - smoothstep(
                    _IndoorOcclusionDistances[secondIndex] - feather,
                    _IndoorOcclusionDistances[secondIndex] + feather, testedDistance);
                if (surfaceProjection > 0.5)
                {
                    // Only configured static wall/decor pixels receive angular
                    // reconstruction. A non-negative cubic B-spline suppresses an
                    // isolated ray flip and makes the transition C1-continuous,
                    // without opening floor/actors behind the wall.
                    int rayCount = (int)_IndoorOcclusionRayCount;
                    int previousIndex = firstIndex > 0 ? firstIndex - 1 : rayCount - 1;
                    int nextIndex = secondIndex + 1 < rayCount ? secondIndex + 1 : 0;
                    float previousVisibility = 1.0 - smoothstep(
                        _IndoorOcclusionDistances[previousIndex] - feather,
                        _IndoorOcclusionDistances[previousIndex] + feather, testedDistance);
                    float nextVisibility = 1.0 - smoothstep(
                        _IndoorOcclusionDistances[nextIndex] - feather,
                        _IndoorOcclusionDistances[nextIndex] + feather, testedDistance);
                    float t = frac(samplePosition);
                    float t2 = t * t;
                    float t3 = t2 * t;
                    float4 weights = float4(
                        (1.0 - 3.0 * t + 3.0 * t2 - t3) / 6.0,
                        (4.0 - 6.0 * t2 + 3.0 * t3) / 6.0,
                        (1.0 + 3.0 * t + 3.0 * t2 - 3.0 * t3) / 6.0,
                        t3 / 6.0);
                    return dot(weights, float4(previousVisibility, firstVisibility,
                        secondVisibility, nextVisibility));
                }
                // Preserve the accepted world/floor occlusion shape. Interpolating
                // the hit distance remains strict for gameplay-bearing pixels.
                float wallDistance = lerp(_IndoorOcclusionDistances[firstIndex],
                    _IndoorOcclusionDistances[secondIndex], frac(samplePosition));
                return 1.0 - smoothstep(wallDistance - feather, wallDistance + feather,
                    testedDistance);
            }

            float IndoorShadowEdgeFade(float2 worldPosition)
            {
                if (_FlashlightActive < 0.5 || _IndoorFlashlightBoundaryFade <= 0.0)
                    return 0.0;
                float2 offset = worldPosition - _IndoorOcclusionOrigin;
                float fade = 0.0;
                [loop]
                for (int i = 0; i < (int)_IndoorShadowEdgeCount; i++)
                {
                    float4 edge = _IndoorShadowEdges[i];
                    float edgeWeight = saturate(_IndoorShadowEdgeMeta[i].x);
                    float along = dot(offset, edge.xy);
                    float side = (edge.x * offset.y - edge.y * offset.x) * sign(edge.w);
                    // Only the visible side beyond the near blocker is graded.
                    // Ordinary ray/wall contact is NOT a fade boundary.
                    float segment = smoothstep(edge.z, edge.z + 0.15, along);
                    // This path is evaluated on the projected surface position.
                    // Stop shortly after the far sampled surface instead of
                    // letting one discrete ray edge create an infinite protrusion.
                    float farPadding = max(0.22, _IndoorSurfaceProbe + 0.16);
                    segment *= 1.0 - smoothstep(abs(edge.w) + farPadding,
                        abs(edge.w) + farPadding + 0.22, along);
                    // Cover the narrow existing reconstruction band on either
                    // side without a sign cutoff (which produces a bright seam).
                    // The caller only raises opacity; accepted visibility is
                    // applied once by the final cover blend, never expanded.
                    float inward = 1.0 - smoothstep(0.0, _IndoorFlashlightBoundaryFade,
                        abs(side));
                    fade = max(fade, inward * segment * edgeWeight);
                }
                return fade;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 worldPosition = _FogWorldBottomLeft + input.uv.x * _FogWorldRight + input.uv.y * _FogWorldUp;
                float2 projectedSurfacePosition = worldPosition;
                float2 lightingPosition = worldPosition;
                float surfaceCoverage = 0;
                if (_IndoorActive > 0.5 && _IndoorSurfaceActive > 0.5)
                {
                    float2 atlasUv = (worldPosition - _IndoorSurfaceBounds.xy) / _IndoorSurfaceBounds.zw;
                    if (all(atlasUv >= 0) && all(atlasUv <= 1))
                    {
                        float4 filteredSurface = SAMPLE_TEXTURE2D(_IndoorSurfaceAtlas,
                            sampler_IndoorSurfaceAtlas, atlasUv);
                        // The atlas clears to transparent black, so bilinear RGB at a
                        // silhouette edge is alpha-weighted. Divide by alpha before
                        // decoding the projected coordinate; otherwise it is pulled
                        // toward the atlas origin and creates a large dark scallop.
                        float atlasAlpha = filteredSurface.g;
                        float projectedV = filteredSurface.r / max(atlasAlpha, 0.0001);
                        projectedSurfacePosition = float2(worldPosition.x,
                            _IndoorSurfaceBounds.y + projectedV * _IndoorSurfaceBounds.w);

                        // Resolve the authored alpha edge over roughly one screen pixel.
                        // This removes the visible atlas-texel staircase without changing
                        // ray count/update rate or widening the visibility mask by a world
                        // texel (which would leak into an adjacent room).
                        float alphaAa = max(fwidth(atlasAlpha), 0.0001);
                        surfaceCoverage = smoothstep(0.5 - alphaAa, 0.5 + alphaAa, atlasAlpha);
                        lightingPosition = lerp(worldPosition, projectedSurfacePosition,
                            surfaceCoverage);
                    }
                }
                float2 offsetFromPlayer = lightingPosition - _VisionWorldCenter;
                float distanceFromPlayer = length(offsetFromPlayer);
                float2 directionToPixel = distanceFromPlayer > 0.0001 ? offsetFromPlayer / distanceFromPlayer : _VisionDirection;
                float worldOcclusionVisibility = IndoorOcclusionVisibility(worldPosition, 0, 0);
                float projectedOcclusionVisibility = IndoorOcclusionVisibility(
                    projectedSurfacePosition, _IndoorSurfaceProbe, 1);
                // Projection may only repair visibility already accepted by the strict
                // world sample. Blend the repair result, rather than its input position,
                // so partially covered edge pixels cannot generate false ray distances.
                projectedOcclusionVisibility = max(projectedOcclusionVisibility,
                    worldOcclusionVisibility);
                float indoorOcclusionVisibility = lerp(worldOcclusionVisibility,
                    projectedOcclusionVisibility, surfaceCoverage);
                float originalInside = 0;
                if (_IndoorActive > 0.5 && _IndoorSurfaceActive > 0.5)
                {
                    originalInside = IsInsideIndoorPolygon(worldPosition);
                }

                float angleDot = dot(directionToPixel, normalize(_VisionDirection));
                // A deliberately broad angular feather prevents the hard, fake
                // vision border that is most visible at night.
                float coneFeather = max(_VisionEdgeSoftness, 0.20);
                float rawConeVisibility = smoothstep(_VisionCosHalfAngle - coneFeather,
                                                     _VisionCosHalfAngle + coneFeather,
                                                     angleDot);
                float coneVisibility = rawConeVisibility;
                coneVisibility *= 1.0 - smoothstep(_PlayerBubbleRadius * 0.55, _PlayerBubbleRadius, distanceFromPlayer);
                coneVisibility *= indoorOcclusionVisibility;

                float flashlightReach = 1.0 - smoothstep(_FlashlightRadius * 0.34,
                                                          _FlashlightRadius * 1.08,
                                                          distanceFromPlayer);
                float flashlightVisibility = rawConeVisibility * flashlightReach * _FlashlightActive * indoorOcclusionVisibility;

                float insideIndoor = IsInsideIndoorPolygon(worldPosition);
                if (_IndoorActive > 0.5 && _IndoorSurfaceActive > 0.5)
                {
                    insideIndoor = max(originalInside,
                        IsInsideIndoorPolygon(projectedSurfacePosition) * surfaceCoverage);
                    float visible = insideIndoor * indoorOcclusionVisibility;
                    // Grade flashlight intensity across the visible floor and projected art.
                    // Keep the accepted dark outer edge; start fading earlier INSIDE the
                    // beam instead of blurring occlusion or revealing an adjacent room.
                    float lightEdge = _VisionCosHalfAngle + _IndoorSurfaceLighting.z;
                    float lightCore = min(0.999, lightEdge + max(0.20, _IndoorSurfaceLighting.w));
                    float lightCone = smoothstep(lightEdge, lightCore, angleDot);
                    float illumination = lightCone * flashlightReach * _FlashlightActive;
                    float ambientOpacity = saturate(_IndoorSurfaceLighting.x + (1 - rawConeVisibility) * 0.12);
                    float surfaceOpacity = lerp(ambientOpacity, _IndoorSurfaceLighting.y, illumination);
                    // Preserve the existing inward cast-shadow grading. Reconstruct
                    // coordinates before evaluating it, then apply authored coverage
                    // to the result instead of interpolating a false ray position.
                    float flashlightBoundaryFade = lerp(IndoorShadowEdgeFade(worldPosition),
                        IndoorShadowEdgeFade(projectedSurfacePosition), surfaceCoverage) *
                        _FlashlightActive * lightCone;
                    surfaceOpacity = lerp(surfaceOpacity, _IndoorWallOccludedOpacity,
                        flashlightBoundaryFade);
                    float opacity = lerp(_IndoorExteriorOpacity, surfaceOpacity, visible);
                    opacity = max(opacity, (1 - indoorOcclusionVisibility) * _IndoorWallOccludedOpacity);
                    float exitAwareness = (1 - smoothstep(_IndoorExitAwarenessRadius * 0.28,
                        _IndoorExitAwarenessRadius, distanceFromPlayer)) * indoorOcclusionVisibility;
                    opacity *= 1 - exitAwareness * (1 - insideIndoor) * _IndoorExitAwarenessClearance;
                    opacity *= 1 - flashlightVisibility * (1 - insideIndoor) * _IndoorExteriorFlashlightClearance;
                    float3 color = lerp(_IndoorExteriorColor.rgb, _IndoorAmbientColor.rgb, visible);
                    return half4(color, saturate(opacity));
                }
                if (_IndoorActive > 0.5)
                {
                    float visibleIndoor = insideIndoor * indoorOcclusionVisibility;
                    float indoorOpacity = lerp(_IndoorExteriorOpacity, _IndoorAmbientOpacity, visibleIndoor);
                    float3 indoorColor = lerp(_IndoorExteriorColor.rgb, _IndoorAmbientColor.rgb, visibleIndoor);
                    // The fog cover is also the final guard against Light2D leakage.
                    // Rays through a real doorway remain unblocked; pixels behind a
                    // structural collider become effectively opaque.
                    indoorOpacity = max(indoorOpacity,
                        (1.0 - indoorOcclusionVisibility) * _IndoorWallOccludedOpacity);
                    // Do not turn the exterior into a 96%-opaque black wall as
                    // soon as the player crosses an indoor trigger. A small soft
                    // area around the player keeps doorways and exits navigable.
                    float exitAwareness = (1.0 - smoothstep(_IndoorExitAwarenessRadius * 0.28,
                                                            _IndoorExitAwarenessRadius,
                                                            distanceFromPlayer)) * indoorOcclusionVisibility;
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

                if (_QuestBoundaryActive > 0.5)
                {
                    float2 relative = worldPosition - _QuestBoundaryOrigin;
                    float determinant = _QuestBoundaryRight.x * _QuestBoundaryUp.y -
                                        _QuestBoundaryRight.y * _QuestBoundaryUp.x;
                    float safeDeterminant = abs(determinant) < 0.0001 ? 0.0001 : determinant;
                    float2 zonePosition = float2(
                        (relative.x * _QuestBoundaryUp.y - relative.y * _QuestBoundaryUp.x) / safeDeterminant,
                        (_QuestBoundaryRight.x * relative.y - _QuestBoundaryRight.y * relative.x) / safeDeterminant);

                    // Convert normalized overrun on either isometric axis back
                    // into an approximate world-space distance from its edge.
                    float area = abs(determinant);
                    float rightEdgeSpacing = area / max(length(_QuestBoundaryUp), 0.0001);
                    float upEdgeSpacing = area / max(length(_QuestBoundaryRight), 0.0001);
                    float outsideDistance = max(
                        max(max(-zonePosition.x, zonePosition.x - 1.0) * rightEdgeSpacing,
                            max(-zonePosition.y, zonePosition.y - 1.0) * upEdgeSpacing),
                        0.0);
                    float boundaryFog = smoothstep(0.0, max(_QuestBoundaryFade, 0.1), outsideDistance);
                    // Outside the allowed district becomes a continuous opaque
                    // fog wall. Do not modulate its alpha with the weather bank:
                    // that previously left clear pockets and a pale edge.
                    opacity = lerp(opacity, _QuestBoundaryOpacity, boundaryFog);
                    // Fade both colour and opacity from the existing weather fog.
                    // A hard colour step exposes the exact polygon edge; keeping
                    // both channels on the same curve removes that visible seam
                    // while still reaching an almost-black wall farther outside.
                    fogColor = lerp(fogColor, float3(0.004, 0.007, 0.009), boundaryFog);
                }
                return half4(fogColor, saturate(opacity));
            }
            ENDHLSL
        }
    }
}
