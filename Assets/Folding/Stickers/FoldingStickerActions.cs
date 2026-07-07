using Crease.Folding.Decals;
using Crease.Folding.PaperGraph;
using UnityEngine;

namespace Crease.Folding.Stickers
{
    /// <summary>
    /// UI button wrappers for sticker-phase and decal actions.
    /// </summary>
    public class FoldingStickerActions : MonoBehaviour
    {
        [SerializeField] private FoldInstructionRunner _foldInstructionRunner;
        [SerializeField] private PaperDecalManager _decalManager;

        public void InstantResetPaper()
        {
            if (_foldInstructionRunner == null)
            {
                Debug.LogError("FoldingStickerActions: FoldInstructionRunner not assigned.");
                return;
            }
            _foldInstructionRunner.InstantResetPaper();
        }

        public void UnfoldPaper()
        {
            if (_foldInstructionRunner == null)
            {
                Debug.LogError("FoldingStickerActions: FoldInstructionRunner not assigned.");
                return;
            }
            _foldInstructionRunner.Unfold();
        }

        public void ClearStickers()
        {
            if (_decalManager == null)
            {
                Debug.LogError("FoldingStickerActions: PaperDecalManager not assigned.");
                return;
            }
            _decalManager.ClearUserStickers();
        }
    }
}
