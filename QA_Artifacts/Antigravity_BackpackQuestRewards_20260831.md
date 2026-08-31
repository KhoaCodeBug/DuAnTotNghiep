# Antigravity task prompt — Backpack quest milestone rewards

## Objective

Implement the backpack quest milestone feature in the Unity project. The exact requested progression is:

1. After the player reaches the hospital and the server starts the hospital objective, award and auto-equip Backpack Level 4.
2. After the hospital quest is completed (`IsCityMapUnlocked`) and the player's authoritative avatar enters `KhuVucQuanSu`, award and auto-equip Backpack Level 5.

This is not a generic reward for equipping or looting a tier-4/tier-5 backpack. Backpack Levels 1–3 keep their existing capacity-only behavior. Map fragment/map-unlock rewards must remain separate and must not be reused by this backpack reward.

## Multiplayer rules

- State Authority validates the milestone, player identity, alive/player-object state, and physical trigger/area position.
- Capacity, equipped backpack level, and one-time claim state are authoritative and replicated to the owning client.
- Each player claims Level 4 and Level 5 independently; duplicate RPCs, repeated trigger polling, respawn, reconnect, and late join must not duplicate or downgrade the reward.
- The reveal UI/RPC is targeted to the qualifying player's Input Authority only. Do not broadcast another player's personal reward overlay.
- Preserve the existing map/quest network state and existing loot backpack equip validation.

## Scope

Expected areas to inspect/change only as needed:

- `Assets/Script/Tin/InventorySystem.cs`
- `Assets/Script/Tin/ItemData.cs` / `BackpackItemCatalog`
- `Assets/Script/Tin/MainQuest/MainQuestManager.cs`
- `Assets/Script/Tin/MainQuest/MainQuestStartTrigger.cs`
- `Assets/Script/Tin/MainQuest/MilitaryBaseQuestManager.cs`
- independent backpack reward presentation/localization files under `Assets/Script/Tin/`
- `Assets/Resources/Backpacks/BackpackLevel1.png` through `BackpackLevel5.png`
- focused EditMode/PlayMode tests under `Assets/Script/Tin/Prototype/Tests/`

Do not edit unrelated scenes/prefabs, delete data, couple the backpack presenter to `QuestMapUIPrototype`, or replace existing map rewards. Preserve old serialized fields/API compatibility and use the existing procedural icon as a fallback when a PNG cannot load.

## UX/art requirements

- Use a separate full-screen scan/pulse/core reveal that feels consistent with map reveal timing and tactical survival styling.
- Show the awarded backpack icon, level number, capacity result, and a clear milestone title (hospital or military base).
- Create five consistent transparent-background inventory icons with readable silhouettes and no text/watermark; use distinct silhouette upgrades and restrained tactical color accents.

## Acceptance criteria

- Hospital entry grants exactly Level 4 once per qualifying player.
- Military-zone entry after hospital completion grants exactly Level 5 once per qualifying player.
- No Level 4/5 reward is granted by ordinary loot/equip calls.
- Host/client state remains consistent and late join/respawn cannot duplicate rewards.
- Map fragment and map unlock behavior remains unchanged.
- EditMode and PlayMode tests pass; Unity compiles without new errors; report changed files, test names/results, console state, and screenshots.

Return a concise list of changed files, tests run, and any blocker. Do not claim to have changed files outside this scope.
