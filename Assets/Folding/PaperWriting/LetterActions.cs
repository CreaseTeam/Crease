using Crease.Folding.PaperGraph;
using UnityEngine;

namespace Crease.Folding.PaperWriting
{
    /// <summary>
    /// Simple wrapper with instance methods that delegate to LetterController.Instance
    /// and FoldingManager.Instance. Attach to any GameObject and drag these functions
    /// onto Button OnClick events, etc.
    /// </summary>
    public class LetterActions : MonoBehaviour
    {
        [SerializeField]
        string _sectionName;

        public void WriteSection()
        {
            WriteSection(_sectionName);
        }

        public void WriteSection(string sectionName)
        {
            if (LetterController.Instance == null)
            {
                Debug.LogError($"{nameof(LetterActions)}: No {nameof(LetterController)} instance.");
                return;
            }

            LetterController.Instance.WriteSection(sectionName);
        }

        public void WriteSectionAndWait()
        {
            WriteSectionAndWait(_sectionName);
        }

        public void WriteSectionAndWait(string sectionName)
        {
            if (LetterController.Instance == null)
            {
                Debug.LogError($"{nameof(LetterActions)}: No {nameof(LetterController)} instance.");
                return;
            }

            StartCoroutine(LetterController.Instance.WriteSectionAndWait(sectionName));
        }

        public void QueueLetterSection()
        {
            QueueLetterSection(_sectionName);
        }

        public void QueueLetterSection(string sectionName)
        {
            if (FoldingManager.Instance == null)
            {
                Debug.LogError($"{nameof(LetterActions)}: No {nameof(FoldingManager)} instance.");
                return;
            }

            FoldingManager.Instance.QueueLetterSection(sectionName);
        }
    }
}
