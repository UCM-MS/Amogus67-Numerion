# Shared Unity PC + Web protocol · 2 November 2026

Unity Windows and WebGL use the same `OnlineAcademyClient` and `/api/unity` HTTP API. This revision is prepared locally; it has not been published to the existing public site. `/classic` and its existing APIs are preserved.

The normal **Hrať spolu** panel creates or joins a six-character room, with up to 16 players. Native players use `UnityWebRequest`; browsers use their page's origin. State is stored in D1 in deployment or SQLite in the local preview. There is no UDP or Durable Object dependency for this path and no claim of a measured 16-device session.

Clients publish presence at **5 Hz**, independently of rendering. The server rejects non-finite values, world-bound violations, old sequence numbers and implausible displacement. This HTTP path validates snapshots; it does not simulate controller physics as the native UDP path below does. Peers interpolate visible transforms. An absent player expires after 15 seconds. Spawn/course/checkpoint requests use approved world positions. Six shared beacon bits require proximity to registered positions.

Duels require a challenge and explicit acceptance. Each player commits one hidden action per round; the server controls the 60-second deadline, resources, switches, knockouts and winner. Questions cover grades 7–9 and six mathematics strands. Public question fields include `grade`, integer `subject` 0–5, readable `topic`, prompt and shuffled options. The answer key and solution appear only after evaluation. Client-supplied `Correct` or `Topic` values are never trusted. QuickStrike requires no question and grants no mathematical artifact or participation credit.

Action IDs are `battleId:round:side`. Compare-and-swap battle revisions prevent concurrent overwrite. Correct answers produce private stable artifact receipts; duplicates cannot spend resources or create another receipt. A mathematically participating winner receives 60 XP, 100 coins and 40 pet XP. A unique server ledger and persistent client receipts guard repeated snapshot awards. Online pet base stats are normalized; artifact bonuses apply only to training. Cloud save JSON is client-owned game progress, not a ranked economy or verified identity.

Cloud profiles use a UUID and random 256-bit bearer secret; the server stores its hash. Roster responses omit the secret, save JSON and private party metadata. **Spoločný profil a prenos postupu** reveals the full pairing code only on request. Anyone holding it can load the game profile: share room codes with friends, not profile codes. Profile revisions reject stale overwrite. Loading or pairing preserves a local backup and is blocked while joined to a room.

Follow [the local server guide](../../lumen/UNITY-ONLINE-LOCAL.md): `PORT=4174`, optional `HOST=0.0.0.0` for LAN, and native argument `-lumen-online-origin http://127.0.0.1:4174`. The service client permits HTTP only on loopback/private IPv4 addresses, with HTTPS elsewhere. The builder allows these local HTTP requests. HTTPS Web pages still obey browser mixed-content restrictions. `-lumen-profile-suffix hrac1` and `hrac2` isolate native saves and cloud credentials.

Server checks are `node tests/unity-online.test.mjs` and `node tests/unity-battle-conformance.test.mjs` from `work/lumen`. The online client API check compiles current core and client code against installed Unity assemblies. Full native RPC compilation additionally requires imported package assemblies and NGO post-processing. These checks do not replace a newly built PC-to-browser play session.

On 2 November 2026, `node scripts/unity-online-http-smoke.mjs http://127.0.0.1:4174` passed 107 checks against the actual local preview: independent profiles, optimistic conflict, shared movement/appearance, approved respawn, a complete grade-nine pet duel, stable reward and artifact receipts, and the preserved `/classic` route. The runner prints no bearer secrets and disconnects its test players. `tests/unity-online-timeouts.test.mjs` separately passed 62 injected-time checks preventing an expired round from committing a phantom command into the next round.

## Native UDP LAN path · historical implementation and checks

The section below records the Unity 6.3 / NGO 2.7.0 path and October 2026 validation. This remains a separate native transport. The current project targets Unity 6000.6.6f1 with NGO 2.13.3 and protocol `LUMEN-NATIVE-2` after adding grade to quiz RPCs; binary players must be rebuilt together. The method is now `BeginQuiz(subject, ageBand, grade = 7)`, preserving two-argument source calls. Selection validates grade 7–9. Earlier age-only content and session-only identity descriptions below concern the older build, not the new HTTP cloud service.

# Native multiplayer · October 2026 record

