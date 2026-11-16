# Cutscene Guide & Roadmap — XR Horror Escape-Room

Companion to [Project_Documentation.md](file:///d:/unity_projects/xr-game-dev/Documentation/Project_Documentation.md).
Part A = **how to animate cutscenes**, Part B = **how to keep building the game**.

Project facts that shape both parts:
- **Unity 6000.6.2f1**, URP, Input System only.
- **Timeline 6.6.0 is installed** ✓ ; **AI Navigation 2.0.14** ✓ ; **Visual Scripting** ✓.
- **Cinemachine is NOT installed** — nothing in the project uses it yet. Install `com.unity.cinemachine` from *Window → Package Manager → Unity Registry* if you want authored camera shots.
- `concerthall.unity` already has a `timeline` GameObject with a `PlayableDirector` and a `Canvas` showing the TMP text "Welcome".

---

# Part A — Animating cutscenes

## A0. Pick the right kind of cutscene for VR

VR has no fixed camera — the player can look anywhere at any time. There are three practical styles:

| Style | How it works | Best for |
| --- | --- | --- |
| **1. Flat cinematic** (what `concerthall.unity` is today) | A normal authored camera + Timeline. The player just watches. No XR rig needed. | The Act 1 concert-hall scare, title/menu screens. |
| **2. VR "in-world" cutscene** ⭐ | Keep the XR rig **alive** so the player keeps head tracking, but **take away control** and animate the *world around them* (the monster walks in, the lights go out, a door slams). | Most of your horror beats. Feels native to VR. |
| **3. Player-rail** | Disable control and animate the `XR Origin` transform itself along a path. | "Being dragged somewhere" / teleported moments. |

**Rule of thumb:** never take the headset camera away from the player. Move actors and lights, or move the rig, but keep head tracking.

## A1. The three animation tools you already have

1. **Unity Timeline** (`com.unity.timeline` ✓ installed) — the main tool. A `PlayableDirector` plays a `TimelineAsset` made of tracks:
   - *Activation Track* — show/hide GameObjects on cue (great: pop the monster in, hide a door).
   - *Animation Track* — keyframe a character's `Animator` or any object's Transform.
   - *Audio Track* — stings, music, dialogue.
   - *Signal Track / Signal Emitter* — fire a callback at an exact frame (this is how you make Timeline talk to your scripts).
   - *Cinemachine Track* — only after you install Cinemachine; gives proper camera cuts.
2. **Animator + Animator Controller** — for looping/skeletal motion. You already have `Monster.controller`, `Boss.controller` and `conductor.controller` (see documentation §6.4).
3. **Coroutines** — for hard-coded sequences with waits. `BossAI` and `MonsterAI` already do this. Good for simple "freeze → move → scare → unfreeze" moments.

## A2. Build a flat cinematic (Act 1 intro) — step by step

1. Open `concerthall.unity`.
2. **Window → Sequencing → Timeline**.
3. Select the existing `timeline` GameObject. In the Timeline window click **Create** → save the asset as `Assets/Timeline/IntroCinematic.playable`. Unity adds/uses the `PlayableDirector` on that object.
4. Set the Director:
   - **Update Method:** `Game Time`
   - **Playable:** `IntroCinematic`
   - **Play On Awake:** `✓` (or drive it from code — see A3)
   - **Wrap Mode:** `Hold`
5. Add tracks on the left:
   - **+ → Activation Track** → drag in the monster/boss model. Add a clip; toggle the clip on/off to reveal it.
   - **+ → Animation Track** → drag in a character with an Animator; right-click the track → *Add From Animation Clip*, or record keyframes with the red record button.
   - **+ → Audio Track** → drag in an `AudioSource`; place clips for stings and ambience.
   - **+ → Signal Track** → add a `SignalEmitter` where you want the game code to react (e.g. "scare happened", "cutscene over").
6. Scrub the playhead to preview. Press ▶ in the Timeline window to test in Play mode.
7. The `Canvas` + "Welcome" TMP text is already in the scene — animate its `CanvasGroup.alpha` from 0→1→0 on an Animation Track for a fade-in title.

## A3. Build a **VR-safe** cutscene (recommended) — with helper code

The cleanest pattern is a small "cutscene director" that (1) freezes the player, (2) plays a Timeline, (3) unfreezes. Nothing about the player's view is touched, so head tracking keeps working.

**Step 1 — wire the Timeline to the game with a Signal.**
In the Timeline, add a *Signal Track* and drop an emitter at the end of the cutscene. Create a `SignalReceiver` component on a scene object, add a Unity Event reaction, and point it at a public method (e.g. `CutsceneDirector.OnCutsceneFinished`). Alternatively just use `director.stopped += ...` in code (shown below).

**Step 2 — add this script** (suggested new file, e.g. `Assets/Scripts/Player/CutsceneDirector.cs`):

```csharp
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Plays a Timeline and freezes the player for its duration.
/// Attach to a persistent object (e.g. the XR Origin) and assign in the Inspector.
public class CutsceneDirector : MonoBehaviour
{
    [Tooltip("The PlayableDirector holding the cutscene Timeline.")]
    public PlayableDirector director;

    [Tooltip("Turn / move providers + teleport providers to disable while the cutscene plays.")]
    public Behaviour[] disableWhilePlaying;

    [Tooltip("Hands/ray interactors to disable so the player cannot grab mid-cutscene.")]
    public XRBaseInteractor[] disableInteractors;

    [Tooltip("Optional black fade canvas (CanvasGroup).")]
    public CanvasGroup fadeGroup;
    public float fadeDuration = 0.6f;

    public event Action Finished;

    /// <summary>Play with a fade-out, freeze, play, fade-in, unfreeze.</summary>
    public IEnumerator PlayWithFade()
    {
        SetPlayerActive(false);
        yield return Fade(1f);          // to black
        director.time = 0;
        director.Play();
        while (director.state == PlayState.Playing) yield return null;
        yield return Fade(0f);          // back to the world
        SetPlayerActive(true);
        Finished?.Invoke();
    }

    /// <summary>Play without fading — for an in-world scare the player must see.</summary>
    public void PlayInWorld()
    {
        SetPlayerActive(false);
        director.stopped += OnStopped;
        director.time = 0;
        director.Play();
    }

    void OnStopped(PlayableDirector d)
    {
        d.stopped -= OnStopped;
        SetPlayerActive(true);
        Finished?.Invoke();
    }

    void SetPlayerActive(bool on)
    {
        foreach (var b in disableWhilePlaying) if (b) b.enabled = on;
        foreach (var i in disableInteractors) if (i) i.enabled = on;
    }

    IEnumerator Fade(float target)
    {
        if (fadeGroup == null) yield break;
        float start = fadeGroup.alpha, t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        fadeGroup.alpha = target;
    }
}
```

**Step 3 — animate the world, not the player.** On the Timeline, use *Activation* / *Animation* / *Audio* tracks to move the monster, swing doors, flicker lights. If you must move the player, add an *Animation Track* bound to the **XR Origin transform** (not the camera) and keep the motion slow and comfortable, or just teleport the rig to a new position at a cut.

## A4. Scene transitions between acts (this is a "cutscene" too)

The game needs `concerthall → backrooms → conductor`. There is **no transition code yet**. A minimal, reusable flow manager (suggested `Assets/Scripts/Player/GameFlowManager.cs`):

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Fades to black and loads the next act. Put the scene names in build order
/// (File > Build Profiles > Scene List) and call Advance() from a trigger, a
/// timeline Signal, or a monster jumpscare.
public class GameFlowManager : MonoBehaviour
{
    public CanvasGroup fadeGroup;
    public float fadeDuration = 0.7f;
    public string nextScene;

    public void Advance() => StartCoroutine(LoadNext());

    IEnumerator LoadNext()
    {
        yield return Fade(1f);
        var op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return Fade(0f);
    }

    IEnumerator Fade(float target)
    {
        if (fadeGroup == null) yield break;
        float start = fadeGroup.alpha, t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        fadeGroup.alpha = target;
    }
}
```

**Fade canvas recipe:** in each scene add a world-space (VR) or screen-space (flat) `Canvas` with a full-screen black `Image`, and put a `CanvasGroup` on the Canvas. Set `alpha = 0`, and set the Canvas `sortingOrder` high so it draws over everything. Because VR has no screen, use a **world-space canvas parented to the camera** (a black quad 1 m in front of the eyes) with `sortingOrder` above all else.

## A5. Animating the monsters/boss

- **Reuse the existing controllers.** `Monster.controller` has `Speed` (float) and `Scream` (trigger); `Boss.controller` has `IsTerrified` (bool), `CastSpell`/`TakeHit`/`Die` (triggers). `MonsterAI`/`BossAI` reference these by string hash — if you add or rename a parameter, update the `StringToHash` constant in the script.
- **Adding a death animation:** in `Monster.controller`, add a `Die` trigger parameter, create a *Death* state with your death clip, and add a transition `Any State → Death` with condition `Die`. Then call `animator.SetTrigger("Die")` from your new damage code (see Part B, milestone 1).
- **Importing new Mixamo animation:** download FBX (T-pose, without skin) → drag into `Assets/Imports/<monster>/` → select it → *Rig → Animation Type = Humanoid* → *Avatar Definition = Copy From Other Avatar* → point at the monster's existing avatar → *Apply*. Then it will retarget to your rig. For a quick retime, duplicate the FBX and set *Loop Time* / bake root motion in the Animation tab.
- **Recorded animation:** the Unity **Recorder** package can record a play session to an animation clip; useful for hand-authored scares.

## A6. Cutscene checklist
- [ ] Cutscene asset saved under `Assets/Timeline/` with a descriptive name.
- [ ] Player control disabled for the duration (locomotion + interactors) and **always re-enabled** at the end (including on `OnDisable`/abort — a stuck-frozen player is the #1 VR cutscene bug).
- [ ] Head tracking never removed.
- [ ] A skip path exists (optional for a film-style intro; important for replay).
- [ ] Audio mixed so a jumpscare doesn't clip.
- [ ] Tested standing *and* seated, both Quest and Editor.

---

# Part B — Roadmap: how to keep building

The design's core rule — **play the matching instrument to defeat the matching monster** — is **not implemented**. Priority order below.

## Milestone 1 — Connect instruments to monsters (the missing core loop)

Today `DrumPad`, `TrumpetValves` and `SimpleViolin` only make sound, and `MonsterAI` has **no health**. Add three small pieces:

**1) A shared "instrument type" enum.**

```csharp
// Assets/Scripts/Instruments/InstrumentType.cs
public enum InstrumentType { None, Drum, Trumpet, Violin }
```

**2) Give the monster health + a type.** Add to [MonsterAI.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Monsters/MonsterAI.cs):

```csharp
[Header("Combat")]
public InstrumentType weakness = InstrumentType.Drum;  // which instrument kills it
public float maxHealth = 100f;
float currentHealth;

void Awake() { currentHealth = maxHealth; /* ...existing init... */ }

/// Called when the player plays an instrument nearby.
public void OnInstrumentPlayed(InstrumentType instrument, float strength)
{
    if (instrument != weakness) return;      // wrong instrument = no effect
    currentHealth -= strength;               // strength 0..1 scaled to damage
    animator.SetTrigger("Scream");           // or a dedicated "Hurt" trigger
    if (currentHealth <= 0f) Die();
}

void Die()
{
    StopAllCoroutines();
    if (agent) { agent.isStopped = true; agent.enabled = false; }
    animator.SetTrigger("Die");              // add this param to Monster.controller
    Destroy(gameObject, 2.5f);
}
```

**3) Emit "played a note" events from each instrument.** Simplest approach: a tiny helper that finds monsters in range.

```csharp
// Assets/Scripts/Instruments/InstrumentEmitter.cs — put on each instrument prefab
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class InstrumentEmitter : MonoBehaviour
{
    public InstrumentType instrument = InstrumentType.Drum;
    public float hearingRadius = 12f;   // how far the note reaches

    /// Call this the moment a note is successfully played
    /// (from DrumPad on a hit, TrumpetValves on each pattern step,
    ///  SimpleViolin.Rub() while bowing).
    public void EmitNote(float strength01)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, hearingRadius);
        foreach (var c in hits)
        {
            var m = c.GetComponentInParent<MonsterAI>();
            if (m != null) m.OnInstrumentPlayed(instrument, strength01 * Time.deltaTime * 100f);
        }
    }
}
```

Wire the call sites:
- `DrumPad.OnTriggerEnter` → after a successful hit: `GetComponent<InstrumentEmitter>()?.EmitNote(strength);`
- `TrumpetValves.Update` → in the pattern-step branch: `emitter.EmitNote(1f);` (every `stepTime`)
- `SimpleViolin.Rub()` → `emitter.EmitNote(1f);` each frame the bow is moving.

> **Design note:** rate-based damage (per frame / per beat) means the player must *keep playing*. That matches "play the corresponding one to defeat the corresponding monster". Tune `hearingRadius` so the monster must be close, and consider requiring the monster to be within a *facing* cone for extra skill.

## Milestone 2 — Pair monsters with zones
Give `MonsterZoneTrigger` a `public InstrumentType zoneMonsterWeakness;` and set `monsterAI.weakness = zoneMonsterWeakness;` right after `Instantiate` in `DelayedHorrorSpawn`. Then the `Drums` zone spawns a drum-weak monster, the `Trumpet` zone a trumpet-weak one, and so on — matching the objects already named `Drums`, `Trumpet`, `Violin` in `backrooms.unity`.

## Milestone 3 — Wire the boss to instrument damage
`BossAI.TakeDamage(float)` already exists but **has no caller**. In the instrument emit path, also call it:

```csharp
// inside InstrumentEmitter.EmitNote(...)
var boss = FindFirstObjectByType<BossAI>();
if (boss != null) boss.TakeDamage(strength01 * Time.deltaTime * 100f);
```
`BossAI.TakeDamage` already ignores damage unless it is `VulnerableTerrified`, so this is safe: the player can only hurt the boss while its shield is down. Add a "boss takes a hit" VFX/sound for feedback.

## Milestone 4 — Scene flow (`concerthall → backrooms → conductor`)
1. Add all four scenes to **File → Build Profiles → Scene List** (only `SampleScene` is enabled today).
2. Drop `GameFlowManager` (Part A, A4) into each scene.
3. Trigger Act 1 → Act 2 from the end of the intro cutscene (`CutsceneDirector.Finished` event → `GameFlowManager.Advance()`).
4. Trigger Act 2 → Act 3 when the player finishes the last Backrooms zone, or after a set number of monsters are defeated.
5. Add a black fade canvas per scene (see A4 recipe).

## Milestone 5 — Cutscenes for the act transitions
- Act 1: intro horror cinematic in `concerthall.unity` (already has a `PlayableDirector`).
- Act 2: a "wake up in the Backrooms" VR cutscene — fade to black, move the rig to the respawn point, fade in, re-enable control.
- Act 3: boss reveal cinematic — use `BossAI`'s `CastSpell` trigger and the `Conductor` model; freeze the player.

## Milestone 6 — Inventory & equipment polish
- Assign a `Sprite` to `icon` on all four `ItemData` assets (they are blank → plain colour slots).
- Fix `Violin.asset`'s empty `prefab` field.
- Decide whether the violin is **one item or two**: today `Violin_bow.asset` has a `secondPrefab` = `Violin_Objec`, so summoning the bow also gives you the violin body in the left hand. Confirm this is intended and mirror it in the wheel slots.
- Consider adding a 4th slot (there are only 3 `slotImages` wired, but `WheelInventory.items` supports more).

## Milestone 7 — Monster AI polish
- Multiple simultaneous monsters: `MonsterZoneTrigger` currently enforces **one active monster at a time** via static fields. For the boss wave you may want to relax that for `BossAI`-spawned monsters (they bypass zones, so it already works, but verify).
- The jumpscare teleports the player using `Camera.main` — confirm `Camera.main` is the headset camera in each scene (in `backrooms`/`conductor` the main camera is a child of `VR Player Sim`; that is fine, but in a real XR rig it must be the rig's Camera).
- Add an aggro/patrol state so the monster doesn't beeline the instant it spawns.

## Milestone 8 — Audio & juice
- 3D audio is already set up on monsters and instruments (`spatialBlend = 1`). Add reverb zones for the concert hall.
- Add haptics on the trumpet/violin (only the drum has haptics today, via `DrumStick.Haptic`).
- Screen/eye "vignette" on jumpscare — an animated black world-space quad around the camera.

## Milestone 9 — Performance & build to Quest
- Watch draw calls in the Backrooms (many `Wall` prefabs) — batch them or use `Mesh.CombineMeshes` / a proper modular wall mesh.
- Ensure `NavMeshSurface` is baked after any geometry change.
- Build to Android/Quest with the OpenXR + Meta XR plugins already configured.

## Milestone 10 — Housekeeping
- Fix the `CS0618` warnings in [ProximityInteractor.cs](file:///d:/unity_projects/xr-game-dev/Assets/Scripts/Player/ProximityInteractor.cs) (obsolete `FindObjectsSortMode` overload).
- Enable `ProximityInteractor` in `SampleScene` if you want the glow/X-grab available.
- Keep `.meta` files in version control — moving scripts is only safe because the GUIDs travel with them.

---

## Suggested order of work (dependency-driven)

```
1. InstrumentType + MonsterAI health + InstrumentEmitter   ← unlocks everything
2. Zone → monster weakness pairing
3. Boss damage wiring
4. GameFlowManager + build settings (scene transitions)
5. Cutscenes at each transition (Timeline + CutsceneDirector)
6. Inventory icons / violin two-part item
7. Monster AI & juice polish
8. Quest build + performance pass
```
