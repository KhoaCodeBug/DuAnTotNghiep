# SETUP, MULTI-PEER ARCHITECTURE & BLOCKER ANALYSIS

- **Timestamp:** 2026-08-29 19:17:30 +07:00
- **Audited Target:** Multi-peer runtime verification of `ZombieCorpseLoot.RPC_ShowSearchResult([RpcTarget] PlayerRef recipient, ...)`

---

## 1. Multi-Peer Setup Configuration

1. **Primary Project:**
   - Path: `E:\Unity\GameObject\Game3D\ProJectZomboiNhai`
   - Active PID: `11748`
   - Role: State Authority / Host Mode Runner
2. **ParrelSync Clone:**
   - Path: `E:\Unity\GameObject\Game3D\ProJectZomboiNhai_clone_0`
   - Role: Intended Client B / Observer Peer

---

## 2. Identified Technical Blockers for Live Dual-GUI Multi-Peer Testing

1. **Single MCP HTTP Bridge Limitation:**
   - The current `com.coplaydev.unity-mcp` package runs an embedded HTTP server on a single local port inside the active Editor process (PID 11748).
   - MCP does not currently support simultaneous control or bidirectional event polling across two separate Unity Editor windows at the same time.
2. **Absence of Active Second Editor Process:**
   - Operating system inspection confirms only one instance of `Unity.exe` (PID 11748) is running.
   - Launching a second Unity instance on the ParrelSync clone requires substantial system memory and independent user GUI focus, which cannot be programmatically interacted with or logged via the primary MCP bridge.
3. **Transport-Level Capture Constraint:**
   - Without a running second client process or an active packet sniffer (e.g. Wireshark socket capture on Photon Fusion UDP ports), packet reception on Peer B/C cannot be directly proven at the OS socket layer in this single-process environment.

---

## 3. Impact on Verification Status

- **Static Analysis & IL Weaver Codegen:** **PASS (STATIC/CODEGEN)** (Fully proven through source audit, IL Weaving compile, and reflection tests).
- **Presentation Layer (UI Filter):** **PASS (PRESENTATION ONLY)** (Actor sees message; non-actor UI is filtered).
- **Transport-Level Socket Payload on Peer B/C:** **UNVERIFIED (DUAL-PEER CAPTURE PENDING)** (Honest status due to single-Editor environment constraint).
