/// <summary>
/// Cross-scene progress flags. Static, so they survive scene loads for the whole play session.
/// The concert hall reads them to decide whether it is showing the intro (first visit) or
/// dropping the player straight into the fight (second visit, after the backrooms are cleared).
/// </summary>
public static class GameSession
{
    /// <summary>True once the player has scribbled their signature on the orchestra rules.</summary>
    public static bool ContractSigned;

    /// <summary>True once every monster in the backrooms has been banished.</summary>
    public static bool BackroomsCleared;

    /// <summary>How many monsters the player has banished this session.</summary>
    public static int MonstersDefeated;

    /// <summary>Reset for a fresh run (useful when testing without restarting the editor).</summary>
    public static void Reset()
    {
        ContractSigned = false;
        BackroomsCleared = false;
        MonstersDefeated = 0;
    }

    /// <summary>
    /// Clears the flags every time the game starts, so a new run always plays the concert-hall
    /// intro. SubsystemRegistration also runs when Unity's domain reload is disabled, which is
    /// the case in this project - without this the flags leak from the previous play session and
    /// the concert hall skips straight to the boss fight.
    /// </summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Reset();
    }
}
