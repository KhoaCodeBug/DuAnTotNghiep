# Indoor Fog concept — exact imagegen prompts

Mode: built-in image_gen; no CLI/API fallback. Date: 2026-08-31.

## Flashlight ON

Use case: lighting-weather.
Asset type: faithful edited gameplay screenshot, visual target for an indoor fog-of-war prototype, NOT concept art of a redesigned game.
Input image 1 is the clean screenshot to edit. Input image 2 is the same scene with red marks indicating the problem surfaces only; do NOT copy the red marks.

Make one full-size landscape screenshot in the exact original camera view and original isometric 2D pixel-art style. FLASHLIGHT ON scenario.
Strictly preserve the room geometry, brick patterns, floor tiles, all furniture, shelves, paintings, blood, player position and facing, exterior, doorways, and HUD. Do not add lamps, windows, objects, characters, smoke, volumetric light shafts, gloss, or high-fidelity 3D details. No aesthetic restyling. Do not crop or rearrange the room. Keep the original wide aspect ratio, approximately 2.21:1.

Change indoor fog shading only. The player near the bottom center looks toward the far upper interior wall and shelving. The current error makes upper portions of the visible wall/shelves dark despite their being in sight:
1. Reveal the INWARD-facing brick wall above the television/pictures and along the shelves on the far upper-left-to-upper-center wall. The entire vertical face within the player's sight should show its existing brick/picture texture, with moderate natural light. Do not brighten the outer side of the wall or anything beyond it.
2. Reveal the full height of visible cabinet/shelf fronts facing the player, including the right-hand cabinet/counter at the edge of the lit area. Gradually reduce brightness across that cabinet toward the shadowed right-hand side instead of the original straight black diagonal cut through its front.
3. Keep a brighter broad flashlight region centered from the player toward the far shelving, but soften its angular edge. The wall and cabinet faces in that region are readable, not self-emissive. Preserve texture and muted brown/beige colors. Avoid washing the entire room with equal brightness or overexposing the floor.
4. Within the currently visible main room, peripheral surfaces receive much weaker ambient visibility and smooth falloff. Keep occluded spaces behind the right partition, the upper-right room, and outside the house dark, no x-ray views or reveal of hidden contents. Do not spread softness through walls.
5. Darkness on tall sprites must follow the visible surface, not be a ground-plane triangle cutting off the upper half.
The aim is achievable restrained stylized game lighting, not physically simulated bounce lighting. Keep pixel-art edges crisp; soften only illumination gradients, not image texture. No annotations, labels, captions, arrows or red circles. Original HUD unchanged.

## Flashlight OFF

Use case: lighting-weather.
Asset type: faithful alternate-lighting gameplay mockup, FLASHLIGHT OFF.
Input image is the approved-for-comparison draft FLASHLIGHT ON image, not a new scene.
Make the EXACT SAME wide landscape isometric 2D pixel-art screenshot with the flashlight switched OFF. Only change lighting on the already visible indoor surfaces. Keep every wall, floor, furniture item, shelf, picture, plant, blood stain, player, doorway, camera framing and HUD exactly in place. No text annotations, no red marks, no object additions, no new light sources, no restyling.
Remove the bright directional flashlight illumination. The visible main room must be significantly dimmer, approximately one-third of the apparent brightness of the input image, but still readable: the player can faintly see the inward-facing back brick wall, pictures, shelf fronts and right-hand cabinets in front of them. Preserve subtle natural gradients; do not create a sharp black diagonal cutting vertically across a visible cabinet face. The floor is also dim ambient light, no bright central pool or luminous beam. Gentle neutral-cool ambient tint appropriate to low-light indoor visibility, muted colors, readable outlines and details when looking closely, not total black. Keep occluded adjacent upper-right rooms and far sides of walls dark as in the input; do not reveal hidden geometry or hidden objects. Keep outside darkness and HUD brightness unchanged. Pixel texture stays crisp; only the illumination changes. This is a restrained visual target, no ambient glow, no volumetric haze, no dramatic bloom. Preserve approximately 2.21:1 aspect ratio.

