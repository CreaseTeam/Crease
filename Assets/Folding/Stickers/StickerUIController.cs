using System.Collections.Generic;
using Crease.Folding.Decals;
using Crease.Folding.PaperGraph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Crease.Folding.Stickers
{
    public class StickerUIController : MonoBehaviour, IPointerClickHandler
    {
        [FormerlySerializedAs("stickerManager")]
        public PaperDecalManager DecalManager;

        [FormerlySerializedAs("library")]
        public StickerLibrary Library;

        [FormerlySerializedAs("dropdown")]
        public TMP_Dropdown Dropdown;

        [FormerlySerializedAs("previewImage")]
        public Image PreviewImage;

        [FormerlySerializedAs("foldInstructionRunner")]
        public FoldInstructionRunner FoldInstructionRunner;

        [Tooltip("Optional. Created at runtime if unset. Follows the cursor while a sticker is held off-paper.")]
        public Image CursorFollowerImage;

        [Tooltip("Screen size of the cursor follower image.")]
        public float CursorFollowerSize = 64f;

        private int _selectedIndex;
        private bool _isHoldingSticker;
        private StickerEntry _heldEntry;
        private Sprite _previewSprite;
        private Sprite _cursorFollowerSprite;
        private Canvas _rootCanvas;
        private bool _createdCursorFollower;
        private Mouse _mouse;

        public bool IsHoldingSticker => _isHoldingSticker;

        private void Start()
        {
            PopulateDropdown();
            if (Dropdown != null)
                Dropdown.onValueChanged.AddListener(OnDropdownChanged);
            RefreshPreview();
        }

        private void OnEnable()
        {
            PopulateDropdown();
            RefreshPreview();
        }

        private void OnDisable()
        {
            ClearHeldSticker();
        }

        private void Update()
        {
            if (_mouse == null)
                _mouse = Mouse.current;
            if (_mouse == null)
                return;

            if (!_isHoldingSticker || DecalManager == null || _heldEntry?.Texture == null)
                return;

            Vector2 screenPosition = _mouse.position.ReadValue();
            UpdateHoldVisuals(screenPosition);

            if (!_mouse.leftButton.wasPressedThisFrame || IsPointerOverUi(_mouse))
                return;

            DecalSurfaceQuery.SurfaceHit hit = DecalManager.RaycastScreen(screenPosition);
            if (!hit.Hit)
                return;

            DecalManager.PlaceDecal(_heldEntry.Texture, hit, _heldEntry.DefaultScale);
            ClearHeldSticker();
        }

        public void PopulateDropdown()
        {
            if (Dropdown == null || Library == null) return;

            Dropdown.ClearOptions();
            var options = new List<string>();
            foreach (StickerEntry entry in Library.Stickers)
                options.Add(string.IsNullOrEmpty(entry.DisplayName) ? "Sticker" : entry.DisplayName);
            if (options.Count == 0)
                options.Add("No stickers");
            Dropdown.AddOptions(options);
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, Library.Stickers.Count - 1));
            Dropdown.SetValueWithoutNotify(_selectedIndex);
        }

        private void OnDropdownChanged(int index)
        {
            _selectedIndex = index;
            RefreshPreview();
            if (_isHoldingSticker)
            {
                StickerEntry entry = GetSelectedEntry();
                if (entry?.Texture == null)
                    ClearHeldSticker();
                else
                    _heldEntry = entry;
            }
        }

        private void RefreshPreview()
        {
            StickerEntry entry = GetSelectedEntry();
            if (PreviewImage == null) return;

            if (_previewSprite != null)
            {
                Destroy(_previewSprite);
                _previewSprite = null;
            }

            if (entry?.Texture != null)
            {
                _previewSprite = Sprite.Create(
                    entry.Texture,
                    new Rect(0, 0, entry.Texture.width, entry.Texture.height),
                    new Vector2(0.5f, 0.5f));
                PreviewImage.sprite = _previewSprite;
                PreviewImage.enabled = true;
            }
            else
            {
                PreviewImage.sprite = null;
                PreviewImage.enabled = false;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsStickerPhaseActive() || DecalManager == null)
                return;

            StickerEntry entry = GetSelectedEntry();
            if (entry?.Texture == null)
                return;

            _heldEntry = entry;
            _isHoldingSticker = true;
            UpdateHoldVisuals(eventData.position);
        }

        /// <summary>
        /// Releases the sticker currently held by the cursor. Wire to a cancel button via the Inspector.
        /// </summary>
        public void ClearHeldSticker()
        {
            _isHoldingSticker = false;
            _heldEntry = null;
            DecalManager?.HideGhost();
            HideCursorFollower();
        }

        public void OnResetStickersClicked()
        {
            ClearHeldSticker();
            DecalManager?.ClearDecals();
        }

        private void UpdateHoldVisuals(Vector2 screenPosition)
        {
            DecalSurfaceQuery.SurfaceHit hit = DecalManager.RaycastScreen(screenPosition);
            if (hit.Hit)
            {
                DecalManager.ShowGhost(_heldEntry.Texture, hit, _heldEntry.DefaultScale);
                HideCursorFollower();
            }
            else
            {
                DecalManager.HideGhost();
                ShowCursorFollower(screenPosition);
            }
        }

        private void ShowCursorFollower(Vector2 screenPosition)
        {
            if (_heldEntry?.Texture == null)
                return;

            EnsureCursorFollower();
            if (CursorFollowerImage == null)
                return;

            if (_cursorFollowerSprite == null || _cursorFollowerSprite.texture != _heldEntry.Texture)
            {
                if (_cursorFollowerSprite != null)
                    Destroy(_cursorFollowerSprite);
                _cursorFollowerSprite = Sprite.Create(
                    _heldEntry.Texture,
                    new Rect(0, 0, _heldEntry.Texture.width, _heldEntry.Texture.height),
                    new Vector2(0.5f, 0.5f));
            }

            CursorFollowerImage.sprite = _cursorFollowerSprite;
            CursorFollowerImage.enabled = true;
            CursorFollowerImage.rectTransform.SetAsLastSibling();
            CursorFollowerImage.rectTransform.position = screenPosition;
        }

        private void HideCursorFollower()
        {
            if (CursorFollowerImage != null)
                CursorFollowerImage.enabled = false;
        }

        private void EnsureCursorFollower()
        {
            if (CursorFollowerImage != null)
                return;

            _rootCanvas ??= GetComponentInParent<Canvas>();
            if (_rootCanvas == null)
                return;

            GameObject followerObject = new GameObject("StickerCursorFollower");
            followerObject.transform.SetParent(_rootCanvas.transform, false);

            RectTransform rectTransform = followerObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(CursorFollowerSize, CursorFollowerSize);

            CursorFollowerImage = followerObject.AddComponent<Image>();
            CursorFollowerImage.raycastTarget = false;
            CursorFollowerImage.enabled = false;
            _createdCursorFollower = true;
        }

        private static bool IsPointerOverUi(Mouse mouse)
        {
            return EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject(mouse.deviceId);
        }

        private StickerEntry GetSelectedEntry()
        {
            if (Library == null || Library.Stickers.Count == 0) return null;
            int idx = Dropdown != null ? Dropdown.value : _selectedIndex;
            idx = Mathf.Clamp(idx, 0, Library.Stickers.Count - 1);
            return Library.Stickers[idx];
        }

        private bool IsStickerPhaseActive()
        {
            return FoldInstructionRunner != null && FoldInstructionRunner.IsInStickerPhase;
        }

        private void OnDestroy()
        {
            if (_previewSprite != null)
                Destroy(_previewSprite);
            if (_cursorFollowerSprite != null)
                Destroy(_cursorFollowerSprite);
            if (_createdCursorFollower && CursorFollowerImage != null)
                Destroy(CursorFollowerImage.gameObject);
        }
    }
}
