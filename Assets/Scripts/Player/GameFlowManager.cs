using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Fades to black and loads the next act. Put the scene names in build order
/// (File > Build Profiles > Scene List) and call Advance() from a trigger, a
/// timeline Signal, or a monster jumpscare.
public class GameFlowManager : MonoBehaviour
{
    public CanvasGroup fadeGroup;
    public float fadeDuration = 0.7f;
    public string nextScene;

    public void Advance() => StartCoroutine(LoadNext());

    IEnumerator LoadNext()
    {
        yield return Fade(1f);
        var op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return Fade(0f);
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