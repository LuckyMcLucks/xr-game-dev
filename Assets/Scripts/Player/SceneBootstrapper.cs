using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single scene entry point, placed in every gameplay scene. It reads the cross-scene
/// <see cref="GameSession"/> flags and configures the player accordingly:
///
///   * concert hall, first visit  -> attack disabled, intro cutscene runs (fully frozen),
///   * backrooms                  -> attack enabled, free walking/running/inventory,
///   * concert hall, second visit -> attack enabled, free walking, no intro.
/// </summary>
[DisallowMultipleComponent]
public class SceneBootstrapper : MonoBehaviour
{
    [Header("Concert hall")]
    public ConcertHallIntro intro;
    public PlayerRigLock rigLock;

    [Header("Backrooms")]
    public MonsterClearWatcher watcher;

    void Start()
    {
        string scene = SceneManager.GetActiveScene().name;

        if (scene == "backrooms")
        {
            PrepareCombat();
            if (watcher != null) watcher.enabled = true;
            return;
        }

        // Concert hall: intro on the first visit, straight into the boss fight on the second.
        if (GameSession.ContractSigned)
        {
            PrepareCombat();
            if (intro != null)
            {
                intro.HideAll();
                intro.enabled = false;
                StartBossFight();
            }
        }
        else
        {
            InstrumentStamina.AttackEnabled = false;
            if (intro != null) intro.Begin();
        }
    }

    void PrepareCombat()
    {
        InstrumentStamina.AttackEnabled = true;
        if (rigLock != null) rigLock.Unlock();
    }

    /// <summary>Second visit: the conductor is waiting in the middle of the hall and starts summoning waves.</summary>
    void StartBossFight()
    {
        if (intro == null || intro.bossObject == null) return;

        intro.bossObject.SetActive(true);

        var boss = intro.bossObject.GetComponent<BossAI>();
        if (boss != null) boss.ForceStartSpawningPhase();
    }
}
