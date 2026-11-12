using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class MonsterAI : MonoBehaviour
{
    [Header("Targets & Respawn")]
    public Transform playerTransform;
    [Tooltip("Drag your central Backrooms hub Transform here, or tag an empty object 'RespawnPoint'.")]
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

    [Header("Audio Cues")]
    [Tooltip("Loud scream SFX played directly in the player's ears during the jumpscare.")]
    public AudioClip jumpscareScreamClip;
    [Tooltip("Optional screech when the monster spots you and starts sprinting.")]
    public AudioClip aggroScreechClip;

    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource loopAudioSource;

    private bool isSprinting = false;
    private bool isJumpscaring = false;

    // Animator Hashes
    private readonly int AnimSpeed = Animator.StringToHash("Speed");
    private readonly int AnimScream = Animator.StringToHash("Scream");

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        loopAudioSource = GetComponent<AudioSource>();

        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        // Auto-find central hub respawn point by tag if not assigned in Inspector
        if (backroomsCenterPoint == null)
        {
            GameObject respawnObj = GameObject.FindGameObjectWithTag("Respawn");
            if (respawnObj != null) backroomsCenterPoint = respawnObj.transform;
        }

        agent.speed = stalkSpeed;
    }

    void Update()
    {
        if (playerTransform == null || isJumpscaring) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

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
            // Calm back down to stalking if player manages to gain significant distance
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

    private void TriggerFrenzySprint()
    {
        isSprinting = true;
        agent.speed = sprintSpeed;

        // Increase ambient loop pitch to heighten panic
        if (loopAudioSource != null)
        {
            loopAudioSource.pitch = 1.25f;
        }

        // Play aggro screech
        if (aggroScreechClip != null)
        {
            AudioSource.PlayClipAtPoint(aggroScreechClip, transform.position, 1.0f);
        }
    }

    private IEnumerator ExecuteJumpscareSequence()
    {
        isJumpscaring = true;

        // 1. Stop NavMesh movement immediately so it doesn't push the player around
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.enabled = false;

        // Stop ambient footsteps/breathing loop so the scream dominates
        if (loopAudioSource != null) loopAudioSource.Stop();

        // 2. Freeze Player Movement during the scare
        VRPlayerController playerController = playerTransform.GetComponent<VRPlayerController>();
        CharacterController charController = playerTransform.GetComponent<CharacterController>();
        if (playerController != null) playerController.enabled = false;

        // 3. Snap Monster right in front of the player's VR camera / eyes
        Transform camTransform = Camera.main != null ? Camera.main.transform : playerTransform;
        Vector3 flatForward = camTransform.forward;
        flatForward.y = 0f;
        flatForward.Normalize();

        Vector3 scarePosition = camTransform.position + (flatForward * faceSnapDistance);
        scarePosition.y += faceHeightOffset;
        transform.position = scarePosition;

        // Make the monster stare directly at the camera
        Vector3 lookTarget = new Vector3(camTransform.position.x, transform.position.y, camTransform.position.z);
        transform.LookAt(lookTarget);

        // 4. Trigger Scream Animation & Loud Audio
        animator.SetFloat(AnimSpeed, 0f);
        animator.SetTrigger(AnimScream);

        if (jumpscareScreamClip != null)
        {
            AudioSource.PlayClipAtPoint(jumpscareScreamClip, camTransform.position, 1.0f);
        }

        // 5. Hold the jumpscare in the player's face
        yield return new WaitForSeconds(jumpscareDuration);

        // 6. Teleport Player back to the center of the Backrooms
        if (backroomsCenterPoint != null)
        {
            // Must disable CharacterController momentarily to teleport cleanly in Unity
            if (charController != null) charController.enabled = false;
            
            playerTransform.position = backroomsCenterPoint.position;
            playerTransform.rotation = backroomsCenterPoint.rotation;
            
            if (charController != null) charController.enabled = true;
        }
        else
        {
            Debug.LogWarning("[MonsterAI] No Backrooms Center Point assigned or tagged 'Respawn'!");
        }

        // Re-enable player controls
        if (playerController != null) playerController.enabled = true;

        // 7. Despawn the monster
        Destroy(gameObject);
    }
}