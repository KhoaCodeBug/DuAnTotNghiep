# RUNTIME ROUTING EVIDENCE & LIMITATIONS LEDGER

- **Context:** Unity 6000.0.69f1 | Photon Fusion 2 Host Mode
- **Test Session Job IDs:** EditMode `1e9aa64923ca48d2b7fd26cce72d1ee1` | PlayMode `2d7520805be94cb3b57d7b08f13fa51e`

---

## 1. Honest Evidence & Verification Status

| Scenario / Check | Evidence Available | Classification |
| :--- | :--- | :--- |
| **`[RpcTarget]` Unicast Compilation** | Reflection test `ZombieCorpseLoot_RPC_ShowSearchResult_UsesRpcTargetAndCorrectSignature` in `ReadinessAndChatEditorTests.cs` (145/145 pass) | **PASS (STATIC/CODEGEN)** |
| **Actor (Peer A) Local Chat Notification** | Single local call `AutoChatManager.Instance.AddSystemMessage` when `Runner.LocalPlayer == recipient` | **PASS (LOCAL PRESENTATION)** |
| **Peer B/C BoxChat Non-Display** | Filter `if (Runner.LocalPlayer != recipient) return;` prevents local UI execution | **PASS (PRESENTATION ONLY)** |
| **Peer B/C Transport Payload Exclusion** | Requires physical Wireshark/packet capture or instrumented dual-process ParrelSync receiver log | **UNVERIFIED (DUAL-PEER CAPTURE PENDING)** |
| **Corpse Visual State Replication** | Driven by `[Networked] HasCorpseBeenSearched`. Statically verified in code; live multi-GUI capture pending | **UNVERIFIED (LIVE MULTI-GUI PENDING)** |
| **Simultaneous Corpse Search Race** | Code ensures single `ConsumeCorpse()` call on State Authority; specific standalone test pending | **UNVERIFIED (STANDALONE TEST PENDING)** |

---

## 2. Explanation of Environment Limitations

1. **Unity MCP Bridge Limitation:**
   - The current Unity MCP bridge communicates with a single Unity Editor instance (PID 11748).
   - Simulating two live GUI processes simultaneously with independent packet-level inspection requires multi-instance network test harness or external Wireshark capture.
2. **Static vs Runtime Boundary:**
   - Photon Fusion 2's IL Weaver compiles `[RpcTarget]` into a unicast RPC header, which is standard engine behavior for `[RpcTarget]`.
   - However, without active packet interception logs on a secondary process, we honestly keep the transport-level check marked as `UNVERIFIED` until multi-process packet capture is executed.
