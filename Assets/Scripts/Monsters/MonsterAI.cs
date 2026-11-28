using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum InstrumentType
{
    Trumpet,
    Drums,
    Violin,
    Flute
}

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class MonsterAI : MonoBehaviour
{
    [Header("Monster Classification")]
    [Tooltip("The specific instrument required to scare away or banish this monster.")]
    public InstrumentType requiredInstrument;

    [Header("Instrument Reaction")]
    public float hearDistance = 15f;
    public float banishDistance = 4f;
    public float banishHoldTime = 0.5f;
    public float fleeSpeed = 3.5f;
    public float fleeStep = 6f;
    private float lastHeardTime = -10f;
    private InstrumentType lastHeardType;
    private float banishTimer;

    [Header("Targets & Respawn")]
    public Transform playerTransform;
    [Tooltip("Used in the Backrooms scene. Drag your central hub here or tag it 'Respawn'.")]
    public Transform backroomsCenterPoint;

    [Header("Horror Pacing & Speeds")]
    [Tooltip("Slow, creepy stalking speed when far from the player.")]
    public float stalkSpeed = 1.8f;
    [Tooltip("Sudden burst sprint speed when close to the player.")]
    public float sprintSpeed = 5.2f;
    [Tooltip("Distance at which the monster switches from stalking to sprinting.")]
    public float sprintTriggerDistance = 8f;

    [Header("Jumpscare Settings")]
    [Tooltip("Distance from player to trigger the in-your-face jumpscare.")]
    public float jumpscareDistance = 1.6f;
    [Tooltip("How close to the VR camera face the monster snaps during the scream.")]
    public float faceSnapDistance = 0.85f;
    [Tooltip("Vertical offset so the monster's head aligns with the player's eye level.")]
    public float faceHeightOffset = -1.2f;
    [Tooltip("Duration of the scream before teleporting the player back.")]
    public float jumpscareDuration = 2.2f;
    [Tooltip("Adjust if your 3D model's face is rotated (e.g., 180 if it turns its back to you).")]
    public float modelForwardYawOffset = 0f;

    [Header("Audio Cues")]
    [Tooltip("Loud scream SFX played directly in the player's ears during the jumpscare.")]
    public AudioClip jumpscareScreamClip;
    [Tooltip("Optional screech when the monster spots you and starts sprinting.")]
    public AudioClip aggroScreechClip;
    [Tooltip("Sound played when the monster is successfully banished by the matching instrument.")]
    public AudioClip banishSfxClip;

    [Header("Banishment VFX & Polish")]
    [Tooltip("Particle system prefab (e.g., dark smoke, ash, or glowing musical notes) spawned when banished.")]
    public GameObject banishParticlePrefab;
    [Tooltip("Height offset from the monster's feet where the particles burst (e.g., chest height).")]
    public float particleSpawnHeight = 1.2f;
    [Tooltip("How long the monster visibly dissolves/shrinks before being destroyed.")]
    public float banishDuration = 1.2f;
    [Tooltip("Optional Animator trigger name for the monster recoiling in pain (e.g., 'Scream' or 'Recoil').")]
    public string banishAnimTrigger = "Scream";

    [HideInInspector]
    public bool isBossArenaMinion = false;

    public static System.Collections.Generic.List<MonsterAI> all = new System.Collections.Generic.List<MonsterAI>();

    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource loopAudioSource;

    private bool isSprinting = false;
    private bool isJumpscaring = false;
    private bool isBanished = false;

    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;

    // Animator Hashes
    private readonly int AnimSpeed = Animator.StringToHash("Speed");
    private readonly int AnimScream = Animator.StringToHash("Scream");

    void OnEnable()  { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    public void HearInstrument(InstrumentType type)
    {
        lastHeardTime = Time.time;
        lastHeardType = type;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        loopAudioSource = GetComponent<AudioSource>();

        // Record spawn transform so Boss Arena monsters can warp back after a jumpscare
        initialSpawnPosition = transform.position;
        initialSpawnRotation = transform.rotation;

        // Auto-detect if this monster is in the Conductor Boss scene
        if (BossArenaPenaltyManager.Instance != null || FindFirstObjectByType<BossAI>() != null)
        {
            isBossArenaMinion = true;
        }

        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        // Auto-find Backrooms central hub respawn point by tag
        if (backroomsCenterPoint == null)
        {
            GameObject respawnObj = GameObject.FindGameObjectWithTag("Respawn");
            if (respawnObj != null) backroomsCenterPoint = respawnObj.transform;
        }

        agent.speed = stalkSpeed;
    }

    void Update()
    {
        if (playerTransform == null || isJumpscaring || isBanished) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool hears = Time.time - lastHeardTime < 0.15f && distanceToPlayer < hearDistance;

        if (hears && lastHeardType == requiredInstrument)
        {
            if (distanceToPlayer < banishDistance)
            {
                banishTimer += Time.deltaTime;
                if (banishTimer >= banishHoldTime)
                {
                    StartCoroutine(ExecuteBanishSequence());
                    return;
                }
            }
            else
            {
                banishTimer = 0f;
            }

            agent.speed = fleeSpeed;
            Flee();
            animator.SetFloat(AnimSpeed, agent.velocity.magnitude);
            return; // Skip jumpscare and chase while the right instrument is playing
        }

        banishTimer = 0f;

        // Wrong instrument angers it
        if (hears && !isSprinting) TriggerFrenzySprint();

        // Restore the correct chase speed after fleeing
        agent.speed = isSprinting ? sprintSpeed : stalkSpeed;

        // 1. Check Jumpscare Trigger
        if (distanceToPlayer <= jumpscareDistance)
        {
            StartCoroutine(ExecuteJumpscareSequence());
            return;
        }

        // 2. Dynamic Pacing: Stalk -> Sudden Frenzy Sprint
        if (distanceToPlayer <= sprintTriggerDistance && !isSprinting)
        {
            TriggerFrenzySprint();
        }
        else if (distanceToPlayer > sprintTriggerDistance * 1.3f && isSprinting)
        {
            isSprinting = false;
            agent.speed = stalkSpeed;
            if (loopAudioSource != null) loopAudioSource.pitch = 1.0f;
        }

        // 3. Pursue Player
        agent.SetDestination(playerTransform.position);

        // 4. Update Animator Blend Tree / Transitions
        float currentSpeed = agent.velocity.magnitude;
        animator.SetFloat(AnimSpeed, currentSpeed);
    }

    private void Flee()
    {
        Vector3 away = transform.position - playerTransform.position;
        away.y = 0f;
        away.Normalize();

        Vector3 target = transform.position + away * fleeStep;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    public bool TryBanishWithInstrument(InstrumentType playedInstrument)
    {
        if (isJumpscaring || isBanished) return false;

        if (playedInstrument == requiredInstrument)
        {
            StartCoroutine(ExecuteBanishSequence());
            return true;
        }
        else
        {
            if (!isSprinting) TriggerFrenzySprint();
            return false;
        }
    }

    private IEnumerator ExecuteBanishSequence()
    {
        isBanished = true;

        // Only count toward Backrooms completion if NOT in the Conductor Boss fight
        if (!isBossArenaMinion)
        {
            GameSession.MonstersDefeated++;
        }

        // 1. Immediately disable movement and colliders
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 2. Stop ambient loop audio and play banishment SFX
        if (loopAudioSource != null) loopAudioSource.Stop();

        if (banishSfxClip != null)
        {
            AudioSource.PlayClipAtPoint(banishSfxClip, transform.position, 1.0f);
        }

        // 3. Play agony/recoil animation
        if (animator != null && !string.IsNullOrEmpty(banishAnimTrigger))
        {
            animator.SetFloat(AnimSpeed, 0f);
            animator.SetTrigger(banishAnimTrigger);
        }

        // 4. Spawn standalone particle burst at chest height
        if (banishParticlePrefab != null)
        {
            Vector3 vfxPosition = transform.position + Vector3.up * particleSpawnHeight;
            GameObject vfxInstance = Instantiate(banishParticlePrefab, vfxPosition, Quaternion.identity);
            Destroy(vfxInstance, 4f);
        }

        // 5. Smoothly shrink and sink the monster into the floor
        Vector3 initialScale = transform.localScale;
        Vector3 initialPos = transform.position;
        float elapsed = 0f;

        while (elapsed < banishDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / banishDuration;

            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, progress * progress);
            transform.position = Vector3.Lerp(initialPos, initialPos + Vector3.down * 0.5f, progress);

            yield return null;
        }

        // 6. Destroy the monster (BossAI automatically detects null in activeWaveMonsters)
        Destroy(gameObject);
    }

    private void TriggerFrenzySprint()
    {
        isSprinting = true;
        agent.speed = sprintSpeed;

        if (loopAudioSource != null)
        {
            loopAudioSource.pitch = 1.25f;
        }

        if (aggroScreechClip != null)
        {
            AudioSource.PlayClipAtPoint(aggroScreechClip, transform.position, 1.0f);
        }
    }

    private IEnumerator ExecuteJumpscareSequence()
    {
        isJumpscaring = true;

        // 1. Stop NavMesh movement and disable collider
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.enabled = false;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (loopAudioSource != null) loopAudioSource.Stop();

        // 2. Freeze Player Movement during the scare
        VRPlayerController playerController = playerTransform.GetComponent<VRPlayerController>();
        CharacterController charController = playerTransform.GetComponent<CharacterController>();
        if (playerController != null) playerController.enabled = false;

        // 3. Find the active VR Camera
        Camera playerCam = playerTransform.GetComponentInChildren<Camera>();
        Transform camTransform = playerCam != null ? playerCam.transform : (Camera.main != null ? Camera.main.transform : playerTransform);

        // 4. Trigger Scream Animation & Loud Audio
        animator.SetFloat(AnimSpeed, 0f);
        animator.SetTrigger(AnimScream);

        if (jumpscareScreamClip != null)
        {
            AudioSource.PlayClipAtPoint(jumpscareScreamClip, camTransform.position, 1.0f);
        }

        // 5. Keep the monster locked in front of the player's gaze and facing them
        float elapsed = 0f;
        while (elapsed < jumpscareDuration)
        {
            elapsed += Time.deltaTime;

            Vector3 flatForward = camTransform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = playerTransform.forward;
            flatForward.Normalize();

            Vector3 scarePosition = camTransform.position + (flatForward * faceSnapDistance);
            scarePosition.y = camTransform.position.y + faceHeightOffset;
            transform.position = scarePosition;

            Quaternion lookRotation = Quaternion.LookRotation(-flatForward, Vector3.up);
            transform.rotation = lookRotation * Quaternion.Euler(0f, modelForwardYawOffset, 0f);

            yield return null;
        }

        // 6. Apply Scene-Specific Teleport & Reset Behavior
        if (isBossArenaMinion)
        {
            // BOSS SCENE: Teleport player to back of hall + spawn escalating props
            if (BossArenaPenaltyManager.Instance != null)
            {
                BossArenaPenaltyManager.Instance.TriggerPlayerHitPenalty(playerTransform);
            }
            else if (backroomsCenterPoint != null)
            {
                TeleportPlayerTo(backroomsCenterPoint, charController);
            }

            // Re-enable player controls
            if (playerController != null) playerController.enabled = true;

            // Warp monster back to its stage spawn point to keep hunting
            transform.position = initialSpawnPosition;
            transform.rotation = initialSpawnRotation;
            isSprinting = false;

            if (col != null) col.enabled = true;
            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(initialSpawnPosition);
                agent.speed = stalkSpeed;
                agent.isStopped = false;
            }
            if (loopAudioSource != null)
            {
                loopAudioSource.pitch = 1.0f;
                loopAudioSource.Play();
            }

            isJumpscaring = false;
            yield break;
        }
        else
        {
            // BACKROOMS SCENE: Teleport player to central hub & despawn monster
            if (backroomsCenterPoint != null)
            {
                TeleportPlayerTo(backroomsCenterPoint, charController);
            }
            else
            {
                Debug.LogWarning("[MonsterAI] No Backrooms Center Point assigned or tagged 'Respawn'!");
            }

            if (playerController != null) playerController.enabled = true;
            Destroy(gameObject);
        }
    }

    private void TeleportPlayerTo(Transform destination, CharacterController charController)
    {
        if (charController != null) charController.enabled = false;

        playerTransform.position = destination.position;
        playerTransform.rotation = destination.rotation;

        if (charController != null) charController.enabled = true;
    }
}