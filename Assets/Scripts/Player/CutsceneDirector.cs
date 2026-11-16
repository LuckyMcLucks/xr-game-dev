using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Plays a Timeline and freezes the player for its duration.
/// Attach to a persistent object (e.g. the XR Origin) and assign in the Inspector.
public class CutsceneDirector : MonoBehaviour
{
    [Tooltip("The PlayableDirector holding the cutscene Timeline.")]
    public PlayableDirector director;

    [Tooltip("Turn / move providers + teleport providers to disable while the cutscene plays.")]
    public Behaviour[] disableWhilePlaying;

    [Tooltip("Hands/ray interactors to disable so the player cannot grab mid-cutscene.")]
    public XRBaseInteractor[] disableInteractors;

    [Tooltip("Optional black fade canvas (CanvasGroup).")]
    public CanvasGroup fadeGroup;
    public float fadeDuration = 0.6f;

    public event Action Finished;

    /// <summary>Play with a fade-out, freeze, play, fade-in, unfreeze.</summary>
    public IEnumerator PlayWithFade()
    {
        SetPlayerActive(false);
        yield return Fade(1f);          // to black
        director.time = 0;
        director.Play();
        while (director.state == PlayState.Playing) yield return null;
        yield return Fade(0f);          // back to the world
        SetPlayerActive(true);
        Finished?.Invoke();
    }

    /// <summary>Play without fading — for an in-world scare the player must see.</summary>
    public void PlayInWorld()
    {
        SetPlayerActive(false);
        director.stopped += OnStopped;
        director.time = 0;
        director.Play();
    }

    void OnStopped(PlayableDirector d)
    {
        d.stopped -= OnStopped;
        SetPlayerActive(true);
        Finished?.Invoke();
    }

    void SetPlayerActive(bool on)
    {
        foreach (var b in disableWhilePlaying) if (b) b.enabled = on;
        foreach (var i in disableInteractors) if (i) i.enabled = on;
    }

    IEnumerator Fade(float target)
    {
        if (fadeGroup == null) yield break;
        float start = fadeGroup.alpha, t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        fadeGroup.alpha = target;
    }
}