LUMEN uses Unity Netcode for GameObjects **2.7.0** and Unity Transport. A real host or dedicated server simulates character movement at **60 fixed steps per second**. `NetworkConfig.TickRate` is also **60 Hz**. The lobby accepts up to **16 players**; this is a configured capacity, not a completed 16-device performance benchmark.

## Play together

1. Build the same project version on each computer. Connect the computers to the same LAN.
2. In the multiplayer menu choose **Host**. The default port is **7777/UDP**.
3. Other players enter the host computer's LAN IPv4 address and the same port, then choose **Join**. `127.0.0.1` is only for two processes on one computer.
4. Permit the game through the host computer's firewall for the intended network. This project does not silently change firewall rules.

Internet direct connections require a reachable public endpoint and appropriate router/firewall configuration. There is no bundled relay, automatic NAT traversal, cloud account service or password authentication. Do not treat an anonymous LAN nickname as a verified account identity.

## Dedicated server

The same desktop player can run headlessly:

```powershell
.\LumenAcademy.exe -batchmode -nographics -lumen-server -port 7777 -logFile server.log
```

For a host or a client launched from a terminal:

```powershell
.\LumenAcademy.exe -lumen-host -port 7777 -name "Ada"
.\LumenAcademy.exe -lumen-connect 192.168.1.25 -port 7777 -name "Niko"
```

`NetworkSession.TryStartFromCommandLine()` must be called after the procedural world and colliders exist. The runtime entry point handles this; the session then defers the actual connection by one frame so NGO's generated `AfterSceneLoad` message registration has completed. Starting NGO directly from the scene's `Awake` is too early. A dedicated server still needs colliders, even when it skips all renderers.

## Simulation and validation

- Owner clients send normalized input at 60 Hz using an owner-only unreliable RPC. Monotonic sequence numbers reject duplicate and reordered commands. A small repeat window protects jump presses against single packet loss.
- The server validates finite axes/yaw, normalizes direction, rate-limits commands, bounds its queue, and advances **at most one input per 1/60 s physics step**. Extra input packets cannot increase speed. It computes gravity, collisions, grounded jumps, sprint speed and world bounds itself; clients never submit positions.
- Each authoritative fixed step broadcasts an unreliable snapshot with a server tick, server time and acknowledged input sequence. Owners rewind and replay pending input; other characters use a 100 ms interpolation buffer. Old snapshots are rejected explicitly. No `NetworkTransform` should be attached alongside this custom movement synchronizer.
- Names are sanitized on both ends, preserve whole Unicode text elements, exclude controls/markup, and are limited to 20 elements / 100 UTF-8 bytes. The server resolves visible duplicates. Registered appearance indices are clamped; names and appearance replicate through server-write-only variables.
- Co-op beacons use registered world coordinates, a server-side range check, a server-owned bit mask and a request cooldown. A beacon can be collected once per session; team shards, contributors and collection announcements replicate, including initial progress for late joiners.
- Parkour starts and respawns use registered checkpoint foot positions. The server tracks checkpoint visits from its own grounded character positions, accepts returns only to already visited checkpoints, and times course completion itself. Clients cannot submit arbitrary teleport destinations.
- Quiz requests send only a subject and age cohort. The cohort is kept private on the server. The server's `LearningService` selects and evaluates the active question, rejects duplicate awards, and controls session XP. The wire question omits its answer key and explanation until evaluation. Quiz RPCs are rate-limited.

## Progress and identity

Online quiz XP and correct-answer counts belong to the current anonymous server session. They reset when the session is destroyed. The authoritative server does **not** accept client-supplied XP, currency, attempts or mastery as trusted progression. Local solo saves remain separate from this server score. Persistent authenticated profiles, cross-server rankings and reconnect recovery would require a real identity/storage service.

## Prefab and runtime integration

The source includes `Assets/Lumen/Resources/Generated/Explorer.prefab` and its stable `.meta`, plus the stable `NetworkExplorer.cs.meta`. Keep these files together. The NetworkObject script GUID (`d5a57f767e5e46a458fc5d3c628d0cbb`) was verified from Unity's exact NGO **2.7.0** registry package, SHA-1 `bd2a018756edad4b0a685634ffde855b1207c37e`.

