# Live Host + Client loading evidence — room P1QA3

## Reproduction and fix

The first two-Editor room used Fusion `ConnectionTimeout=30`. Main scene deserialization
and integration took long enough under concurrent Editor load that the client was
disconnected with `OnDisconnectedFromServer: Timeout`; Host readiness remained at 1/2.

The Fusion connection timeout was increased to 120 seconds and the same flow was rerun
with the original project as Host and a ParrelSync clone as Client.

## Confirmed runtime evidence

The preserved Unity Editor log contains the following P1QA3 milestones:

- Client joined `P1QA3`; session reported `PlayerCount=2`, `MaxPlayers=4`.
- Both peers loaded config with `ConnectionTimeout:120.0`.
- Host session transitioned from `IsLocked=0, GameState=0` to
  `IsLocked=1, GameState=1` when the campaign began.
- Host logged: `[ĐIỂM DANH] Đã có 2/2 người tải xong Map.`
- Host and Client each logged: `=== LOADING HOÀN TẤT VÀ GIẢI PHÓNG GAMEPLAY ===`.
- Both Game views visibly reached controllable Main gameplay; observed Editor frame
  counters were approximately 36–38 FPS during this two-peer loading check.
- No `OnDisconnectedFromServer: Timeout` occurred in P1QA3 before the Editors were
  intentionally stopped.

Source at capture time: `%LOCALAPPDATA%/Unity/Editor/Editor.log`, notable lines
10637–12653 (Client) and 75680–77652 (Host). This file is machine-local; the durable
summary above records the relevant evidence without checking in unrelated Editor noise.

## Honest limits

This is a real two-peer loading/readiness verification. It is not evidence for:

- three-peer targeted corpse-loot transport privacy and simultaneous search race;
- Host/Client difficulty mismatch in both directions;
- 5–10 live peers, disconnect/death at the extraction boundary, or latency behavior;
- the 80–112 zombie horde performance target or a 60-minute soak.
