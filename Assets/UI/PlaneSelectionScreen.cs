using Crease.Flying.Player.Loadouts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crease.UI
{
    /// <summary>
    /// Shared detail panel for the plane selection screen.
    /// </summary>
    public class PlaneSelectionScreen : MonoBehaviour
    {
        [Header("Detail Panel")]
        [SerializeField]
        private TextMeshProUGUI _displayNameText;
        [SerializeField]
        private Image _image;
        [SerializeField]
        private TextMeshProUGUI _descriptionText;
        [SerializeField]
        private IconBarDisplay _speedBar;
        [SerializeField]
        private IconBarDisplay _controlBar;
        [SerializeField]
        private IconBarDisplay _durabilityBar;
        [SerializeField]
        private IconBarDisplay _windResistanceBar;
        [SerializeField]
        private TextMeshProUGUI _abilityDescriptionText;

        private PlaneLoadout _displayedLoadout;

        public PlaneLoadout DisplayedLoadout => _displayedLoadout;

        public void Show() => gameObject.SetActive(true);

        public void Hide()
        {
            _displayedLoadout = null;
            RefreshCardHighlights();
            gameObject.SetActive(false);
        }

        public void ShowDetails(PlaneLoadout loadout)
        {
            if (loadout == null)
                return;

            if (_displayedLoadout == loadout)
                return;

            _displayedLoadout = loadout;

            if (_displayNameText != null)
                _displayNameText.text = loadout.DisplayName;

            if (_image != null)
            {
                _image.sprite = loadout.Image;
                _image.enabled = loadout.Image != null;
            }

            if (_descriptionText != null)
                _descriptionText.text = loadout.Description;

            if (_speedBar != null)
                _speedBar.SetValue(loadout.Speed);

            if (_controlBar != null)
                _controlBar.SetValue(loadout.Control);

            if (_durabilityBar != null)
                _durabilityBar.SetValue(loadout.Durability);

            if (_windResistanceBar != null)
                _windResistanceBar.SetValue(loadout.WindResistance);

            if (_abilityDescriptionText != null)
                _abilityDescriptionText.text = loadout.AbilityDescription;

            RefreshCardHighlights();
        }

        private void RefreshCardHighlights()
        {
            PlaneCard[] cards = GetComponentsInChildren<PlaneCard>(true);
            foreach (PlaneCard card in cards)
                card.SetHighlighted(card.Loadout == _displayedLoadout);
        }
    }
}
