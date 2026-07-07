using System.Collections;
using Crease.Handwritting;
using UnityEngine;

namespace Crease.Folding.PaperWriting
{
    public class LetterController : MonoBehaviour
    {
        public static LetterController Instance { get; private set; }

        [SerializeField]
        GameObject _textFieldsPrefab;

        GameObject _textFields;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void Start()
        {
            if (_textFieldsPrefab == null)
            {
                Debug.LogWarning($"{nameof(LetterController)} has no {nameof(_textFieldsPrefab)} assigned.");
                return;
            }

            _textFields = Instantiate(_textFieldsPrefab, transform);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void WriteSection(string name)
        {
            if (!TryActivateSection(name, out HandwrittenTextPlayer textPlayer))
                return;

            if (textPlayer != null)
                textPlayer.PlayWriteIn();
        }

        public IEnumerator WriteSectionAndWait(string name)
        {
            if (!TryActivateSection(name, out HandwrittenTextPlayer textPlayer))
                yield break;

            if (textPlayer == null)
                yield break;

            textPlayer.PlayWriteIn();
            yield return new WaitWhile(() => textPlayer != null && textPlayer.IsPlaying);
        }

        bool TryActivateSection(string name, out HandwrittenTextPlayer textPlayer)
        {
            textPlayer = null;

            if (_textFields == null)
            {
                Debug.LogWarning($"{nameof(LetterController)} has no instantiated text fields.");
                return false;
            }

            Transform sectionTransform = FindSectionTransform(name);
            if (sectionTransform == null)
            {
                Debug.LogWarning($"{nameof(LetterController)} could not find a text section named \"{name}\".");
                return false;
            }

            sectionTransform.gameObject.SetActive(true);
            textPlayer = sectionTransform.GetComponentInChildren<HandwrittenTextPlayer>(true);
            return true;
        }

        Transform FindSectionTransform(string name)
        {
            Transform textFieldsTransform = _textFields.transform;

            for (int i = 0; i < textFieldsTransform.childCount; i++)
            {
                Transform child = textFieldsTransform.GetChild(i);
                if (child.name == name)
                    return child;
            }

            return null;
        }
    }
}
