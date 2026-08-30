using System.Collections;
using Crease.Events;
using TMPro;
using UnityEngine;

public class InkTextManager : MonoBehaviour
{
    [SerializeField] private TMP_Text inkTextMesh;

    [SerializeField, Min(0.01f)] private float writeSpeed = 0.05f;
    [SerializeField, Min(0f)] private float lingerTime = 2f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 1f;

    private Coroutine currentRoutine;

    public void PlayText(string text)
    {
        if (inkTextMesh == null)
        {
            Debug.LogWarning("InkTextManager is missing a TMP_Text reference.");
            return;
        }

        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            GameEvents.OnLetterWritingStopped?.Invoke();
        }

        currentRoutine = StartCoroutine(WriteAndFadeRoutine(text));
    }

    private IEnumerator WriteAndFadeRoutine(string newText)
    {
        GameEvents.OnLetterWritingStarted?.Invoke();
        inkTextMesh.gameObject.SetActive(true);
        inkTextMesh.enabled = true;

        inkTextMesh.text = newText;
        inkTextMesh.alpha = 1f;
        inkTextMesh.maxVisibleCharacters = 0;

        inkTextMesh.ForceMeshUpdate();
        int totalCharacters = inkTextMesh.textInfo.characterCount;

        for (int i = 0; i <= totalCharacters; i++)
        {
            inkTextMesh.maxVisibleCharacters = i;
            yield return new WaitForSeconds(writeSpeed);
        }

        GameEvents.OnLetterWritingStopped?.Invoke();

        yield return new WaitForSeconds(lingerTime);

        float elapsed = 0f;
        float startAlpha = inkTextMesh.alpha;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            inkTextMesh.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        inkTextMesh.alpha = 0f;
        currentRoutine = null;
    }
}