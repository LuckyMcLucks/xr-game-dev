using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SimpleViolin : MonoBehaviour
{
    private AudioSource audioSource;
    private float targetVolume = 0f;

    [Tooltip("Smoothing speed for volume fading")]
    public float fadeSpeed = 8f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.volume = 0f;
        audioSource.loop = true;
    }

    void Update()
    {
        // Smoothly fade audio in/out to prevent harsh cutoffs
        audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime * fadeSpeed);

        // Pause when totally silent, resume when playing
        if (audioSource.volume < 0.01f && audioSource.isPlaying)
        {
            audioSource.Pause();
        }

        // Reset volume target every frame. Bow will keep it high if rubbing.
        targetVolume = 0f;
    }

    // Called by the Bow script whenever it's actively moving across this collider
    public void Rub()
    {
        if (!audioSource.isPlaying)
        {
            audioSource.UnPause();
            if (!audioSource.isPlaying) audioSource.Play();
        }

        targetVolume = 1f; // Full volume while rubbing
    }
}
