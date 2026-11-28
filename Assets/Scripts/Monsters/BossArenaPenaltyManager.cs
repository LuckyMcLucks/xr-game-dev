using System.Collections.Generic;
using UnityEngine;

public class BossArenaPenaltyManager : MonoBehaviour
{
    public static BossArenaPenaltyManager Instance { get; private set; }

    [Header("Player Reset Settings")]
    [Tooltip("Transform at the back of the concert hall where the player is sent when hit/scared.")]
    public Transform arenaStartPoint;
    public Transform playerTransform;
    [Tooltip("Optional sound played when the player is knocked back to the start.")]
    public AudioClip teleportPenaltySfx;

    [Header("Escalating Prop Spawning")]
    [Tooltip("List of obstacle prefabs (e.g., overturned chairs, music stands, debris) to spawn.")]
    public List<GameObject> propPrefabs;
    [Tooltip("How many props spawn on the very first hit/jumpscare.")]
    public int initialPropSpawnCount = 3;
    [Tooltip("How many EXTRA props are added on top of the previous amount each subsequent hit.")]
    public int extraPropsPerHit = 2;
    [Tooltip("Maximum total props allowed in the arena so VR performance stays smooth.")]
    public int maxTotalProps = 45;

    [Header("Spawn Zone & Orientation")]
    [Tooltip("Center of the area between the start point and the stage where props will spawn.")]
    public Transform spawnZoneCenter;
    [Tooltip("Width (X), Height (Y), and Length (Z) of the random prop spawn area.")]
    public Vector3 spawnAreaSize = new Vector3(14f, 4f, 16f);
    [Tooltip("LayerMask for the concert hall floor so props snap cleanly onto the ground.")]
    public LayerMask floorLayerMask = ~0;
    [Tooltip("Minimum distance from the player's start point so props don't trap them immediately.")]
    public float safeRadiusAroundStart = 2.5f;
    [Tooltip("If true, randomizes Y rotation (360°) with slight tilt. If false, randomizes full 3D X/Y/Z rotation.")]
    public bool uprightRandomOrientation = true;

    private int hitCount = 0;
    private readonly List<GameObject> spawnedProps = new List<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }
    }

    /// <summary>
    /// Teleports the player back to the start position and spawns an escalating wave of obstacle props.
    /// </summary>
    public void TriggerPlayerHitPenalty(Transform targetPlayer = null)
    {
        Transform player = targetPlayer != null ? targetPlayer : playerTransform;
        if (player == null) return;

        hitCount++;

        // 1. Teleport player back to the Arena Start Position
        if (arenaStartPoint != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.position = arenaStartPoint.position;
            player.rotation = arenaStartPoint.rotation;

            if (cc != null) cc.enabled = true;
        }
        else
        {
            Debug.LogWarning("[BossArenaPenaltyManager] Arena Start Point is not assigned!");
        }

        if (teleportPenaltySfx != null)
        {
            AudioSource.PlayClipAtPoint(teleportPenaltySfx, player.position, 1.0f);
        }

        // 2. Calculate escalating prop count (e.g., Hit 1 = 3, Hit 2 = 5, Hit 3 = 7...)
        int propsToSpawn = initialPropSpawnCount + ((hitCount - 1) * extraPropsPerHit);
        SpawnRandomObstacleProps(propsToSpawn);
    }

    private void SpawnRandomObstacleProps(int count)
    {
        if (propPrefabs == null || propPrefabs.Count == 0) return;

        Vector3 center = spawnZoneCenter != null ? spawnZoneCenter.position : transform.position;
        Vector3 halfExtents = spawnAreaSize * 0.5f;

        int spawnedThisCall = 0;
        int safetyAttempts = count * 5; // Prevent infinite loops if safe radius blocks points

        while (spawnedThisCall < count && spawnedProps.Count < maxTotalProps && safetyAttempts > 0)
        {
            safetyAttempts--;

            // Pick a random X and Z inside the spawn zone
            float randX = Random.Range(-halfExtents.x, halfExtents.x);
            float randZ = Random.Range(-halfExtents.z, halfExtents.z);
            Vector3 candidatePos = center + new Vector3(randX, halfExtents.y, randZ);

            // Raycast straight down to snap the prop onto the floor
            if (Physics.Raycast(candidatePos, Vector3.down, out RaycastHit hit, spawnAreaSize.y + 5f, floorLayerMask))
            {
                candidatePos = hit.point;
            }
            else
            {
                candidatePos.y = center.y;
            }

            // Ensure prop doesn't spawn directly on top of the player's start point
            if (arenaStartPoint != null && Vector3.Distance(candidatePos, arenaStartPoint.position) < safeRadiusAroundStart)
            {
                continue;
            }

            // Pick random prefab and random orientation
            GameObject prefab = propPrefabs[Random.Range(0, propPrefabs.Count)];
            Quaternion randomRot = uprightRandomOrientation
                ? Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f))
                : Random.rotation;

            GameObject newProp = Instantiate(prefab, candidatePos, randomRot, transform);
            spawnedProps.Add(newProp);
            spawnedThisCall++;
        }

        Debug.Log($"[BossArenaPenaltyManager] Player hit #{hitCount}. Spawned {spawnedThisCall} props (Total in arena: {spawnedProps.Count}).");
    }

    /// <summary>
    /// Optional: Call this if you restart the boss fight to clear all spawned penalty props.
    /// </summary>
    public void ClearAllSpawnedProps()
    {
        foreach (GameObject prop in spawnedProps)
        {
            if (prop != null) Destroy(prop);
        }
        spawnedProps.Clear();
        hitCount = 0;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = spawnZoneCenter != null ? spawnZoneCenter.position : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.35f);
        Gizmos.DrawWireCube(center, spawnAreaSize);

        if (arenaStartPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(arenaStartPoint.position, safeRadiusAroundStart);
        }
    }
}