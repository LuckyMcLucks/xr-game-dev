using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossState
{
    SpawningWave,
    Commanding,
    VulnerableTerrified,
    Dead
}

[RequireComponent(typeof(Animator))]
public class BossAI : MonoBehaviour
{
    [Header("Wave Progression Settings")]
    [Tooltip("Number of monsters to spawn in each wave (e.g., Wave 1 = 2, Wave 2 = 4, Wave 3 = 6).")]
    public int[] monstersPerWave = new int[] { 2, 4, 6 };
    [Tooltip("Current active wave (1-based index for easy reading in Inspector).")]
    public int currentWave = 0;
    public BossState currentState;

    [Header("Spawn Settings")]
    [Tooltip("Your 4 monster prefabs (Trumpet, Drums, Violin, Flute).")]
    public List<GameObject> waveMonsterPrefabs;
    [Tooltip("Your 4 spawn point transforms around the stage.")]
    public Transform[] spawnPoints;
    [Tooltip("Slight delay between spawning each monster in a wave so they don't overlap.")]
    public float staggerSpawnInterval = 0.4f;

    [Header("State Timers")]
    [Tooltip("How long the boss stays terrified between waves before summoning the next wave.")]
    public float vulnerableDuration = 5f;
    [Tooltip("Brief terrified reaction before the boss dies after the final wave is defeated.")]
    public float finalDeathDelay = 2.0f;

    [Header("References")]
    public Transform playerTransform;
    public GameObject shieldVisual;

    [Header("--- MANUAL ANIMATION TESTING (PLAY MODE ONLY) ---")]
    public bool testCastSpellTrigger;
    public bool testTerrifiedBool;
    private bool previousTerrifiedValue = false;
    public bool testTakeHitTrigger;
    public bool testDeathTrigger;

    private Animator animator;
    private List<GameObject> activeWaveMonsters = new List<GameObject>();

    // Animator Parameter Hashes
    private readonly int AnimIsTerrified = Animator.StringToHash("IsTerrified");
    private readonly int AnimCastSpell = Animator.StringToHash("CastSpell");
    private readonly int AnimHit = Animator.StringToHash("TakeHit");
    private readonly int AnimDie = Animator.StringToHash("Die");

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        CheckInspectorAnimationTriggers();

