using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class SimpleViolin : MonoBehaviour
{
    private AudioSource audioSource;
    private float targetVolume = 0f;

    public float fadeSpeed = 8f;

    [Header("Debug")]
    public bool enableDebugKey = true;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.volume = 0f;
        audioSource.loop = true;
    }

    void Update()
    {
        
        // Debug: hold Ctrl+L to simulate bowing (must run before the fade below)
        if (enableDebugKey && Keyboard.current != null)
        {
            var kb = Keyboard.current;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (ctrl && kb.lKey.isPressed) Rub();
        }

        audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime * fadeSpeed);

        if (audioSource.volume < 0.01f && audioSource.isPlaying)
            audioSource.Pause();

        targetVolume = 0f;
    }

    public void Rub()
    {
        if (!InstrumentStamina.Available) return;
        InstrumentStamina.ReportPlaying();
        if (!audioSource.isPlaying)
        {
            audioSource.UnPause();
            if (!audioSource.isPlaying) audioSource.Play();
        }

        targetVolume = 1f;

        foreach (var monster in MonsterAI.all)
            monster.HearInstrument(InstrumentType.Violin);
    }
}