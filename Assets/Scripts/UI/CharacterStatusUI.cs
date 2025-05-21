using Entity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CharacterStatusUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image characterIcon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Slider hpBar;
        [SerializeField] private TextMeshProUGUI hpText;

        private BattleEntity _linkedEntity;
        private int _entityIndex;

        private Image _fillImage;

        public void Setup(BattleEntity entity, int index)
        {
            _linkedEntity = entity;
            _entityIndex = index;

            nameText.text = entity.EntityName;
            if (entity.EntitySprite != null)
            {
                characterIcon.sprite = entity.EntitySprite;
            }
            
            _fillImage = hpBar.fillRect.GetComponent<Image>();
        
            UpdateHP(_linkedEntity.MaxHP);
        }

        public void UpdateHP(int newHP)
        {
            if (_linkedEntity is null)
            {
                return;
            }

            hpBar.maxValue = _linkedEntity.MaxHP;
            hpBar.value = newHP;
            hpText.text = $"{newHP}/{_linkedEntity.MaxHP}";
        
            if (hpBar.fillRect && _fillImage)
            {
                float ratio = (float)newHP/ _linkedEntity.MaxHP;
                _fillImage.color = (ratio < 0.2f) ? Color.red : Color.green;
            }
        }
    }
}
