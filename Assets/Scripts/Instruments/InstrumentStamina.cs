using UnityEngine;
using UnityEngine.UI;

public class InstrumentStamina : MonoBehaviour
{
    public static InstrumentStamina Instance;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float drainPerSecond = 20f;       // while an instrument is played continuously
    public float regenPerSecond = 15f;
    public float regenDelay = 1f;            // pause after playing before regen starts
    public float recoverThreshold = 25f;     // once empty, must refill to this before playing again

    [Header("UI (optional)")]
    public Image fillImage;                  // Image Type = Filled
    public Color normalColor = Color.green;
    public Color exhaustedColor = Color.red;

    public float Current { get; private set; }
    public bool Exhausted { get; private set; }

    bool playingThisFrame;
    float lastUseTime = -10f;

    void Awake()
    {
        Instance = this;
        Current = maxStamina;
    }

    // True if the player is allowed to play right now
    public static bool Available => Instance == null || (!Instance.Exhausted && Instance.Current > 0f);

    // Call every frame while a continuous instrument (trumpet, violin) is playing
    public static void ReportPlaying()
    {
        if (Instance != null) Instance.playingThisFrame = true;
    }

    // Call once for a one-off action (a drum hit)
    public static void Spend(float amount)
    {
        if (Instance == null) return;
        Instance.Current = Mathf.Max(0f, Instance.Current - amount);
        Instance.lastUseTime = Time.time;
    }

    // Runs after every instrument's Update, so the flag is always up to date
    void LateUpdate()
    {
        if (playingThisFrame)
        {
            Current -= drainPerSecond * Time.deltaTime;
            lastUseTime = Time.time;
        }
        else if (Time.time - lastUseTime >= regenDelay)
        {
            Current += regenPerSecond * Time.deltaTime;
        }

        Current = Mathf.Clamp(Current, 0f, maxStamina);

        if (Current <= 0f) Exhausted = true;
        else if (Exhausted && Current >= recoverThreshold) Exhausted = false;

        playingThisFrame = false;

        if (fillImage != null)
        {
            fillImage.fillAmount = Current / maxStamina;
            fillImage.color = Exhausted ? exhaustedColor : normalColor;
        }
    }
}