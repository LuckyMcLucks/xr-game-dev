using System.Collections;
using UnityEngine;

/// <summary>
/// Full-screen white flash used when the player signs the orchestra contract.
///
/// Put this on an always-active parent and point <see cref="group"/> at a CanvasGroup on a
/// child world-space canvas (parented to the headset) that holds a white Image. The child is
/// kept active and simply fades its alpha, so the coroutine running on this component is
/// never cut short by deactivating its own GameObject.
/// </summary>
[DisallowMultipleComponent]
public class WhiteFlash : MonoBehaviour
{
    [Tooltip("CanvasGroup that drives the flash alpha. Leave the owning object active.")]
    public CanvasGroup group;

    public float fadeIn = 0.25f;
    public float hold = 0.7f;
    public float fadeOut = 1.2f;

    public bool IsPlaying { get; private set; }

    void Awake()
    {
        if (group == null) group = GetComponentInChildren<CanvasGroup>(true);
        if (group != null) group.alpha = 0f;
    }

    /// <summary>Fire-and-forget version.</summary>
    public void Play()
    {
        StopAllCoroutines();
        StartCoroutine(PlayRoutine());
    }

    /// <summary>Yield on this from a sequence that needs to wait for the flash to finish.</summary>
    public IEnumerator PlayRoutine()
    {
        if (group == null) yield break;

        IsPlaying = true;
        group.alpha = 0f;
        yield return Fade(1f, fadeIn);
        if (hold > 0f) yield return new WaitForSeconds(hold);
        yield return Fade(0f, fadeOut);
        IsPlaying = false;
    }

    IEnumerator Fade(float target, float duration)
    {
        float start = group.alpha;
        if (duration <= 0f || Mathf.Approximately(start, target))
        {
            group.alpha = target;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }
}
