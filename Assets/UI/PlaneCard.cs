using System.Collections.Generic;
using Crease.Flying.Player.Loadouts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crease.UI
{
    /// <summary>
    /// Plane selection card. Hover populates the shared detail panel; click selects the loadout and starts folding.
    /// </summary>
    public class PlaneCard : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField]
        private Graphic _pointerTarget;

        [Header("Loadout")]
        [SerializeField]
        private PlaneLoadout _loadout;

        [Header("Card Preview")]
        [SerializeField]
        private Image _cardImage;
        [SerializeField]
        private TextMeshProUGUI _cardLabel;
        [SerializeField]
        private Image _highlightImage;

        public PlaneLoadout Loadout => _loadout;

        private void Awake()
        {
            SetHighlighted(false);

            if (_pointerTarget == null)
                _pointerTarget = GetComponent<Graphic>() ?? GetComponentInChildren<Graphic>(true);

            if (_pointerTarget == null)
            {
                Debug.LogWarning("PlaneCard: No pointer target assigned or found.");
                return;
            }

            EnsureTrigger(_pointerTarget.gameObject, EventTriggerType.PointerEnter, _ => ShowDetails());
            EnsureTrigger(_pointerTarget.gameObject, EventTriggerType.PointerClick, _ => SelectLoadout());
        }

        private void Start()
        {
            RefreshCardPreview();
        }

        public void SetLoadout(PlaneLoadout loadout)
        {
            _loadout = loadout;
            RefreshCardPreview();
        }

        public void ShowDetails()
        {
            if (_loadout == null || HUDCanvas.Instance == null)
                return;

            HUDCanvas.Instance.ShowLoadoutDetails(_loadout);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_highlightImage != null)
                _highlightImage.enabled = highlighted;
        }

        public void SelectLoadout()
        {
            if (HUDCanvas.Instance == null)
            {
                Debug.LogWarning("PlaneCard: HUDCanvas instance is not available.");
                return;
            }

            if (_loadout == null)
            {
                Debug.LogWarning("PlaneCard: No loadout assigned.");
                return;
            }

            HUDCanvas.Instance.SelectLoadoutFromCard(_loadout);
        }

        private void RefreshCardPreview()
        {
            if (_loadout == null)
                return;

            if (_cardImage != null)
            {
                _cardImage.sprite = _loadout.Image;
                _cardImage.enabled = _loadout.Image != null;
            }

            if (_cardLabel != null)
                _cardLabel.text = _loadout.DisplayName;
        }

        private static void EnsureTrigger(GameObject target, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = target.AddComponent<EventTrigger>();

            if (trigger.triggers == null)
                trigger.triggers = new List<EventTrigger.Entry>();

            foreach (EventTrigger.Entry entry in trigger.triggers)
            {
                if (entry.eventID != type)
                    continue;

                entry.callback.AddListener(callback);
                return;
            }

            var newEntry = new EventTrigger.Entry
            {
                eventID = type,
                callback = new EventTrigger.TriggerEvent()
            };
            newEntry.callback.AddListener(callback);
            trigger.triggers.Add(newEntry);
        }
    }
}
