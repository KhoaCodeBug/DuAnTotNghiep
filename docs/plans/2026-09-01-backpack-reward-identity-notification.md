# Backpack reward identity notification

## Scope

Fix the post-reward Notification A so the player can identify the backpack received and understand why it was awarded. Keep Effect B purely visual and preserve the existing reward order, capacity rules, and warning-cleanup changes.

## Expected files

- `Assets/Script/Tin/BackpackQuestRewardPresentation.cs`
- `Assets/Script/Tin/GameLocalization.cs`
- `Assets/Script/Tin/Prototype/Tests/Editor/BackpackRewardCombinationBATests.cs`
- `Assets/Script/Tin/Prototype/Tests/PlayMode/PlayModeBackpackVisualCaptureTests.cs`

## Acceptance criteria

1. During Effect B there are no active text labels or notification HUD.
2. After Effect B, Notification A shows the actual localized backpack display name, the milestone reason, and the correct capacity transition.
3. Notification A stays compact, wraps/ellipsizes safely, and does not overflow at 720p or 1080p.
4. Level 5 still follows map-fragment reward -> map reveal -> map close -> backpack Effect B -> notification.
5. EditMode, PlayMode, compile/Console checks, and fresh runtime screenshots provide evidence.

## Verification

- Run focused EditMode reward tests, then the full EditMode suite.
- Run the deterministic PlayMode backpack capture flow and inspect the new notification screenshots.
- Check Unity compile state and Console for errors/warnings.
- Review the diff and commit only the scoped changes on a new `codex/` branch; do not push or merge without approval.