The imported source prefab has `GlobalObjectIdHash: 1820835490`, calculated and saved by NGO's real Editor `NetworkObject.OnValidate()` from its stable GlobalObjectId. No invented runtime hash is supplied. Before distributing a player build, run **LUMEN → Prepare Academy**, save the asset, and verify that the serialized hash remains nonzero. Preserve the prefab GUID and component file IDs across builds. For an immediate LAN session, distribute the same complete player build folder to every participant.

The Editor builder must save an actual prefab asset containing `NetworkObject`, `CharacterController` and `NetworkExplorer`. This gives `NetworkObject` a stable prefab hash. Runtime-only `AddComponent<NetworkObject>()` on an arbitrary player is insufficient. `NetworkSession.Configure(prefab, 60, spawnOrigin)` registers this saved prefab before listening; all builds must contain the same asset and components. Procedural-world scene management is disabled, so use the same world configuration in all builds.

Main integration points:

```csharp
session.Configure(prefab, 60, new Vector3(0, 0, 12));
session.ConfigureBeacons(fixedWorldBeaconPositions);
session.ConfigureCourse(fixedCourseCheckpointFootPositions);
session.StartHost(profile.DisplayName, appearance, 7777);
session.Join("192.168.1.25", profile.DisplayName, appearance, 7777);
session.LocalPlayer?.SetInput(move, cameraYaw, jumpPressed, sprintHeld);
session.LocalPlayer?.TryCollectBeacon(beaconIndex);
session.LocalPlayer?.BeginQuiz(subject, profile.AgeBand);
session.LocalPlayer?.SubmitQuizAnswer(question.Id, optionIndex);
session.LocalPlayer?.StartCourse();
session.LocalPlayer?.ReturnToCheckpoint(session.LocalPlayer.CourseCheckpoint);
session.LocalPlayer?.ResetToSpawn();
```

`NetworkExplorer.Spawned` / `Despawned` initialize and release world visuals. `AppearanceChanged` refreshes those visuals. `NetworkSession.LocalPlayerReady` selects the locally owned camera target. `QuestionReceived`, `QuizAnswerReceived` and `QuizError` drive the online question UI. Session properties expose actual connection state, player count, configured tick rate and transport RTT.

## Validation

EditMode tests cover replay/wrap ordering, malformed motion, Unicode/markup handling and appearance bounds. The actual Unity import has compiled the networking assembly and NGO RPC post-processing successfully; Editor validation saved the stable prefab hash. Native packet/runtime checks use an opt-in two-process smoke runner:

```powershell
.\LumenAcademy.exe -batchmode -nographics -lumen-smoke host -lumen-host -name SmokeHost -port 17877 -logFile host-smoke.log
.\LumenAcademy.exe -batchmode -nographics -lumen-smoke client -lumen-connect 127.0.0.1 -name SmokeGuest -port 17877 -logFile client-smoke.log
```

Launch these in separate processes, with the client starting within a few seconds. The runner checks replicated names/appearance, actual movement of both characters, measured authoritative ticks, server quiz XP, duplicate reward rejection, beacon range validation, fixed checkpoint teleports and client disconnect propagation. Each process emits `LUMEN_SMOKE_PASS` and exits0 only after its checks succeed; failure emits `LUMEN_SMOKE_FAIL` and exits1, with a 55 second deadline. The switch runs a dedicated test flow and does not load or save the user's local profile. Without `-lumen-smoke`, the runner is inactive. Completing these two-process checks does not imply a tested 16-player capacity.

On **18 October 2026**, the actual Unity 6000.3.26f1 Windows build passed this two-process test over local UDP port 17877. Both processes exited **0**, both emitted `LUMEN_SMOKE_PASS`, and measured authoritative rates were **59.46 Hz** on the host and **59.74 Hz** on the client. Evidence is saved in `Artifacts/host-smoke-3.log`, `Artifacts/client-smoke-3.log` and `Artifacts/network-smoke-3-result.json`. This verifies two native processes on one computer; multiple physical LAN devices and internet routing remain untested.

Official references:

- [NGO 2.7 RPCs](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/advanced-topics/message-system/rpc.html)
- [RpcAttribute permissions and delivery](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/api/Unity.Netcode.RpcAttribute.html)
- [NetworkConfig / tick rate](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/api/Unity.Netcode.NetworkConfig.html)
- [Unity Transport direct connection and RTT](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/api/Unity.Netcode.Transports.UTP.UnityTransport.html)
