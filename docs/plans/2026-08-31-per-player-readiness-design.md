# Per-Player Multiplayer Readiness Design

## Understanding summary

- Multiplayer loading must be local to each player: a ready player enters gameplay immediately.
- A slow player remains on loading and enters later without blocking ready players.
- Fusion remains host-authoritative; clients may report readiness but cannot authorize their own gameplay entry.
- Local readiness requires an authoritative difficulty, a spawned local `PlayerObject`, avatar binding, and HUD/systems readiness.
- Early or reordered network release signals must be retained until local readiness is safe.
- Loading failures must remain explicit and must never be converted into gameplay release.
- Backpack/map rewards, quests, scenes, prefabs, and unrelated gameplay are out of scope.

## Assumptions and non-functional requirements

- Architecture remains Fusion host mode for cooperative multiplayer.
- Supported room size stays at the current project limit; the change adds one readiness RPC per player and no per-frame network traffic.
- A ready player should close loading immediately, apart from the existing short progress/fade animation.
- The host authenticates the reporting `PlayerRef`; spoofed readiness reports remain rejected.
- Duplicate readiness reports are idempotent.
- The existing 35-second local failure path remains as a final safety limit; it is not used to delay a valid release.
- Maintenance ownership stays in `HostModeSpawner`, `AutoMainMenuManager`, and `GameplayReadinessCoordinator`.

## Final design

`HostModeSpawner` keeps the authoritative ready-player set. When a player first reports readiness, the host records that player, marks `IsMatchStarted` when the first ready player arrives, and sends a targeted release RPC only to that authenticated player. The initial host follows the same targeted path. Later players use the same method; there is no separate room barrier or ten-second readiness watchdog.

`AutoMainMenuManager` treats a host release as a durable local signal. `ForceCloseLoadingScreen` records the signal even if the local readiness stage is still early. A condition-based check releases gameplay as soon as the stage reaches `HUDAndSystemsReady` or `AwaitingHostRelease`. A `Failed` state is terminal and never releases.

The global `IsMatchStarted` state remains for gameplay/late-join behavior, but it no longer means every connected player is ready. It means at least one authenticated player has entered the running match.

## Error handling and edge cases

- Early RPC: retained and consumed when local readiness becomes safe.
- Duplicate ready report: ignored by the ready set and does not duplicate release/announcement.
- Spoofed `PlayerRef`: rejected by the existing authority validation.
- Runner disappears or authoritative difficulty is unavailable: transition to `Failed` with a visible reason.
- Player disconnects while loading: remove readiness state; no other player is blocked.
- Solo: uses the same targeted release behavior without a room barrier.

## Testing strategy

- EditMode regression tests for targeted per-player release and removal of the all-room barrier.
- EditMode state test proving an early release stays pending and is consumed only at a safe local stage.
- Existing readiness, authority, solo, and late-join tests remain green.
- Full EditMode and PlayMode suites, Unity compile/Console inspection, and a runtime loading screenshot are required before completion.

## Decision log

1. Chosen: per-player targeted readiness. Rejected: all-player barrier and immediate scene-only release. Reason: avoids unnecessary waiting while preserving local safety.
2. Chosen: host-authoritative targeted RPC. Rejected: client self-release. Reason: prevents spoofed readiness and keeps network authority intact.
3. Chosen: durable pending release signal. Rejected: one-shot deferred call. Reason: Fusion ordering can deliver the host signal before local UI readiness.
4. Chosen: one shared path for initial and late players. Rejected: separate timeout/late-join release paths. Reason: fewer timing branches and easier regression testing.
