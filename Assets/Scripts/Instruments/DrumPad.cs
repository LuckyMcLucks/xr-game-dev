using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class DrumPad : MonoBehaviour
{
    public AudioClip hitSound;
    public string stickTag = "DrumStick";

    [Header("Feel")]
    public float minHitSpeed = 0.3f;
    public float maxHitSpeed = 4f;
    public float cooldown = 0.04f;
    public Vector3 downDirection = Vector3.down;

    [Header("Monster hearing")]
    public float soundHoldTime = 0.7f;

    [Header("Debug")]
    public bool enableDebugKey = true;
    public float debugHitInterval = 0.3f;

    AudioSource source;
    float lastHitTime = -10f;
    float nextDebugHit;
    public float hitCost = 8f;
    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
    }

    void Update()
    {
        
        // Debug: hold Ctrl+K to simulate drumming
        if (enableDebugKey && Keyboard.current != null)
        {
            var kb = Keyboard.current;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (ctrl && kb.kKey.isPressed && Time.time >= nextDebugHit)
            {
                nextDebugHit = Time.time + debugHitInterval;
                PlayHit(0.8f);
            }
        }

        // Keep telling monsters the drum is being played for a moment after each hit
        if (Time.time - lastHitTime < soundHoldTime)
        {
            foreach (var monster in MonsterAI.all)
                monster.HearInstrument(InstrumentType.Drums);
        }
    }

    bool PlayHit(float strength)
    {
        if (!InstrumentStamina.Available) return false;
        InstrumentStamina.Spend(hitCost);

        source.pitch = Random.Range(0.97f, 1.03f);
        if (hitSound != null)
            source.PlayOneShot(hitSound, Mathf.Lerp(0.2f, 1f, strength));
        lastHitTime = Time.time;
        return true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(stickTag)) return;
        if (Time.time - lastHitTime < cooldown) return;

        var stick = other.GetComponent<DrumStick>();
        if (stick == null) return;

        float speed = Vector3.Dot(stick.Velocity, downDirection.normalized);
        if (speed < minHitSpeed) return;

        float strength = Mathf.InverseLerp(minHitSpeed, maxHitSpeed, speed);
        if (PlayHit(strength))
        stick.Haptic(Mathf.Lerp(0.1f, 0.8f, strength));
    }
}