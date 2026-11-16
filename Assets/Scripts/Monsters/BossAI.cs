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
    [Header("Boss Stats")]
    public float maxHealth = 100f;
    public float currentHealth;
    public BossState currentState;

    [Header("Spawn Settings")]
    [Tooltip("List of monster prefabs to spawn per wave.")]
    public List<GameObject> waveMonsterPrefabs;
    [Tooltip("Transforms where monsters will spawn around the stage.")]
    public Transform[] spawnPoints;
    
    [Header("State Timers")]
    [Tooltip("How long the boss stays terrified/vulnerable when all wave monsters die.")]
    public float vulnerableDuration = 8f;

    [Header("References")]
    public Transform playerTransform;
    public GameObject shieldVisual;

    [Header("--- MANUAL ANIMATION TESTING (PLAY MODE ONLY) ---")]
    [Tooltip("Click the checkbox to manually trigger the Spell Cast animation in Play Mode.")]
    public bool testCastSpellTrigger;
    
    [Tooltip("Toggle on/off to force the Terrified/Vulnerable animation state.")]
    public bool testTerrifiedBool;
    private bool previousTerrifiedValue = false;

    [Tooltip("Click the checkbox to manually trigger the Hit animation.")]
    public bool testTakeHitTrigger;

    [Tooltip("Click the checkbox to manually trigger the Death animation.")]
    public bool testDeathTrigger;

    private Animator animator;
    private List<GameObject> activeWaveMonsters = new List<GameObject>();

    // Animator Parameter Hashes
    private readonly int AnimIsTerrified = Animator.StringToHash("IsTerrified");
    private readonly int AnimCastSpell = Animator.StringToHash("CastSpell");
    private readonly int AnimHit = Animator.StringToHash("TakeHit");
    private readonly int AnimDie = Animator.StringToHash("Die");

    void Start()
    {
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;
        
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        // Handle Inspector Testing Triggers
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

        // Check wave completion
        if (currentState == BossState.Commanding)
        {
            CleanupDeadMonstersInList();
            if (activeWaveMonsters.Count == 0)
            {
                StartCoroutine(EnterVulnerableState());
            }
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

        // Only update the Animator parameter when the checkbox actually changes value
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

    // --- CONTEXT MENU BUTTONS (Right-click script title in Inspector) ---

    [ContextMenu("Test Animation: Spell Cast")]
    public void TriggerSpellCastAnimation()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animator.SetTrigger(AnimCastSpell);
        Debug.Log("[Boss Testing] Triggered Spell Cast Animation");
    }

    [ContextMenu("Test Animation: Toggle Terrified ON")]
    public void EnableTerrifiedAnimation()
    {
        if (animator == null) animator = GetComponent<Animator>();
        testTerrifiedBool = true;
        animator.SetBool(AnimIsTerrified, true);
        Debug.Log("[Boss Testing] Terrified Animation set to TRUE");
    }

    [ContextMenu("Test Animation: Toggle Terrified OFF")]
    public void DisableTerrifiedAnimation()
    {
        if (animator == null) animator = GetComponent<Animator>();
        testTerrifiedBool = false;
        animator.SetBool(AnimIsTerrified, false);
        Debug.Log("[Boss Testing] Terrified Animation set to FALSE");
    }

    [ContextMenu("Test Animation: Take Hit")]
    public void TriggerTakeHitAnimation()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animator.SetTrigger(AnimHit);
        Debug.Log("[Boss Testing] Triggered Take Hit Animation");
    }

    [ContextMenu("Test Animation: Death")]
    public void TriggerDeathAnimation()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animator.SetTrigger(AnimDie);
        Debug.Log("[Boss Testing] Triggered Death Animation");
    }

    // --- GAMEPLAY PHASES ---

    [ContextMenu("Start Wave Spawning Phase")]
    public void ForceStartSpawningPhase()
    {
        StopAllCoroutines();
        StartCoroutine(StartSpawningPhase());
    }

    private IEnumerator StartSpawningPhase()
    {
        currentState = BossState.SpawningWave;
        
        if (shieldVisual != null) shieldVisual.SetActive(true);

        testTerrifiedBool = false;
        animator.SetBool(AnimIsTerrified, false);
        animator.SetTrigger(AnimCastSpell);

        yield return new WaitForSeconds(1.5f);

        activeWaveMonsters.Clear();
        for (int i = 0; i < waveMonsterPrefabs.Count; i++)
        {
            if (spawnPoints.Length == 0) break;
            Transform spawnPoint = spawnPoints[i % spawnPoints.Length];
            GameObject monster = Instantiate(waveMonsterPrefabs[i], spawnPoint.position, spawnPoint.rotation);
            
            MonsterAI monsterAI = monster.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                monsterAI.playerTransform = playerTransform;
            }

            activeWaveMonsters.Add(monster);
        }

        currentState = BossState.Commanding;
    }

    private IEnumerator EnterVulnerableState()
    {
        currentState = BossState.VulnerableTerrified;
        
        if (shieldVisual != null) shieldVisual.SetActive(false);
        testTerrifiedBool = true;
        animator.SetBool(AnimIsTerrified, true);

        yield return new WaitForSeconds(vulnerableDuration);

        if (currentState == BossState.VulnerableTerrified)
        {
            StartCoroutine(StartSpawningPhase());
        }
    }

    public void TakeDamage(float amount)
    {
        if (currentState != BossState.VulnerableTerrified) return;

        currentHealth -= amount;
        animator.SetTrigger(AnimHit);

        if (currentHealth <= 0f)
        {
            Die();
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
        testTerrifiedBool = false;
        animator.SetBool(AnimIsTerrified, false);
        animator.SetTrigger(AnimDie);
        
        if (GetComponent<Collider>() != null) GetComponent<Collider>().enabled = false;
        this.enabled = false;
    }
}