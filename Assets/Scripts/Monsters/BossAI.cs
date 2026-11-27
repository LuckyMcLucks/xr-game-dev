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
    public int[] monstersPerWave = new int[] { 2, 4, 6 };
    public int currentWave = 0;
    public BossState currentState;

    [Header("Spawn Settings")]
    public List<GameObject> waveMonsterPrefabs;
    public Transform[] spawnPoints;
    public float staggerSpawnInterval = 0.4f;

    [Header("Active Boss Attacks (Sonic Wave)")]
    [Tooltip("Prefab of a dark sonic wave projectile that travels toward the player.")]
    public GameObject sonicWavePrefab;
    public Transform batonTipPoint;
    public float attackInterval = 6.5f;
    public AudioClip batonAttackTelegraphSfx;

    [Header("Horror Events (Blackout & Escalation)")]
    [Tooltip("Arena lights that flicker/shut off during a Crescendo Blackout.")]
    public Light[] arenaLights;
    [Tooltip("AudioSource playing the intense boss battle BGM.")]
    public AudioSource battleBgmSource;
    public AudioClip blackoutSfx;
    public float blackoutDuration = 7f;

    [Header("Interactive Instrument Counter-Play")]
    [Tooltip("The specific instrument required to strike the boss while he is terrified.")]
    public InstrumentType currentWeaknessInstrument;
    [Tooltip("Optional visual indicator (e.g., colored spotlight or floating icon) showing boss weakness.")]
    public Light bossWeaknessLight;
    public float vulnerableDuration = 10f;
    public AudioClip bossStaggerSfx;

    [Header("References")]
    public Transform playerTransform;
    public GameObject shieldVisual;

    private Animator animator;
    private List<GameObject> activeWaveMonsters = new List<GameObject>();
    private float attackTimer;
    private bool blackoutTriggeredThisWave = false;

    // Animator Hashes
    private readonly int AnimIsTerrified = Animator.StringToHash("IsTerrified");
    private readonly int AnimSummonSpell = Animator.StringToHash("SummonSpell");
    private readonly int AnimCastSpell = Animator.StringToHash("CastSpell");
    private readonly int AnimHit = Animator.StringToHash("TakeHit");
    private readonly int AnimDie = Animator.StringToHash("Die");

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (bossWeaknessLight != null) bossWeaknessLight.enabled = false;
    }

    void Update()
    {
        if (currentState == BossState.Dead || playerTransform == null) return;

        // Smoothly rotate boss to face player
        Vector3 targetDir = playerTransform.position - transform.position;
        targetDir.y = 0f;
        if (targetDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * 3.5f);
        }

        if (currentState == BossState.Commanding)
        {
            HandleActiveBossAttacks();
            HandleMidWaveBlackout();

            CleanupDeadMonstersInList();
            if (activeWaveMonsters.Count == 0)
            {
                StartCoroutine(EnterVulnerableState());
            }
        }
    }

    // --- ACTIVE BOSS ATTACKS ---
    private void HandleActiveBossAttacks()
    {
        if (sonicWavePrefab == null) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            // Attack faster in later waves
            attackTimer = Mathf.Max(3.5f, attackInterval - (currentWave * 0.8f));
            StartCoroutine(PerformSonicWaveAttack());
        }
    }

    private IEnumerator PerformSonicWaveAttack()
    {
        // 1. Audio telegraph so VR player knows to look at the boss and dodge
        if (batonAttackTelegraphSfx != null)
        {
            AudioSource.PlayClipAtPoint(batonAttackTelegraphSfx, transform.position, 1.0f);
        }

        animator.SetTrigger(AnimCastSpell);

        // Brief windup so player can react
        yield return new WaitForSeconds(0.9f);

        if (currentState != BossState.Commanding || playerTransform == null) yield break;

        // 2. Spawn Sonic Wave aimed at the player's current position
        Vector3 origin = batonTipPoint != null ? batonTipPoint.position : (transform.position + Vector3.up * 1.4f);
        Vector3 aimTarget = playerTransform.position + Vector3.up * 1.0f;
        Quaternion rotation = Quaternion.LookRotation((aimTarget - origin).normalized);

        Instantiate(sonicWavePrefab, origin, rotation);
    }

    // --- SENSORY DEPRIVATION BLACKOUT ---
    private void HandleMidWaveBlackout()
    {
        // Trigger a terrifying blackout once per wave starting from Wave 2 when half the wave is dead
        if (currentWave >= 2 && !blackoutTriggeredThisWave && activeWaveMonsters.Count <= monstersPerWave[currentWave - 1] / 2)
        {
            blackoutTriggeredThisWave = true;
            StartCoroutine(CrescendoBlackoutRoutine());
        }
    }

    private IEnumerator CrescendoBlackoutRoutine()
    {
        if (blackoutSfx != null)
        {
            Vector3 earPos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(blackoutSfx, earPos, 1.0f);
        }

        // Turn off arena lights
        SetArenaLights(false);

        yield return new WaitForSeconds(blackoutDuration);

        // Restore arena lights
        SetArenaLights(true);
    }

    private void SetArenaLights(bool state)
    {
        if (arenaLights == null) return;
        foreach (Light l in arenaLights)
        {
            if (l != null) l.enabled = state;
        }
    }

    // --- WAVE & VULNERABLE PHASES ---
    [ContextMenu("Start Boss Fight (Wave 1)")]
    public void ForceStartSpawningPhase()
    {
        StopAllCoroutines();
        currentWave = 0;
        if (battleBgmSource != null)
        {
            battleBgmSource.pitch = 1.0f;
            battleBgmSource.Play();
        }
        StartCoroutine(StartSpawningPhase());
    }

    private IEnumerator StartSpawningPhase()
    {
        if (animator == null) animator = GetComponent<Animator>();

        currentWave++;
        currentState = BossState.SpawningWave;
        blackoutTriggeredThisWave = false;
        SetArenaLights(true);

        if (shieldVisual != null) shieldVisual.SetActive(true);
        if (bossWeaknessLight != null) bossWeaknessLight.enabled = false;

        // Escalate BGM tempo with each wave
        if (battleBgmSource != null)
        {
            battleBgmSource.pitch = 1.0f + ((currentWave - 1) * 0.1f);
        }

        animator.SetBool(AnimIsTerrified, false);
        animator.SetTrigger(AnimSummonSpell);

        yield return new WaitForSeconds(1.5f);

        int waveIndex = Mathf.Clamp(currentWave - 1, 0, monstersPerWave.Length - 1);
        int spawnCount = monstersPerWave[waveIndex];

        activeWaveMonsters.Clear();
        for (int i = 0; i < spawnCount; i++)
        {
            if (waveMonsterPrefabs.Count == 0 || spawnPoints.Length == 0) break;

            GameObject prefab = waveMonsterPrefabs[Random.Range(0, waveMonsterPrefabs.Count)];
            Transform spawnPoint = spawnPoints[i % spawnPoints.Length];

            GameObject monster = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            MonsterAI ai = monster.GetComponent<MonsterAI>();
            if (ai != null) ai.playerTransform = playerTransform;

            activeWaveMonsters.Add(monster);

            if (staggerSpawnInterval > 0f && i < spawnCount - 1)
            {
                yield return new WaitForSeconds(staggerSpawnInterval);
            }
        }

        attackTimer = 3.0f;
        currentState = BossState.Commanding;
    }

    private IEnumerator EnterVulnerableState()
    {
        currentState = BossState.VulnerableTerrified;
        SetArenaLights(true);

        if (shieldVisual != null) shieldVisual.SetActive(false);

        // Pick a random instrument weakness that the player must play at the boss!
        currentWeaknessInstrument = (InstrumentType)Random.Range(0, 4);
        UpdateWeaknessVisual(currentWeaknessInstrument);

        animator.SetBool(AnimIsTerrified, true);
        animator.SetTrigger(AnimHit);

        float timer = vulnerableDuration;
        while (timer > 0f && currentState == BossState.VulnerableTerrified)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        // If player failed to strike the boss in time, he recovers and summons the wave again!
        if (currentState == BossState.VulnerableTerrified)
        {
            currentWave--; // Repeat the failed wave
            StartCoroutine(StartSpawningPhase());
        }
    }

    /// <summary>
    /// Call this from your Instrument scripts when played near the Boss!
    /// </summary>
    public bool TryStrikeBossWithInstrument(InstrumentType playedInstrument)
    {
        if (currentState != BossState.VulnerableTerrified) return false;

        if (playedInstrument == currentWeaknessInstrument)
        {
            if (bossWeaknessLight != null) bossWeaknessLight.enabled = false;
            if (bossStaggerSfx != null) AudioSource.PlayClipAtPoint(bossStaggerSfx, transform.position, 1.0f);

            animator.SetTrigger(AnimHit);

            // Check if final wave was just broken
            if (currentWave >= monstersPerWave.Length)
            {
                Die();
            }
            else
            {
                StopAllCoroutines();
                StartCoroutine(StartSpawningPhase());
            }
            return true;
        }

        return false;
    }

    private void UpdateWeaknessVisual(InstrumentType weakness)
    {
        if (bossWeaknessLight == null) return;
        bossWeaknessLight.enabled = true;

        // Color-code the spotlight over the terrified boss so the player knows which instrument to use:
        // Trumpet = Gold/Yellow, Drums = Red, Violin = Purple, Flute = Cyan/Blue
        switch (weakness)
        {
            case InstrumentType.Trumpet: bossWeaknessLight.color = Color.yellow; break;
            case InstrumentType.Drums:   bossWeaknessLight.color = Color.red; break;
            case InstrumentType.Violin:  bossWeaknessLight.color = new Color(0.7f, 0.2f, 1f); break;
            case InstrumentType.Flute:   bossWeaknessLight.color = Color.cyan; break;
        }
    }

    private void CleanupDeadMonstersInList()
    {
        activeWaveMonsters.RemoveAll(monster => monster == null);
    }

    private void Die()
    {
        StopAllCoroutines();
        currentState = BossState.Dead;
        SetArenaLights(true);

        if (battleBgmSource != null) battleBgmSource.Stop();
        if (bossWeaknessLight != null) bossWeaknessLight.enabled = false;

        animator.SetBool(AnimIsTerrified, false);
        animator.SetTrigger(AnimDie);

        if (GetComponent<Collider>() != null) GetComponent<Collider>().enabled = false;
        this.enabled = false;
    }
}