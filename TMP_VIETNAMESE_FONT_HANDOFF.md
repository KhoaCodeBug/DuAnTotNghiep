# Vietnamese TextMesh Pro font handoff

## Scope completed

- Replaced the runtime-generated `VietnameseDynamic SDF` workflow with `Vietnamese Static SDF` while preserving its asset GUID.
- Baked one 2048x2048 static SDF atlas from the project's Liberation Sans source: 592 characters, all required Vietnamese precomposed characters, Vietnamese combining marks, and TMP UI punctuation/symbols including `…`.
- Set the static asset as the TMP default font and serialized a one-way fallback from the legacy `LiberationSans SDF` asset.
- Removed runtime fallback-list mutation and the 0.4-second localization polling/font assignment.
- Kept localization refresh event-driven through language changes and scene loads.
- Replaced unsupported decorative TMP symbols with baked equivalents. Symbols used only by IMGUI/debug logs were not changed.
- Did not restore or overwrite the existing H1-H2 scene, quest, test, or handoff changes.

## Expected versus actual

| Area | Expected | Actual |
| --- | --- | --- |
| Static atlas | No runtime glyph generation; complete Vietnamese | Static, single atlas; required Vietnamese missing: 0 |
| Main Menu | Vietnamese accents and menu punctuation render without missing-glyph warnings | MainMenu loaded in Play Mode; no TMP font-asset warnings after the final bake |
| Gameplay HUD | Vietnamese action/progress text stays on the same deterministic asset | Main/HUD initialized in SOLO regression; no font warnings. The broad test later stopped on an unrelated 0.000015 px layout tolerance |
| Journal | Vietnamese Route B/H2 journal survives scene and quest transitions | Full Vietnamese Route B regression passed through extraction |
| Host | Host-authoritative flow uses the same static asset | Fusion Single/Host authority initialized and Route B regression passed |
| Client | Client rendering must not add glyphs or alter fallbacks | Host/Client label probe passed after 0.65 seconds; a real second-process client was not available in this session |

## Automated verification

- Unity script compilation: no C# errors.
- `VietnameseStaticFontTests`: 3/3 passed.
- `VietnameseFontRuntimeTests.HostAndClientFontProbesDoNotMutateStaticAtlasAfterLegacyRefreshWindow`: passed.
- `MainMenuToMilitaryQuestFlowTests.RouteBDebugFlowRunsFromMainMenuThroughMilitaryExtractionWithoutLootContainers`: passed after adding the required ellipsis glyph.
- `MainMenuToMilitaryQuestFlowTests.SoloMenuFlowLoadsMainAndSpawnsMilitaryQuestWithoutModalOverlap`: reached Main, Host authority and gameplay HUD, then failed on an unrelated existing layout assertion (`208.500015 <= 208.5`).

## Cách test thực tế trong game

1. Open `MainMenu`, set Language to Vietnamese, and visit New Game, Multiplayer, Options, difficulty selection, Host setup, Join setup, and waiting room.
2. Start a SOLO game, inspect the status HUD/action bar, open Inventory/Health, then press `J` to open Journal and `M` for the mission map.
3. Advance Route B/H2 normally or with the existing debug flow; inspect every quest toast, journal objective, Radio-room text, and military extraction UI.
4. Use ParrelSync or one standalone build plus Editor: Host a Vietnamese room, join from the client, switch language independently on each process, and inspect waiting-room rows, chat, HUD, Journal, and quest notifications on both screens.
5. Keep the Console filtered for `font asset`, `glyph`, and `Unicode` while moving between scenes and switching language repeatedly.

## Kết quả mong đợi

- All Vietnamese tone marks stay attached to the correct base glyph on both processes.
- `…`, `•`, arrows, filled/empty squares, and dropdown triangles render consistently.
- No `character ... is not available in font asset` warning appears.
- `Vietnamese Static SDF` remains `Static`, one atlas, and its character-table count remains 592 after play.
- Host and client may choose different local languages without changing the shared font asset or network state.

## Next plan

1. Run the two-process ParrelSync visual pass above; capture one MainMenu/waiting-room and one Journal/HUD screenshot from each process.
2. Repeat on a second Windows machine or a clean standalone build to confirm identical atlas/material import.
3. Fix the unrelated inventory-layout float tolerance in a separate change so the broad SOLO regression can return green without mixing it into the font fix.
