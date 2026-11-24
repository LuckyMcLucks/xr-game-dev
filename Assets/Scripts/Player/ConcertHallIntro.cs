using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the first visit to the concert hall:
///   1. music starts and the player is rooted in place (head tilting is always allowed),
///   2. the "Welcome" word expands in front of them and slowly fades out (within ~5 s),
///   3. a rules board appears; once the player signs it, everything flashes white,
///   4. the boss conductor appears and the boss-reveal Timeline runs for ~10 s,
///   5. the game teleports to the backrooms.
/// </summary>
[DisallowMultipleComponent]
public class ConcertHallIntro : MonoBehaviour
{
    [Header("References")]
    public PlayerRigLock rigLock;
    public GameFlowManager flow;

    [Tooltip("Optional. Timeline that carries the background music. Restarted if it is not already playing.")]
    public PlayableDirector musicDirector;

    [Header("Welcome word")]
    public GameObject welcomeRoot;
    public CanvasGroup welcomeGroup;
    public RectTransform welcomePanel;

    [Tooltip("Seconds the word takes to grow to full size.")]
    public float welcomeGrowTime = 1.4f;
    [Tooltip("Fraction of the final size the word starts at while it fades in.")]
    [Range(0.1f, 0.95f)] public float welcomeStartScale = 0.55f;
    [Tooltip("Seconds the full word is held before fading.")]
    public float welcomeHoldTime = 0.8f;
    [Tooltip("Seconds the word takes to fade out.")]
    public float welcomeFadeTime = 2.4f;

    [Header("Rules + signature")]
    public SignaturePanel signaturePanel;

    [Header("White flash")]
    public WhiteFlash flash;

    [Header("Boss reveal")]
    [Tooltip("The boss conductor object that is switched on for the reveal.")]
    public GameObject bossObject;
    [Tooltip("10-second boss-reveal Timeline. If left empty the fallback delay is used.")]
    public PlayableDirector bossReveal;
    public float bossRevealFallbackSeconds = 10f;

    [Header("Next scene")]
    public string nextScene = "backrooms";

    Vector3 welcomeTargetScale = Vector3.one;

    void Awake()
    {
        if (welcomePanel != null) welcomeTargetScale = welcomePanel.localScale;
        HideAll();
    }

    /// <summary>Switch everything this intro owns off (used for the second-visit combat flow).</summary>
    public void HideAll()
    {
        if (welcomeRoot != null) welcomeRoot.SetActive(false);
        if (signaturePanel != null && signaturePanel.panelRoot != null) signaturePanel.panelRoot.SetActive(false);
        if (bossObject != null) bossObject.SetActive(false);
    }

    public void Begin()
    {
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // Music starts immediately; the player is rooted but free to look around for the
        // whole cutscene, so only the position is locked (the head is never frozen).
        if (musicDirector != null && musicDirector.state != PlayState.Playing)
        {
            musicDirector.time = 0;
            musicDirector.Play();
        }

        if (rigLock != null) rigLock.LockPositionOnly();

        // Welcome word: expand + fade in, hold, fade out — comfortably inside 5 seconds.
        // The grow starts from a small non-zero scale instead of zero: scaling up from zero
        // makes TextMeshPro rewrite its SDF scale on nearly every frame, which is what made
        // the word look jittery while it appeared.
        if (welcomeRoot != null && welcomeGroup != null && welcomePanel != null)
        {
            welcomeRoot.SetActive(true);
            welcomeGroup.alpha = 0f;
            welcomePanel.localScale = welcomeTargetScale * welcomeStartScale;

            yield return GrowRoutine(welcomePanel, welcomeTargetScale, welcomeGroup, welcomeGrowTime);
            if (welcomeHoldTime > 0f) yield return new WaitForSeconds(welcomeHoldTime);
            yield return AlphaRoutine(welcomeGroup, 0f, welcomeFadeTime);

            welcomeRoot.SetActive(false);
        }

        // Rules board, then wait for the player's signature.
        if (signaturePanel != null)
        {
            if (signaturePanel.panelRoot != null) signaturePanel.panelRoot.SetActive(true);
            signaturePanel.ResetInk();
            while (!signaturePanel.Signed) yield return null;
        }

        // Everything shines bright white.
        if (flash != null) yield return flash.PlayRoutine();

        // Boss conductor appears and the reveal Timeline runs.
        if (bossObject != null) bossObject.SetActive(true);

        if (bossReveal != null)
        {
            bossReveal.time = 0;
            bossReveal.Play();
            while (bossReveal.state == PlayState.Playing) yield return null;
        }
        else if (bossRevealFallbackSeconds > 0f)
        {
            yield return new WaitForSeconds(bossRevealFallbackSeconds);
        }

        // On to the backrooms.
        GameSession.ContractSigned = true;
        if (flow != null) flow.Advance();
        else SceneManager.LoadScene(nextScene);
    }

    /// <summary>Grows the panel to <paramref name="to"/> while fading its CanvasGroup in.</summary>
    static IEnumerator GrowRoutine(RectTransform target, Vector3 to, CanvasGroup group, float duration)
    {
        if (target == null) yield break;
        Vector3 from = target.localScale;
        float alphaFrom = group != null ? group.alpha : 1f;
        if (duration <= 0f)
        {
            target.localScale = to;
            if (group != null) group.alpha = 1f;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            target.localScale = Vector3.LerpUnclamped(from, to, k);
            if (group != null) group.alpha = Mathf.Lerp(alphaFrom, 1f, k);
            yield return null;
        }
        target.localScale = to;
        if (group != null) group.alpha = 1f;
    }

    static IEnumerator AlphaRoutine(CanvasGroup group, float to, float duration)
    {
        if (group == null) yield break;
        float from = group.alpha;
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }
}