        // Keep boss facing the player during battle
        if (currentState != BossState.Dead && playerTransform != null)
        {
            Vector3 targetDir = playerTransform.position - transform.position;
            targetDir.y = 0f;
            if (targetDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * 3f);
            }
        }

        // Check if all monsters in the current wave have been banished
        if (currentState == BossState.Commanding)
        {
            CleanupDeadMonstersInList();
            if (activeWaveMonsters.Count == 0)
            {
                OnWaveCleared();
            }
        }
    }

    private void OnWaveCleared()
    {
        // Check if this was the final wave
        if (currentWave >= monstersPerWave.Length)
        {
            StartCoroutine(FinalBossDefeatSequence());
        }
        else
        {
            StartCoroutine(EnterVulnerableState());
        }
    }

    // --- MANUAL INSPECTOR TRIGGER LOGIC ---
    private void CheckInspectorAnimationTriggers()
    {
        if (testCastSpellTrigger)
        {
            testCastSpellTrigger = false;
            TriggerSpellCastAnimation();
        }

        if (testTerrifiedBool != previousTerrifiedValue)
        {
            previousTerrifiedValue = testTerrifiedBool;
            if (animator != null)
            {
                animator.SetBool(AnimIsTerrified, testTerrifiedBool);
            }
        }

        if (testTakeHitTrigger)
        {
            testTakeHitTrigger = false;
            TriggerTakeHitAnimation();
        }

        if (testDeathTrigger)
        {
            testDeathTrigger = false;
            TriggerDeathAnimation();
        }
    }

    [ContextMenu("Test Animation: Spell Cast")]
    public void TriggerSpellCastAnimation()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animator.SetTrigger(AnimCastSpell);
    }

    [ContextMenu("Test Animation: Toggle Terrified ON")]
    public void EnableTerrifiedAnimation()
    {
        if (animator == null) animator = GetComponent<Animator>();
        testTerrifiedBool = true;
        animator.SetBool(AnimIsTerrified, true);
    }

    [ContextMenu("Test Animation: Toggle Terrified OFF")]
    public void DisableTerrifiedAnimation()
    {
        if (animator == null) animator = GetComponent<Animator>();
        testTerrifiedBool = false;
        animator.SetBool(AnimIsTerrified, false);
    }

    [ContextMenu("Test Animation: Take Hit")]
    public void TriggerTakeHitAnimation()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animator.SetTrigger(AnimHit);
    }

    [ContextMenu("Test Animation: Death")]
    public void TriggerDeathAnimation()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animator.SetTrigger(AnimDie);
    }

    // --- GAMEPLAY PHASES ---

    [ContextMenu("Start Boss Fight (Wave 1)")]
    public void ForceStartSpawningPhase()
    {
        StopAllCoroutines();
        currentWave = 0; // Reset to 0 so the first StartSpawningPhase() increments to Wave 1
        StartCoroutine(StartSpawningPhase());
    }

    private IEnumerator StartSpawningPhase()
    {
        if (animator == null) animator = GetComponent<Animator>();

        currentWave++;
        currentState = BossState.SpawningWave;

        int waveIndex = Mathf.Clamp(currentWave - 1, 0, monstersPerWave.Length - 1);
        int spawnCount = monstersPerWave[waveIndex];

        Debug.Log($"[BossAI] Starting Wave {currentWave}/{monstersPerWave.Length} — Spawning {spawnCount} monsters!");

        if (shieldVisual != null) shieldVisual.SetActive(true);

        testTerrifiedBool = false;
        animator.SetBool(AnimIsTerrified, false);
        animator.SetTrigger(AnimCastSpell);

        // Wait for spell-cast animation windup
        yield return new WaitForSeconds(1.5f);

        activeWaveMonsters.Clear();

        if (waveMonsterPrefabs == null || waveMonsterPrefabs.Count == 0 || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[BossAI] Missing Monster Prefabs or Spawn Points in Inspector!");
            yield break;
        }

        // Create shuffled lists so monster types and spawn points are varied each wave
        List<GameObject> monsterPool = BuildShuffledMonsterPool(spawnCount);
        List<Transform> shuffledSpawns = GetShuffledSpawnPoints();

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefabToSpawn = monsterPool[i];
            Transform spawnPoint = shuffledSpawns[i % shuffledSpawns.Count];

            GameObject monster = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);

            MonsterAI monsterAI = monster.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                monsterAI.playerTransform = playerTransform;
            }

            activeWaveMonsters.Add(monster);

            // Stagger spawns slightly so when spawning 6 monsters across 4 points, they don't collide
            if (staggerSpawnInterval > 0f && i < spawnCount - 1)
            {
                yield return new WaitForSeconds(staggerSpawnInterval);
            }
        }

        currentState = BossState.Commanding;
    }

    private IEnumerator EnterVulnerableState()
    {
        currentState = BossState.VulnerableTerrified;
        Debug.Log($"[BossAI] Wave {currentWave} cleared! Boss is terrified for {vulnerableDuration}s.");

        if (shieldVisual != null) shieldVisual.SetActive(false);
        testTerrifiedBool = true;
        animator.SetBool(AnimIsTerrified, true);
        animator.SetTrigger(AnimHit);

        yield return new WaitForSeconds(vulnerableDuration);

        // Proceed to next wave
        if (currentState == BossState.VulnerableTerrified)
        {
            StartCoroutine(StartSpawningPhase());
        }
    }

    private IEnumerator FinalBossDefeatSequence()
    {
        currentState = BossState.VulnerableTerrified;
        Debug.Log("[BossAI] Final wave cleared! Defeating Boss...");

        if (shieldVisual != null) shieldVisual.SetActive(false);
        testTerrifiedBool = true;
        animator.SetBool(AnimIsTerrified, true);
        animator.SetTrigger(AnimHit);

        yield return new WaitForSeconds(finalDeathDelay);

        Die();
    }

    /// <summary>
    /// Ensures an even, shuffled distribution of your 4 monster types for any wave count (2, 4, or 6).
    /// </summary>
    private List<GameObject> BuildShuffledMonsterPool(int countNeeded)
    {
        List<GameObject> pool = new List<GameObject>();

        while (pool.Count < countNeeded)
        {
            List<GameObject> batch = new List<GameObject>(waveMonsterPrefabs);
            // Fisher-Yates shuffle
            for (int i = batch.Count - 1; i > 0; i--)
            {
                int rand = Random.Range(0, i + 1);
                GameObject temp = batch[i];
                batch[i] = batch[rand];
                batch[rand] = temp;
            }
            pool.AddRange(batch);
        }

        return pool.GetRange(0, countNeeded);
    }

    private List<Transform> GetShuffledSpawnPoints()
    {
        List<Transform> spawns = new List<Transform>(spawnPoints);
        for (int i = spawns.Count - 1; i > 0; i--)
        {
            int rand = Random.Range(0, i + 1);
            Transform temp = spawns[i];
            spawns[i] = spawns[rand];
            spawns[rand] = temp;
        }
        return spawns;
    }

    private void CleanupDeadMonstersInList()
    {
        activeWaveMonsters.RemoveAll(monster => monster == null);
    }

    private void Die()
    {
        StopAllCoroutines();
        currentState = BossState.Dead;
        testTerrifiedBool = false;
        animator.SetBool(AnimIsTerrified, false);
        animator.SetTrigger(AnimDie);

        if (GetComponent<Collider>() != null) GetComponent<Collider>().enabled = false;
        this.enabled = false;
    }
}