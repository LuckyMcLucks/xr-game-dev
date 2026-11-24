using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Watches the backrooms fight. Once the player has banished the required number of monsters
/// (tracked through <see cref="GameSession.MonstersDefeated"/>, incremented by MonsterAI when a
/// monster is banished) the player is sent back to the concert hall for the second visit.
/// </summary>
[DisallowMultipleComponent]
public class MonsterClearWatcher : MonoBehaviour
{
    [Tooltip("How many monsters must be banished before the player is returned to the concert hall.")]
    public int monstersToDefeat = 3;

    [Tooltip("Beat after the final banish before the teleport, so the player sees it vanish.")]
    public float delayAfterClear = 2.5f;

    [Tooltip("Fades to black and loads the return scene. Assign the scene's GameFlowManager.")]
    public GameFlowManager flow;

    [Tooltip("Used only when no GameFlowManager is assigned.")]
    public string returnScene = "concerthall";

    bool triggered;

    void Update()
    {
        if (triggered) return;
        if (GameSession.MonstersDefeated < monstersToDefeat) return;

        triggered = true;
        StartCoroutine(ReturnToConcertHall());
    }

    IEnumerator ReturnToConcertHall()
    {
        yield return new WaitForSeconds(delayAfterClear);

        GameSession.BackroomsCleared = true;

        if (flow != null) flow.Advance();
        else SceneManager.LoadScene(returnScene);
    }
}
