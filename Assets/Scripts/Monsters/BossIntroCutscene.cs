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

    [Header("Audio References")]
    [Tooltip("AudioSource playing the looping slow piano BGM.")]
    public AudioSource pianoBgmSource;
    [Tooltip("3D AudioSource for the boss's intro voice line (put on StageController so it doesn't cut off when piano object disables).")]
    public AudioSource bossVoiceSource;
    public AudioClip bossDialogueClip;
    [Tooltip("Optional heavy power-down / slam SFX when the spotlight cuts out.")]
    public AudioClip lightsOutSfx;

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
                // Temporarily switch to 2D so the blackout slam feels visceral and loud everywhere
                bossVoiceSource.spatialBlend = 0f;
                bossVoiceSource.PlayOneShot(lightsOutSfx, 1.5f); // PlayOneShot supports > 1.0f volume scale
            }
            else
            {
                // Fallback: spawn at the player's camera position instead of the distant stage
                Vector3 earPos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(lightsOutSfx, earPos, 1.0f);
            }
        }

        // 2. Shut off Spotlight and disable the Piano + Performer GameObject
        if (stageSpotlight != null) stageSpotlight.enabled = false;
        if (pianoPerformerObject != null) pianoPerformerObject.SetActive(false);

        // Brief 1-second pause in the dark for tension
        yield return new WaitForSeconds(1.0f);

        // 3. Activate or Spawn the Combat Boss
        GameObject activeBoss = null;

        if (combatBossObject != null)
        {
            // Check if combatBossObject is already in the scene (inactive) or a Prefab asset
            if (combatBossObject.scene.IsValid())
            {
                if (bossSpawnPoint != null)
                {
                    combatBossObject.transform.position = bossSpawnPoint.position;
                    combatBossObject.transform.rotation = bossSpawnPoint.rotation;
                }
                combatBossObject.SetActive(true);
                activeBoss = combatBossObject;
            }
            else
            {
                // It's a Prefab, so Instantiate it
                Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : transform.position;
                Quaternion spawnRot = bossSpawnPoint != null ? bossSpawnPoint.rotation : transform.rotation;
                activeBoss = Instantiate(combatBossObject, spawnPos, spawnRot);
            }
        }

        // 4. Start the BossAI battle loop immediately
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