using System.Collections;
using UnityEngine;

public class BossIntroCutscene : MonoBehaviour
{
    [Header("Trigger Distances")]
    [Tooltip("Distance at which the boss speaks his dialogue while playing piano.")]
    public float dialogueTriggerRadius = 15f;
    [Tooltip("Distance at which the blackout happens and the combat boss appears.")]
    public float battleStartRadius = 7f;

    [Header("Cutscene & Boss Objects")]
    [Tooltip("The combined GameObject of the Piano + Boss playing the piano.")]
    public GameObject pianoPerformerObject;
    [Tooltip("The separate Combat Boss GameObject (can be inactive in scene or a Prefab).")]
    public GameObject combatBossObject;
    [Tooltip("Where the Combat Boss appears if instantiating a prefab or repositioning.")]
    public Transform bossSpawnPoint;
    public Light stageSpotlight;
    public Transform playerTransform;

    [Header("Transition VFX Prefabs")]
    [Tooltip("Optional smoke/dissolve particle prefab spawned where the piano vanishes.")]
    public GameObject pianoVanishVfxPrefab;
    [Tooltip("Dramatic particle/portal/smoke burst prefab spawned where the Combat Boss appears.")]
    public GameObject bossSpawnVfxPrefab;
    [Tooltip("Vertical offset for the VFX prefabs (e.g., 1.0 so it spawns at chest/center height rather than floor).")]
    public float vfxHeightOffset = 1.0f;
    [Tooltip("Delay after spawning the boss VFX before the boss mesh actually pops in (hides the pop-in inside the burst).")]
    public float bossAppearDelay = 0.25f;
    [Tooltip("How long before spawned VFX objects are automatically destroyed.")]
    public float vfxLifetime = 4.0f;

    [Header("Audio References")]
    [Tooltip("AudioSource playing the looping slow piano BGM.")]
    public AudioSource pianoBgmSource;
    [Tooltip("3D AudioSource for the boss's intro voice line (put on StageController so it doesn't cut off when piano object disables).")]
    public AudioSource bossVoiceSource;
    public AudioClip bossDialogueClip;
    [Tooltip("Optional heavy power-down / slam SFX when the spotlight cuts out.")]
    public AudioClip lightsOutSfx;
    [Tooltip("Optional burst/thunder/roar SFX when the Combat Boss manifests in the VFX.")]
    public AudioClip bossSpawnSfx;

    [Header("Timing Options")]
    [Tooltip("If true, waits for the full dialogue audio to finish before swapping to the fight, even if the player runs closer.")]
    public bool waitForDialogueToFinish = true;

    private bool hasPlayedDialogue = false;
    private bool battleStarted = false;

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        // Ensure the piano performer is visible and the combat boss is hidden at start
        if (pianoPerformerObject != null) pianoPerformerObject.SetActive(true);
        
        // Only deactivate combatBossObject if it's an object in the scene (not a project prefab asset)
        if (combatBossObject != null && combatBossObject.scene.IsValid())
        {
            combatBossObject.SetActive(false);
        }
    }

    void Update()
    {
        if (battleStarted || playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        // 1. Outer Ring: Play Boss Voice Dialogue
        if (!hasPlayedDialogue && distance <= dialogueTriggerRadius)
        {
            hasPlayedDialogue = true;
            PlayBossDialogue();
        }

        // 2. Inner Ring: Trigger Transition to Boss Fight
        if (distance <= battleStartRadius)
        {
            // Check if we should wait for the monologue to finish playing first
            if (waitForDialogueToFinish && bossVoiceSource != null && bossVoiceSource.isPlaying)
            {
                return;
            }

            battleStarted = true;
            StartCoroutine(TransitionToBossBattle());
        }
    }

    private void PlayBossDialogue()
    {
        // Duck the piano music slightly so the voice is clearly audible in VR
        if (pianoBgmSource != null)
        {
            pianoBgmSource.volume = 0.3f;
        }

        if (bossVoiceSource != null && bossDialogueClip != null)
        {
            bossVoiceSource.clip = bossDialogueClip;
            bossVoiceSource.Play();
        }
    }

    private IEnumerator TransitionToBossBattle()
    {
        // 1. Stop Piano BGM & play dramatic lights-out sound
        if (pianoBgmSource != null) pianoBgmSource.Stop();

        if (lightsOutSfx != null)
        {
            if (bossVoiceSource != null)
            {
                bossVoiceSource.spatialBlend = 0f;
                bossVoiceSource.PlayOneShot(lightsOutSfx, 1.5f);
            }
            else
            {
                Vector3 earPos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(lightsOutSfx, earPos, 1.0f);
            }
        }

        // 2. Spawn vanish effect at the piano's position, shut off Spotlight, and disable Piano + Performer
        if (pianoPerformerObject != null)
        {
            if (pianoVanishVfxPrefab != null)
            {
                Vector3 vanishPos = pianoPerformerObject.transform.position + Vector3.up * vfxHeightOffset;
                GameObject vanishVfx = Instantiate(pianoVanishVfxPrefab, vanishPos, Quaternion.identity);
                Destroy(vanishVfx, vfxLifetime);
            }

            pianoPerformerObject.SetActive(false);
        }

        if (stageSpotlight != null) stageSpotlight.enabled = false;

        // Brief pause in the dark for tension before the boss manifests
        yield return new WaitForSeconds(1.0f);

        // Determine spawn position and rotation for the Combat Boss
        Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : transform.position;
        Quaternion spawnRot = bossSpawnPoint != null ? bossSpawnPoint.rotation : transform.rotation;

        // 3. Trigger Boss Spawn Effect & Sound first so the mesh emerges from inside the particle burst
        if (bossSpawnVfxPrefab != null)
        {
            Vector3 vfxPos = spawnPos + Vector3.up * vfxHeightOffset;
            GameObject spawnVfx = Instantiate(bossSpawnVfxPrefab, vfxPos, spawnRot);
            Destroy(spawnVfx, vfxLifetime);
        }

        if (bossSpawnSfx != null)
        {
            AudioSource.PlayClipAtPoint(bossSpawnSfx, spawnPos, 1.0f);
        }

        // Wait a fraction of a second for particles to expand and cover the spawn point
        if (bossAppearDelay > 0f)
        {
            yield return new WaitForSeconds(bossAppearDelay);
        }

        // 4. Activate or Spawn the Combat Boss
        GameObject activeBoss = null;

        if (combatBossObject != null)
        {
            if (combatBossObject.scene.IsValid())
            {
                combatBossObject.transform.position = spawnPos;
                combatBossObject.transform.rotation = spawnRot;
                combatBossObject.SetActive(true);
                activeBoss = combatBossObject;
            }
            else
            {
                activeBoss = Instantiate(combatBossObject, spawnPos, spawnRot);
            }
        }

        // 5. Start the BossAI battle loop immediately
        if (activeBoss != null)
        {
            BossAI ai = activeBoss.GetComponent<BossAI>();
            if (ai != null)
            {
                ai.enabled = true;
                ai.playerTransform = playerTransform;
                ai.ForceStartSpawningPhase();
            }
        }

        // Disable this intro trigger now that the fight is active
        this.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, dialogueTriggerRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, battleStartRadius);
    }
}