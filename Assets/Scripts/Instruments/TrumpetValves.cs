using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Put this on the trumpet (the object with the XRGrabInteractable).
/// While held, pulling the trigger plays the music and cycles the valves.
[RequireComponent(typeof(AudioSource))]
public class TrumpetValves : MonoBehaviour
{
    [System.Serializable]
    public class Valve
    {
        public Transform rod;                      // drag rod, rod (1), rod (2) here
        [HideInInspector] public Vector3 restPos;
        [HideInInspector] public bool pressed;
    }

    [Header("References")]
    public XRGrabInteractable grabInteractable;    // auto-found if left empty
    public Valve[] valves;

    [Header("Sound")]
    public AudioClip music;
    public bool loopMusic = true;

    [Header("Valve animation")]
    public float pressDistance = 0.01f;            // local units
    public Vector3 pressAxis = new Vector3(0f, -1f, 0f);   // local direction the valve moves when pressed
    public float speed = 15f;
    public float stepTime = 0.15f;                 // seconds per pattern step

    // Each number is a combination of valves: 1 = valve 1, 2 = valve 2, 4 = valve 3.
    // e.g. 3 = valves 1+2, 7 = all three, 0 = none. Edit to taste.
    public int[] pattern = { 1, 2, 4, 3, 5, 6, 7, 0 };

    AudioSource source;
    bool playing;
    int step;
    float stepTimer;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loopMusic;
        source.spatialBlend = 1f;

        if (grabInteractable == null)
            grabInteractable = GetComponentInParent<XRGrabInteractable>();
    }

    void Start()
    {
        foreach (var v in valves)
            if (v.rod != null) v.restPos = v.rod.localPosition;
    }

    void OnEnable()
    {
        if (grabInteractable == null) return;
        grabInteractable.activated.AddListener(OnTriggerPressed);
        grabInteractable.deactivated.AddListener(OnTriggerReleased);
        grabInteractable.selectExited.AddListener(OnDropped);
    }

    void OnDisable()
    {
        if (grabInteractable == null) return;
        grabInteractable.activated.RemoveListener(OnTriggerPressed);
        grabInteractable.deactivated.RemoveListener(OnTriggerReleased);
        grabInteractable.selectExited.RemoveListener(OnDropped);
    }

    void OnTriggerPressed(ActivateEventArgs args)   => StartPlaying();
    void OnTriggerReleased(DeactivateEventArgs args) => StopPlaying();
    void OnDropped(SelectExitEventArgs args)         => StopPlaying(); // safety: letting go stops it

    void StartPlaying()
    {
        if (playing) return;
        playing = true;
        step = 0;
        stepTimer = 0f;

        if (music != null)
        {
            source.clip = music;
            source.loop = loopMusic;
            source.Play();
        }
        ApplyPattern(pattern.Length > 0 ? pattern[0] : 0);
    }

    void StopPlaying()
    {
        if (!playing) return;
        playing = false;
        source.Stop();
        ApplyPattern(0); // release all valves
    }

    void ApplyPattern(int mask)
    {
        for (int i = 0; i < valves.Length; i++)
            valves[i].pressed = (mask & (1 << i)) != 0;
    }

    void Update()
    {
        // Advance the valve cycle while playing
        if (playing && pattern.Length > 0)
        {
            stepTimer += Time.deltaTime;
            if (stepTimer >= stepTime)
            {
                stepTimer -= stepTime;
                step = (step + 1) % pattern.Length;
                ApplyPattern(pattern[step]);
            }

            // Non-looping clip finished: stop automatically
            if (!loopMusic && !source.isPlaying) StopPlaying();
        }

        // Smoothly move the rods
        foreach (var v in valves)
        {
            if (v.rod == null) continue;
            Vector3 target = v.restPos + pressAxis.normalized * (v.pressed ? pressDistance : 0f);
            v.rod.localPosition = Vector3.Lerp(v.rod.localPosition, target, Time.deltaTime * speed);
        }
    }
}