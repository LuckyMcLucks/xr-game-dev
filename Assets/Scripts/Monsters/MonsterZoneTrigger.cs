using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class MonsterZoneTrigger : MonoBehaviour
{
    [Header("Zone Configuration")]
    public float zoneRadius = 18f;
    public Color gizmoColor = new Color(0.8f, 0.1f, 0.1f, 0.35f);

    [Header("Monster & Spawn Setup")]
    public GameObject monsterPrefab;
    [Tooltip("Multiple hidden spawn points around corners so the monster never spawns in plain sight.")]
    public Transform[] hiddenSpawnPoints;
    public string playerTag = "Player";

    [Header("Horror Pacing & Tension")]
    [Tooltip("Delay after entering the zone before the monster spawns (builds anticipation).")]
    public float spawnDelay = 1.5f;
    [Tooltip("Seconds before despawning after player leaves zone (prevents jarring pop-out).")]
    public float despawnGraceTime = 3.0f;
    [Tooltip("Sound played at the spawn location right as the monster manifests.")]
    public AudioClip spawnStingerClip;

    [Header("3D Monster Audio Settings")]
    [Tooltip("Looping ambient terror sound attached to the monster (footsteps, breathing, static).")]
    public AudioClip monsterLoopAudio;
    public float minHearingDistance = 2f;
    public float maxHearingDistance = 20f;

    private static GameObject activeMonster;
    private static MonsterZoneTrigger currentActiveZone;

    private SphereCollider sphereCollider;
    private Coroutine spawnCoroutine;
    private Coroutine despawnCoroutine;

    void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = zoneRadius;
    }

    void OnValidate()
    {
        if (sphereCollider == null) sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider != null) sphereCollider.radius = zoneRadius;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // If player re-entered this zone during grace period, cancel despawn
        if (despawnCoroutine != null)
        {
            StopCoroutine(despawnCoroutine);
            despawnCoroutine = null;
        }

        // Only start spawn sequence if this zone isn't already the active one
        if (currentActiveZone != this || activeMonster == null)
        {
            if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
            spawnCoroutine = StartCoroutine(DelayedHorrorSpawn(other.transform));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // Cancel pending spawn if player backed out immediately
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        if (currentActiveZone == this && activeMonster != null)
        {
            despawnCoroutine = StartCoroutine(GracefulDespawn());
        }
    }

    private IEnumerator DelayedHorrorSpawn(Transform playerTransform)
    {
        // 1. Clean up any monster from a previous zone
        DespawnImmediate();
        currentActiveZone = this;

        // 2. Pick the best hidden spawn point out of the player's direct view
        Transform chosenPoint = GetBestHiddenSpawnPoint(playerTransform);
        Vector3 spawnPos = chosenPoint != null ? chosenPoint.position : transform.position;
        Quaternion spawnRot = chosenPoint != null ? chosenPoint.rotation : Quaternion.identity;

        // 3. Play distant audio stinger BEFORE monster appears to alert the player's ears first
        if (spawnStingerClip != null)
        {
            AudioSource.PlayClipAtPoint(spawnStingerClip, spawnPos, 1.0f);
        }

        yield return new WaitForSeconds(spawnDelay);

        if (monsterPrefab == null) yield break;

        // 4. Instantiate Monster
        activeMonster = Instantiate(monsterPrefab, spawnPos, spawnRot);

        // 5. Configure 3D Directional Audio on the Monster
        SetupMonster3DAudio(activeMonster);

        // 6. Connect AI target
        MonsterAI ai = activeMonster.GetComponent<MonsterAI>();
        if (ai != null)
        {
            ai.playerTransform = playerTransform;
        }

        spawnCoroutine = null;
    }

    private IEnumerator GracefulDespawn()
    {
        yield return new WaitForSeconds(despawnGraceTime);

        // Wait until player isn't staring directly at the monster before vanishing (optional immersion check)
        float safetyTimeout = 4f;
        while (activeMonster != null && IsVisibleToCamera(activeMonster.transform.position) && safetyTimeout > 0f)
        {
            safetyTimeout -= Time.deltaTime;
            yield return null;
        }

        DespawnImmediate();
        despawnCoroutine = null;
    }

    private void DespawnImmediate()
    {
        if (activeMonster != null)
        {
            Destroy(activeMonster);
            activeMonster = null;
        }
        if (currentActiveZone == this)
        {
            currentActiveZone = null;
        }
    }

    private Transform GetBestHiddenSpawnPoint(Transform player)
    {
        if (hiddenSpawnPoints == null || hiddenSpawnPoints.Length == 0) return null;

        // Prioritize points the player is NOT currently looking at
        foreach (Transform point in hiddenSpawnPoints)
        {
            if (point != null && !IsVisibleToCamera(point.position))
            {
                return point;
            }
        }

        // Fallback to first available point
        return hiddenSpawnPoints[0];
    }

    private bool IsVisibleToCamera(Vector3 worldPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 viewportPoint = cam.WorldToViewportPoint(worldPos);
        bool inFront = viewportPoint.z > 0;
        bool inScreen = viewportPoint.x >= 0 && viewportPoint.x <= 1 && viewportPoint.y >= 0 && viewportPoint.y <= 1;

        if (inFront && inScreen)
        {
            // Check if a wall is blocking line of sight
            if (Physics.Linecast(cam.transform.position, worldPos))
            {
                return false; // Wall blocks view, so it's safely hidden
            }
            return true;
        }
        return false;
    }

    private void SetupMonster3DAudio(GameObject monster)
    {
        if (monsterLoopAudio == null) return;

        AudioSource audioSource = monster.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = monster.AddComponent<AudioSource>();
        }

        audioSource.clip = monsterLoopAudio;
        audioSource.loop = true;

        // Crucial 3D Directional Audio settings for VR
        audioSource.spatialBlend = 1.0f; // 1.0 = Full 3D directional audio, 0.0 = 2D flat audio
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = minHearingDistance;
        audioSource.maxDistance = maxHearingDistance;
        audioSource.dopplerLevel = 0.5f; // Subtle pitch shift as monster rushes past

        audioSource.Play();
    }

    private void OnDisable()
    {
        DespawnImmediate();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, zoneRadius);

        // Draw lines to all hidden spawn points so you can visualize ambush spots
        if (hiddenSpawnPoints != null)
        {
            Gizmos.color = Color.yellow;
            foreach (Transform pt in hiddenSpawnPoints)
            {
                if (pt != null)
                {
                    Gizmos.DrawLine(transform.position, pt.position);
                    Gizmos.DrawWireSphere(pt.position, 0.5f);
                }
            }
        }
    }
}