using UnityEngine;

/// Put this on the snare HEAD (a thin object/cylinder on top of the drum).
/// Needs: a Collider with Is Trigger ON, and an AudioSource (Play On Awake OFF).
[RequireComponent(typeof(AudioSource))]
public class DrumPad : MonoBehaviour
{
    public AudioClip hitSound;
    public string stickTag = "DrumStick";

    [Header("Feel")]
    public float minHitSpeed = 0.3f;     // ignore very slow touches (m/s)
    public float maxHitSpeed = 4f;       // speed that gives full volume
    public float cooldown = 0.04f;       // prevents double triggers (sec)
    public Vector3 downDirection = Vector3.down; // "into the drum" in world space

    AudioSource source;
    float lastHitTime;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f; // 3D sound
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(stickTag)) return;
        if (Time.time - lastHitTime < cooldown) return;

        var stick = other.GetComponent<DrumStick>();
        if (stick == null) return;

        // Only count hits coming from above, moving downward into the head
        float speed = Vector3.Dot(stick.Velocity, downDirection.normalized);
        if (speed < minHitSpeed) return;

        float strength = Mathf.InverseLerp(minHitSpeed, maxHitSpeed, speed);

        source.pitch = Random.Range(0.97f, 1.03f); // slight variation
        source.PlayOneShot(hitSound, Mathf.Lerp(0.2f, 1f, strength));
        stick.Haptic(Mathf.Lerp(0.1f, 0.8f, strength));

        lastHitTime = Time.time;
    }
}