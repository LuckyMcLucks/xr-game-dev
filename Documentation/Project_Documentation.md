# XR Horror Escape-Room — Project Documentation

**Project root:** `d:\unity_projects\xr-game-dev`
**Engine:** Unity 6000.6.2f1 (Unity 6) · URP
**XR stack:** XR Interaction Toolkit 3.6.0 · Input System (only) · OpenXR 1.18.0 · Meta XR SDK 207.0.0 · glTFast

This document explains **what the game is**, **how each mechanic works**, **how the scripts connect to each other**, and **where every asset lives**. A companion file — [Cutscene_And_Roadmap_Guide.md](file:///d:/unity_projects/xr-game-dev/Documentation/Cutscene_And_Roadmap_Guide.md) — covers how to animate cutscenes and how to push the project forward.

---

## 1. The game in one page

The player is trapped in a **horror escape room** and the game runs in three acts:

| Act | Scene | What happens |
| --- | --- | --- |
| **1 — Horror intro** | `concerthall.unity` | The player starts in the **concert hall** purely to be scared (scripted/sit-down horror moment). No gameplay, no XR rig — it is a cinematic scene with a Timeline. |
| **2 — Learn the mechanics** | `backrooms.unity` | The player is **teleported to the Backrooms**. Here they learn the core loop: **pick up music equipment and play the instrument that matches the monster** to defeat it. Monsters appear from hidden spawn points inside invisible trigger *zones*, stalk you, then sprint and jumpscare you back to the centre if they catch you. |
| **3 — Final boss** | `concerthall.unity` / `conductor.unity` | The player is **teleported back to the concert hall** to face the **final boss**, who spawns and commands all the monsters at once. |

**Core design loop:** find equipment → summon it with the wheel → physically *play* the instrument (hit the drum, press the trumpet valves, rub the bow on the violin) → defeat the matching monster → boss wave → repeat.

> **Reality check:** Act 1's "teleport", Act 3's boss transition, and the *"playing an instrument damages a monster"* link are **not implemented yet**. See [§8 Known gaps](#8-known-gaps--not-yet-implemented) — this is the most important section for continuing the project.

---

## 2. Folder map (after re-organisation)

```
Assets/
├── 2d/                     UI sprites (back.png, flute.png, wheel_background.png)
├── Audio/                  Sound effects (drum/tpt/violin sfx, monster footsteps/scream/spawn)
├── Imports/                Raw imported models & animators (see §6)
│   ├── conductor/source/   Boss.controller, conductor.controller, mixamo.com.anim, piano.anim
│   ├── smiler-backrooms/   Monster.controller, Walking.fbx, Zombie Scream.fbx, source materials
│   ├── drumsticks/, dusty-piano/, flute/, trumpet/, violin/
│   └── 4515da3b....glb
├── Oculus/                 OculusProjectConfig.asset
├── Plugins/Android/        AndroidManifest.xml
├── Prefabs/
│   ├── Instruments/        Bow_object, Drum stick, Trumpet, Violin_Objec, violin_bow
│   │   ├── ItemData/       ItemData assets (Drumsticks, Trumpet, Violin, Violin_bow)
│   │   └── 3dmodel/        source meshes
│   ├── Monster.prefab
│   └── backrooms/          Large Room.prefab, Wall.prefab
├── Resources/              Meta/Oculus runtime settings + InputActions
├── Samples/                Meta XR Interaction SDK 207.0.0, XRI 3.6.0 samples
├── Scenes/                 SampleScene, backrooms, concerthall, conductor (+ test.unity)
├── Scripts/                ← REORGANISED (was one flat folder)
│   ├── Instruments/        DrumPad, DrumStick, DrumPlacer, TrumpetValves, SimpleViolin, SimpleBow
│   ├── Inventory/          WheelInventory, ItemData, ItemInstance
│   ├── Monsters/           MonsterAI, MonsterZoneTrigger, BossAI
│   └── Player/             VRPlayerController, ProximityInteractor, HandModelSwitcher
├── Texture/                Materials/ceiling.mat, backrooms_ceiling.jpg
├── XRI/, XR/               XR interaction / rig settings
└── InputSystem_Actions.inputactions
```

**What was changed in the tidy-up:**
- The flat `Assets/Scripts/` (15 scripts mixing instruments, monsters, inventory and player code) was split into `Instruments/`, `Inventory/`, `Monsters/`, `Player/`.
- Four files were **renamed so file name = class name** (Unity strongly prefers this):
  `Drum.cs`→`DrumPad.cs`, `DrumPosition.cs`→`DrumPlacer.cs`, `Drumsticks.cs`→`DrumStick.cs`, `Trumpet Valves.cs`→`TrumpetValves.cs`, `Itemdata.cs`→`ItemData.cs`.
  **GUIDs inside the `.meta` files were preserved**, so every scene/prefab reference still resolves. Nothing was re-wired.
- Deleted `Assets/_Recovery/` (three Unity crash-recovery scenes: `0.unity`, `0 (1).unity`, `0 (2).unity`) and the stray `Audio.meta.local-backup` at the repo root.
- Removed a dead **"Missing (Mono Script)"** component (`guid 97de5ef51484fa44b99a4d0da7d4ff89`, labelled `Assembly-CSharp::WheelInventory`) that was attached to a child of the XR Origin prefab instance in `SampleScene.unity`.

> Unity was closed while these files were moved. On next open, Unity will regenerate `.meta` files for the new folders — that is expected.

---

## 3. Script reference

All 15 gameplay scripts, with their **stable GUIDs** (useful when editing scene/prefab YAML directly).

### 3.1 Instruments — `Assets/Scripts/Instruments/`

| File | Class | Script GUID | Purpose |
| --- | --- | --- | --- |
| [DrumPad.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Instruments/DrumPad.cs) | `DrumPad` | `e3362c9752d9b7044bd3b615e470a6f0` | The **snare head**. Detects the stick hitting it and plays the drum sound. |
| [DrumStick.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Instruments/DrumStick.cs) | `DrumStick` | `8a8450ed28af49241a1d5d7696d2ee84` | The **stick tip**. Measures its own velocity and fires controller haptics. |
| [DrumPlacer.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Instruments/DrumPlacer.cs) | `DrumPlacer` | `b35f956ed50de0a40a8be6c9e9f0dbd8` | **Hovers the drum** in front of the player while a stick is held; makes it pass-through. |
| [TrumpetValves.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Instruments/TrumpetValves.cs) | `TrumpetValves` | `34184a6190215ff4296f5cdde2d6ee35` | While the trumpet is held, **trigger plays the music and animates the valves**. |
| [SimpleViolin.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Instruments/SimpleViolin.cs) | `SimpleViolin` | `489b9d87fde7c9e40b6aa23d3fd21c3d` | Holds a **looping violin AudioSource** that fades in only while being rubbed. |
| [SimpleBow.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Instruments/SimpleBow.cs) | `SimpleBow` | `4094f9d585b2dfb449d8401704f138a5` | The **bow**. Measures its speed and calls `SimpleViolin.Rub()` while moving on the violin. |

### 3.2 Inventory — `Assets/Scripts/Inventory/`

| File | Class | Script GUID | Purpose |
| --- | --- | --- | --- |
| [WheelInventory.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Inventory/WheelInventory.cs) | `WheelInventory` | `3e1c023f674dead43b0bacac83194ebe` | The **radial "summon" inventory**: hold a button → wheel in front of the eyes → pick a slot → the instrument flies into your hand. |
| [ItemData.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Inventory/ItemData.cs) | `ItemData` | `6b951995bda5102449f0d708df5937ca` | **ScriptableObject** describing one wheel slot (name, icon, prefab, optional 2nd prefab). |
| [ItemInstance.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Inventory/ItemInstance.cs) | `ItemInstance` | `15d3880b62f47c645a1a5a742cc5788f` | Tiny tag stamped onto a summoned object so it knows which `ItemData` it came from. |

### 3.3 Monsters — `Assets/Scripts/Monsters/`

| File | Class | Script GUID | Purpose |
| --- | --- | --- | --- |
| [MonsterAI.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Monsters/MonsterAI.cs) | `MonsterAI` | `5a8fba40f577b404ab1ebb22e76c17b1` | Per-monster NavMesh AI: stalk → sprint → **jumpscare** → teleport the player back to the respawn point. |
| [MonsterZoneTrigger.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Monsters/MonsterZoneTrigger.cs) | `MonsterZoneTrigger` | `84cf1f9873234814fb8ebce201cb5eaa` | An **invisible sphere zone**: when the player enters, delay, then spawn one monster from a hidden point the player is *not* looking at. |
| [BossAI.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Monsters/BossAI.cs) | `BossAI` | `c02f59dd44723734aa481e86e4965013` | The **final boss**: spawns a wave of monsters, commands them, becomes vulnerable when they are gone, then takes damage. |

### 3.4 Player — `Assets/Scripts/Player/`

| File | Class | Script GUID | Purpose |
| --- | --- | --- | --- |
| [VRPlayerController.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Player/VRPlayerController.cs) | `VRPlayerController` | `3a588521e6b5c77458b4a5c8805ff090` | **Desktop/Simulator look + move** (mouse look, WASD, gamepad left stick) via `CharacterController`. Used by `backrooms`/`conductor` "VR Player Sim". |
| [ProximityInteractor.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Player/ProximityInteractor.cs) | `ProximityInteractor` | `a1f0c3d47b9e4358a6c2d5e8f1b4a7c0` | **Proximity glow + "press X to grab"**: nearest grabbable within radius glows; X snaps it to the left hand and holds it. |
| [HandModelSwitcher.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Player/HandModelSwitcher.cs) | `HandModelSwitcher` | `3df4900017a08ef4b826e326ba487434` | Hides the **hand model** while an object is grabbed, shows it again on release. |

---

## 4. How each mechanic works

### 4.1 Looking around & moving
- **Real VR:** the `XR Origin (XR Rig)` prefab drives the head/controllers. Head tracking comes straight from the headset.
- **In the Editor (no headset):** an `XR Interaction Simulator` component exists on the rig but is **disabled by a prefab override** in `SampleScene.unity` (`m_IsActive: 0`). This is deliberate — the simulator was overriding real Quest head tracking. Enable it only when testing without a headset.
- **Desktop sim scenes** (`backrooms.unity`, `conductor.unity`) do **not** use an XR rig. They use an object called `VR Player Sim` (tag `Player`) with a `CharacterController` + [VRPlayerController.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Player/VRPlayerController.cs) for mouse-look/WASD.

### 4.2 Proximity glow + "press X to grab" — `ProximityInteractor`
Attach it to a persistent object (in `SampleScene` it is a standalone `Proximity Interactor` GameObject — currently **disabled**, tick `m_Enabled` to test it).

1. Every `0.5 s` it refreshes a list of all `XRGrabInteractable`s in the scene (`FindObjectsByType`).
2. It keeps the **closest one within `proximityRadius` (1.6 m)** of the head, measuring with `Collider.ClosestPoint(head)`.
3. That object gets a bright tint via a `MaterialPropertyBlock` writing `_BaseColor` / `_Color` / `_EmissionColor` (= `glowColor`).
4. Press the **X button** (left controller `primaryButton`) → the object's Rigidbody is frozen, it snaps to the hand (`holdOffset`), and its `XRGrabInteractable` is disabled → it stays held while you move your hand.
5. Press X again to release.

> If `interactAction` is left empty in the Inspector, `Awake()` builds a fallback `<XRController>{LeftHand}/primaryButton` action automatically.

### 4.3 Radial wheel inventory — `WheelInventory` + `ItemData` + `ItemInstance`
The script's header comment is the spec; here is the full behaviour.

**Inputs (wired in `SampleScene`):**
- `openAction` = `<XRController>{LeftHand}/{PrimaryButton}` (hold to open).
- `selectStickAction` = `<XRController>{RightHand}/thumbstick` (choose a slot).

**Open → choose → summon:**
1. Hold `openAction` → `OpenWheel()`: sets `open`, blocks locomotion (`SetLocomotionBlocked(true)` on the turn/move providers listed in `disableWhileOpen`), calls `PlaceWheel()` (the wheel is placed `viewDistance` (0.45 m) in front of the eyes), shows `wheelRoot`, and refreshes the slot UI.
2. `UpdateHover()` reads the thumbstick and maps it to a slot with `Mathf.Atan2(stick.x, stick.y)`; a dead-zone of `0.4` ignores small movements. The hovered slot's `selectedHighlights` object is shown and its `Image` tinted `highlightColor`.
3. Release `openAction` → `CloseWheel()` → `HandleSlotAction(hovered)`.

**What a slot does when chosen:**
- **First time:** the slot's `ItemData.prefab` is instantiated (from the wheel if `spawnFromWheel`, else from the left hand), stamped with an `ItemInstance` carrying its `ItemData`, and if `secondPrefab` is set a second object is created for the **left** hand.
- **Afterwards:** the existing objects are simply **teleported** to the hands — the wheel never duplicates items ("slots never empty").
- `FlyToHandAndGrab` animates the object to the hand over `flyDuration`, disables its grab while in flight, then calls `interactionManager.SelectEnter(...)` so it is properly grabbed by XRI.

**Asset plumbing:** `ItemData` is a `ScriptableObject`:
```csharp
public string displayName;   // slot label
public Sprite icon;           // slot image
public GameObject prefab;     // main item (Collider + Rigidbody + XRGrabInteractable)
public GameObject secondPrefab; // optional, goes to the LEFT hand
```

### 4.4 Hand model swapping — `HandModelSwitcher`
On the hand interactor's `selectEntered` it hides the hand renderers (and the GameObject); on `selectExited` it shows them again. Keeps the hand from clipping through a held object.

### 4.5 Playing the drum — `DrumStick` + `DrumPad` + `DrumPlacer`
This is the only "play an instrument" mechanic that uses **motion**, and the full chain is:

1. **`DrumPlacer`** (on the always-active holder object) listens to both hand interactors. When a `DrumStick` is grabbed it **moves the drum in front of the player** (`distance` 0.7 m, `heightOffset` 0.7 m below the eyes, `yawOffset` 180°), freezes the drum's Rigidbodies and turns all its Colliders into triggers so the drum hovers and nothing blocks the player. It keeps following the player in `LateUpdate` (`followSpeed` smoothing) and hides the drum when you let go (`hideWhenNoStick`).
2. **`DrumStick`** sits on the stick tip. Tracked objects have no physics velocity, so it computes its own in `FixedUpdate` and exposes `Velocity`.
3. **`DrumPad`** sits on the snare head (trigger Collider + `AudioSource`). On `OnTriggerEnter`:
   - ignores anything not tagged `DrumStick` and anything inside the `cooldown` (0.04 s);
   - projects the stick's velocity onto `downDirection` (default `Vector3.down`);
   - if that is below `minHitSpeed` (0.3) it does nothing;
   - otherwise plays `hitSound` with volume `Lerp(0.2, 1, strength)` and a random ±3 % pitch, and fires `stick.Haptic(Lerp(0.1, 0.8, strength))`.

### 4.6 Playing the trumpet — `TrumpetValves`
Put on the trumpet (the object holding the `XRGrabInteractable`).

- Grabbing + pressing the **trigger** (`activated` event) → `StartPlaying()`: plays/loops `music`, resets the valve pattern to step 0.
- Releasing the trigger (`deactivated`) or dropping the trumpet (`selectExited`) → `StopPlaying()`.
- While playing, `Update()` advances through `pattern` every `stepTime` (0.15 s). Each entry is a **bitmask**: `1` = valve 1, `2` = valve 2, `4` = valve 3, `0` = none (so `3` = valves 1+2, `7` = all three). Default pattern: `{ 1, 2, 4, 3, 5, 6, 7, 0 }`.
- Each valve's rod Transform is lerped between its rest position and `restPos + pressAxis * pressDistance` — the visible finger animation.

### 4.7 Playing the violin — `SimpleViolin` + `SimpleBow`
- **`SimpleViolin`** (on `Violin_Objec`) owns a looping `AudioSource` that starts at volume 0.
- **`SimpleBow`** (on `violin_bow`) computes its own speed each frame. `OnTriggerStay` on the violin's collider: if speed > `minRubSpeed` (0.08), it calls `violin.Rub()`.
- `Rub()` unpauses/plays the loop and sets `targetVolume = 1`. `SimpleViolin.Update()` lerps the volume toward the target (`fadeSpeed` 8) and resets the target to 0 each frame — so the note only sounds **while you are actively bowing**, and fades out naturally when you stop.

### 4.8 Monster encounters — `MonsterZoneTrigger` + `MonsterAI`
Two scripts, two jobs.

**`MonsterZoneTrigger`** (invisible sphere, one per instrument area in `backrooms.unity`: `Drums`, `Trumpet`, `Violin`):
- `Awake` forces `isTrigger = true` on its `SphereCollider`.
- When the player enters the `zoneRadius` (18 m) it starts `DelayedHorrorSpawn`: pick a spawn point from `hiddenSpawnPoints` that the camera **cannot see** (`GetBestHiddenSpawnPoint` / `IsVisibleToCamera`), play `spawnStingerClip`, wait `spawnDelay` (1.5 s), then instantiate `monsterPrefab` and give it 3D audio (`SetupMonster3DAudio`: `spatialBlend = 1`, logarithmic rolloff, doppler 0.5, `min/maxHearingDistance` 2/20 m).
- When the player leaves it gracefully despawns after `despawnGraceTime` (3 s).
- Static `activeMonster` / `currentActiveZone` enforce **one monster at a time**.
- `OnDrawGizmos` draws the zone sphere and lines to the spawn points, so you can place things visually.

**`MonsterAI`** (on `Monster.prefab`; requires `NavMeshAgent` + `Animator`):
- Auto-finds a `Player`-tagged object and a `Respawn`-tagged object if not assigned.
- A simple state machine driven by distance:
  - **Stalk** — `stalkSpeed` (1.8) toward the player; Animator float `Speed` = 1.
  - **Sprint** — inside `sprintTriggerDistance` (8 m) it switches to `sprintSpeed` (5.2), plays `aggroScreechClip` once.
  - **Jumpscare** — inside `jumpscareDistance` (1.6 m): stops the agent, disables it, and runs `ExecuteJumpscareSequence()`.
- **`ExecuteJumpscareSequence()`** (the signature move):
  1. Locks the player: disables `VRPlayerController` **and** the player's `CharacterController`.
  2. Uses `Camera.main` to compute where the head is looking; places the monster `faceSnapDistance` (0.85 m) in front of the camera, `faceHeightOffset` (−1.2 m) down, and `LookAt`s the head — so it is **right in your face**.
  3. Sets Animator `Speed = 0` and fires the `Scream` trigger; plays `jumpscareScreamClip` as a 2D-ish `PlayClipAtPoint` on the camera.
  4. Waits `jumpscareDuration` (2.2 s), then **teleports the player** to `backroomsCenterPoint` (or the `Respawn`-tagged object), re-enables the `CharacterController` and `VRPlayerController`, and `Destroy(gameObject)`.

### 4.9 Final boss — `BossAI`
In `conductor.unity` on the object `Boss (use for boss battle)`. Requires an `Animator` (uses `Boss.controller`).

**States:** `SpawningWave → Commanding → VulnerableTerrified → Dead` (enum `BossState`).

1. **`StartSpawningPhase()`** — show `shieldVisual`, Animator `IsTerrified = false`, fire the `CastSpell` trigger, wait 1.5 s, then instantiate one monster per entry in `waveMonsterPrefabs` at the `spawnPoints` (cycling if there are more monsters than points). Each spawned `MonsterAI` gets its `playerTransform` assigned. State → `Commanding`.
2. `Update()` keeps the boss facing the player (`Quaternion.Slerp` toward a flat look rotation) and, while `Commanding`, waits until `activeWaveMonsters.Count == 0` → `EnterVulnerableState()`.
3. **`EnterVulnerableState()`** — hide the shield, Animator `IsTerrified = true`, state → `VulnerableTerrified`, wait `vulnerableDuration` (8 s), then loop back to `StartSpawningPhase()` if still vulnerable.
4. **`TakeDamage(amount)`** — **only works while `VulnerableTerrified`** (invulnerable while commanding). Plays the `TakeHit` trigger; at `currentHealth <= 0` → `Die()`.
5. **`Die()`** — stop coroutines, state `Dead`, `Die` trigger, disable the collider and the component.

Inspector test toggles (`testCastSpellTrigger`, `testTerrifiedBool`, `testTakeHitTrigger`, `testDeathTrigger`) let you drive the Animator by hand without the game logic.

---

## 5. How the code connects (call graph)

```
                    ┌─────────────────────────── PLAYER ───────────────────────────┐
                    │                                                              │
  left X button ───►│ ProximityInteractor ──(glow + snap to hand)──► XRGrabInteractable
  left primary ────►│ WheelInventory ──spawn/fly──► ItemData.prefab ──► ItemInstance
   (hold/open)      │      │                                                   │
  right thumbstick ►│      └─ disableWhileOpen[] (Turn/Move providers)          │
                    │                                                          │
  hand interactor ─►│ HandModelSwitcher  (hide/show hand on grab)              │
  mouse/WASD ──────►│ VRPlayerController (desktop sim scenes only)             │
                    └──────────────────────────────────────────────────────────┘

  INSTRUMENTS (each is an XRGrabInteractable prefab)
    Drum stick.prefab ─┬─ DrumStick  (velocity + haptic) ──┐
                       ├─ DrumPlacer (hovers the drum)     │ Dot(Velocity, down)
                       └─ ItemInstance                     ▼
                                           Snare "DrumPad" ──► AudioSource.PlayOneShot

    Trumpet.prefab ──── TrumpetValves  (grabInteractable.activated/deactivated)
                             └─► AudioSource.Play + valve rod lerp

    violin_bow.prefab ─ SimpleBow ──OnTriggerStay──► Violin_Objec "SimpleViolin".Rub()
                                                          └─► looping AudioSource fade

  MONSTERS
    backrooms scene:  MonsterZoneTrigger (sphere, per zone)
                        └─ delayed spawn from hidden point ──► Monster.prefab
    Monster.prefab:   MonsterAI  (NavMeshAgent + Animator)
                        └─ distance state machine ──► jumpscare ──► teleport player to "Respawn" ──► Destroy

    conductor scene:  BossAI (Animator)
                        ├─ SpawningWave ──► Instantiate(waveMonsterPrefabs) ──► MonsterAI
                        ├─ Commanding   ──► waits for activeWaveMonsters.Count == 0
                        ├─ VulnerableTerrified ──► TakeDamage(amount)   ◄── ⚠ NO CALLER YET
                        └─ Dead
```

**Cross-script contracts to respect:**
- `MonsterAI` needs an object tagged `Player` (auto-found) and one tagged `Respawn`.
- `DrumPad` only reacts to colliders tagged `DrumStick` **that also carry a `DrumStick` component**.
- `SimpleBow` only reacts to a collider that has `SimpleViolin` on the same object.
- `BossAI` assigns `playerTransform` to each spawned monster.
- `WheelInventory` calls `XRInteractionManager.SelectEnter` — the manager reference must be set.

---

## 6. Where everything is (assets)

### 6.1 Scenes — `Assets/Scenes/`

| Scene | Role | Player object | Notes |
| --- | --- | --- | --- |
| [SampleScene.unity](file:///d:/unity_projects/xr-game-dev/Assets/Scenes/SampleScene.unity) | **Main VR test sandbox** | `XR Origin (XR Rig)` (active) | Has the wheel UI, `WheelInventory`, `ProximityInteractor` (disabled), `Snare` (`DrumPad`), all instrument prefabs. `XR Interaction Simulator` is **disabled by override**. |
| [backrooms.unity](file:///d:/unity_projects/xr-game-dev/Assets/Scenes/backrooms.unity) | **Mechanic-learning arena** | `VR Player Sim` (tag `Player`) | `Environment` container (many `Wall` prefabs + `Large Room`), `NavMeshSurface` on `Plane`, `Respawn` object (tag `Respawn`, y=1). Three `MonsterZoneTrigger` zones named `Drums`, `Trumpet`, `Violin`. |
| [concerthall.unity](file:///d:/unity_projects/xr-game-dev/Assets/Scenes/concerthall.unity) | **Act 1 intro (cinematic)** | `Main Camera` (no rig) | Has a `timeline` object with a `PlayableDirector` and a `Canvas` → TMP text "Welcome". No gameplay scripts. |
| [conductor.unity](file:///d:/unity_projects/xr-game-dev/Assets/Scenes/conductor.unity) | **Boss arena** | `VR Player Sim` (tag `Player`) | `Boss (use for boss battle)` prefab + `BossAI` + Animator(`Boss.controller`); `Conductor` model with `conductor.controller`; `Piano`. |

`ProjectSettings/EditorBuildSettings.asset` currently enables **only `SampleScene`**. The other scenes must be added there before they can be loaded at runtime or reached by build order.

### 6.2 Prefabs — `Assets/Prefabs/`

| Prefab | GUID | Key components |
| --- | --- | --- |
| `Monster.prefab` | `7f5789adaa3c84e4f987264ca04969b4` | Root `Monster` (scale 0.4) · `NavMeshAgent` · `MonsterAI` (stalk 0.5 / sprint 2 / faceSnap 2 / faceHeightOffset −2.7) · `Animator` (`Monster.controller`, root motion on) |
| `Instruments/Drum stick.prefab` | `96655b719953dc3448565955bfa1598d` | `Rigidbody` · `XRGrabInteractable` · `DrumPlacer` · `DrumStick` · `ItemInstance`; child stick mesh tagged `DrumStick` |
| `Instruments/Trumpet.prefab` | `b2c905974791fd040844bec17a4de455` | `XRGrabInteractable` · `TrumpetValves`; valve rod/button children |
| `Instruments/Violin_Objec.prefab` | `1bdeb3e7b3ce5ec4faad23537a46a63c` | `Rigidbody` (kinematic) · `XRGrabInteractable` · `SimpleViolin` |
| `Instruments/violin_bow.prefab` | `93918814628c5ad489c794afb77e0ef1` | scaled ~0.0065 · trigger `BoxCollider` · `SimpleBow` (minRubSpeed 0.08) · kinematic `Rigidbody` · `XRGrabInteractable` |
| `Instruments/Bow_object.prefab` | `90417807cf050364798fa94cd6ddd8c1` | `Rigidbody` · `XRGrabInteractable` (Kinematic move type) |
| `backrooms/Wall.prefab`, `backrooms/Large Room.prefab` | `c152a2acdd004fd46874fc58e1ce29f8`, `6a8f5e822db98a14b81d70b86d48e756` | Backrooms geometry |

### 6.3 Wheel slots — `Assets/Prefabs/Instruments/ItemData/`

These four `ItemData` assets are the inventory's content. `SampleScene`'s `WheelInventory.items` currently lists **Trumpet, Violin_bow, Drumsticks** (3 slots), matching the 3 `slotImages`.

| Asset | GUID | displayName | prefab | secondPrefab |
| --- | --- | --- | --- | --- |
| `Drumsticks.asset` | `fc16dec25f7e2d843982f33476ef5380` | Drum Stick | `Drum stick.prefab` | — |
| `Trumpet.asset` | `e7bdba77fee63e149a2cf36e6e48114f` | Trumpet | `Trumpet.prefab` | — |
| `Violin.asset` | `1f68783ae41a7a94ca5ef3148c733a21` | Violin | *(empty!)* | `Violin_Objec.prefab` |
| `Violin_bow.asset` | `998b53f8ea8e78c43902d79e6293f1e2` | Violin Bow | `violin_bow.prefab` | `Violin_Objec.prefab` |

> ⚠ **All four have `icon: {fileID: 0}`** → the wheel slot images render as plain colour boxes. Assign a `Sprite` to `icon` on each asset to fix.
> ⚠ `Violin.asset` has an **empty `prefab`** field — only `secondPrefab` is set. It is not referenced by `SampleScene`, so it is currently harmless, but fill it in (or delete the asset) to avoid confusion.

### 6.4 Animators — `Assets/Imports/`

| Controller | Parameters | Attached to |
| --- | --- | --- |
| `conductor/source/Boss.controller` | `IsTerrified` (bool), `CastSpell` (trigger), `TakeHit` (trigger), `Die` (trigger) | `Boss (use for boss battle)` in `conductor.unity` |
| `smiler-backrooms/Monster.controller` | `Speed` (float), `Scream` (trigger) | `Monster.prefab` |
| `conductor/source/conductor.controller` | *(conducting motion)* | `Conductor` model in `conductor.unity` |

`BossAI` and `MonsterAI` reference these parameters through `Animator.StringToHash` constants, so **renaming a parameter in the Animator will silently break the script** unless you update the hash string too.

### 6.5 Audio — `Assets/Audio/`
Drum hit, trumpet music, violin loop, monster footstep / scream / spawn. The monster clips are referenced from `Monster.prefab` (`jumpscareScreamClip`, `aggroScreechClip`); the zone stinger from `MonsterZoneTrigger.spawnStingerClip`.

### 6.6 Tags
- Custom tag: **`DrumStick`**.
- Built-in tags used: **`Player`** (`VR Player Sim`), **`Respawn`** (`Respawn` object), **`MainCamera`**.
- Layers used: `Ignore Raycast` (wheel rig child), `UI` (wheel canvas).

---

## 7. How to run / test

1. Open `SampleScene`. Turn on the `Proximity Interactor` component (it ships disabled) if you want to test glow/X-grab.
2. Play **in the Editor without a headset** → enable `XR Interaction Simulator` (remove the `m_IsActive: 0` override) so you have a simulated headset.
3. Play **on Quest** → leave the simulator disabled (it overrides real head tracking).
4. In-editor keyboard shortcuts provided by XRI: WASD/arrows move the simulated rig, right mouse drags to look.
5. Wheel test: hold **left X/Primary** to open, push **right thumbstick** to hover a slot, release to summon.
6. Drum test: grab the **Drum stick** → the snare hovers in front of you → strike downward on the pad.
7. Backrooms test: open `backrooms.unity` and walk into a zone (`Drums` / `Trumpet` / `Violin`) — the gizmos show where the monster will spawn.

---

## 8. Known gaps / not-yet-implemented

These are the parts the design describes but the code does not yet do. **This is the priority list for continuing.**

| # | Gap | Detail |
| --- | --- | --- |
| 1 | **Instruments don't damage monsters** | `MonsterAI` has **no health and no damage API at all**. Nothing links "playing the drum/trumpet/violin" to defeating the matching monster. `DrumPad`, `TrumpetValves` and `SimpleViolin` only produce sound. |
| 2 | **`BossAI.TakeDamage()` has no caller** | The boss can only lose health if something calls `TakeDamage(float)`. No instrument or projectile does. |
| 3 | **No monster→instrument pairing** | There is no "type" field on `MonsterAI` / `MonsterZoneTrigger` and no "correct instrument" field on the instruments, so the *matching* rule cannot be evaluated. |
| 4 | **No scene transitions** | Nothing teleports the player from `concerthall` → `backrooms` → boss arena. No `SceneManager.LoadScene`, no fade, no trigger volumes to change act. |
| 5 | **No cutscene system** | `concerthall.unity` has a `PlayableDirector` + Timeline object, but there is no code that plays it or hands control back to the player. See the companion guide. |
| 6 | **`MonsterAI`/`BossAI` combo untested together** | `BossAI` spawns monsters that immediately use `MonsterAI`, which on jumpscare **destroys itself** (`Destroy(gameObject)`). `BossAI` then sees the count drop — that is the intended loop, but it has not been verified in the scene. |
| 7 | **Only `SampleScene` is in build settings** | Add `concerthall`, `backrooms`, `conductor` to *File → Build Profiles → Scene List*. |
| 8 | **Blank inventory icons** | All `ItemData.icon` fields are empty. |
| 9 | **`ItemData` `Violin.asset` has no `prefab`**. | Data slip. |
| 10 | **`ProximityInteractor` disabled in `SampleScene`**. | Tick `m_Enabled` on the `Proximity Interactor` object. |
| 11 | **CS0618 warnings** | `ProximityInteractor.cs` uses the obsolete `FindObjectsSortMode` overload (`FindObjectsByType<T>(FindObjectsSortMode.None)`). Replace with `FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)` or the newer non-obsolete overload to keep the console clean. |
| 12 | **Scenes carry an old class identifier** | `VRPlayerController` is still referenced in `backrooms`/`conductor` with `m_EditorClassIdentifier: Assembly-CSharp::NewMonoBehaviourScript` (an earlier class name). Harmless (Unity resolves by GUID) but it will be cleaned up automatically the next time Unity re-serialises those scenes. |

---

## 9. Quick reference — script ↔ scene ↔ object

| Scene | Object | Script |
| --- | --- | --- |
| `SampleScene` | `Wheelinventory` (child of XR Origin) | `WheelInventory` |
| `SampleScene` | `Proximity Interactor` | `ProximityInteractor` (disabled) |
| `SampleScene` | `Snare` | `DrumPad` |
| `SampleScene` | `Drum stick` | `DrumPlacer`, `DrumStick`, `ItemInstance` |
| `SampleScene` | *(child of XR Origin)* | `HandModelSwitcher` |
| `backrooms` | `Drums` / `Trumpet` / `Violin` | `MonsterZoneTrigger` |
| `backrooms` | `VR Player Sim` (tag `Player`) | `VRPlayerController` |
| `conductor` | `Boss (use for boss battle)` | `BossAI` |
| `conductor` | `VR Player Sim` (tag `Player`) | `VRPlayerController` |
| *(spawned)* | `Monster` from `Monster.prefab` | `MonsterAI` |
