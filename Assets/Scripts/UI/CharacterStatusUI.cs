using Entity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Manages the UI elements for displaying a single character's status (icon, name, HP).
    /// </summary>
    public class CharacterStatusUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _characterIcon; // Image component for the character's icon
        [SerializeField] private TextMeshProUGUI _nameText; // Text component for the character's name
        [SerializeField] private Slider _hpBar; // Slider component for the HP bar
        [SerializeField] private TextMeshProUGUI _hpText; // Text component for the HP values (current/max)

        private BattleEntity _linkedEntity; // The BattleEntity this UI represents

        private Image _fillImage; // Reference to the fill image of the HP bar slider for color changes

        /// <summary>
        /// Sets up the character status UI with the specified entity's data.
        /// </summary>
        /// <param name="entity">The <see cref="BattleEntity"/> to link to this UI.</param>
        /// <param name="index">The index of the entity.</param>
        public void Setup(BattleEntity entity, int index)
        {
            _linkedEntity = entity;
            // _entityIndex = index; // Field removed

            _nameText.text = entity.EntityName;
            if (entity.EntitySprite != null)
            {
                _characterIcon.sprite = entity.EntitySprite;
            }
            
            if (_hpBar != null && _hpBar.fillRect != null) // Ensure HP bar and its fillRect are assigned
            {
                _fillImage = _hpBar.fillRect.GetComponent<Image>();
            }
        
            UpdateHP(_linkedEntity.MaxHP);
        }

        /// <summary>
        /// Updates the HP bar and text to reflect the new HP value.
        /// Also changes the HP bar color based on the HP ratio.
        /// </summary>
        /// <param name="newHP">The new HP value to display.</param>
        public void UpdateHP(int newHP)
        {
            if (_linkedEntity is null)
            {
                return;
            }

            _hpBar.maxValue = _linkedEntity.MaxHP;
            _hpBar.value = newHP;
            _hpText.text = $"{newHP}/{_linkedEntity.MaxHP}";
        
            if (_hpBar.fillRect != null && _fillImage != null) // More robust check
            {
                float ratio = (float)newHP / _linkedEntity.MaxHP;
                // Change HP bar color to red if HP is below 20%, otherwise green.
                _fillImage.color = (ratio < 0.2f) ? Color.red : Color.green; 
            }
        }
    }
}
