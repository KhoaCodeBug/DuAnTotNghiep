# P1/P2 continuation test results — 2026-08-29

## Fresh automated verification

- Unity: `6000.0.69f1`.
- EditMode: **146/146 passed**, 0 failed, 0 skipped, 3.6665s.
- PlayMode: **10/10 passed**, 0 failed, 0 skipped, 105.8037s.
- New regression: `FusionConnectionTimeout_CoversConcurrentMainSceneLoading`.
- Result files: `EditMode.xml`, `PlayMode.xml`.
- Full logs: `EditMode.log`, `PlayMode.log`.

The batch PlayMode log contains one Unity licensing-service message about an unavailable
access token. It did not fail compilation or any test. No `NullReferenceException`,
Fusion timeout, compiler error, or failed test was found in the fresh PlayMode run.

## Scope boundary

These tests verify rules and production-path regressions already represented in the
suite. They do not substitute for a live 5–10 human-peer readiness run, a three-peer
corpse-loot race/privacy capture, or an 80–112 zombie Profiler/60-minute soak run.
