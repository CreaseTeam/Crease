using Crease.Folding.PaperSurface.Decals;
using Crease.Folding.Paper;
using UnityEngine;

namespace Crease.Folding.PaperSurface.Stickers
{
    /// <summary>
    /// UI button wrappers for sticker-phase and decal actions.
    /// </summary>
    public class FoldingStickerActions : MonoBehaviour
    {
        [SerializeField] private FoldInstructionRunner _foldInstructionRunner;

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
            if (DecalController.Instance == null)
            {
                Debug.LogError("FoldingStickerActions: DecalController is not available.");
                return;
            }
            DecalController.Instance.ClearUserStickers();
        }
    }
}
