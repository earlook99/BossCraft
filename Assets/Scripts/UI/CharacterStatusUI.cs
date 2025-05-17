using System.Collections;
using Entity;
using GameSystem;
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
        [SerializeField] private Image typeIcon;
        
        private BattleEntity _linkedEntity;
        
        private int _entityIndex;
        public int EntityIndex => _entityIndex;

        public void Setup(BattleEntity entity, int index)
        {
            _linkedEntity = entity;
            _entityIndex = index;
            
            nameText.text = entity.EntityName;
            if (entity.EntitySprite is not null)
            {
                characterIcon.sprite = entity.EntitySprite;
            }
                
            UpdateTypeIcon(entity.ElementType);
            UpdateHP();
        }

        public void UpdateHP()
        {
            if (_linkedEntity == null) return;
            
            hpBar.maxValue = _linkedEntity.MaxHP;
            hpBar.value = _linkedEntity.CurrentHP;
            hpText.text = $"{_linkedEntity.CurrentHP}/{_linkedEntity.MaxHP}";

            if ((float)_linkedEntity.CurrentHP / _linkedEntity.MaxHP < 0.2f)
            {
                hpBar.fillRect.GetComponent<Image>().color = Color.red;
            }
            else
            {
                hpBar.fillRect.GetComponent<Image>().color = Color.green;
            }
        }
        
        private void UpdateTypeIcon(ElementType type)
        {
            Color typeColor = Color.white;
            
            switch (type)
            {
                case ElementType.Fire: typeColor = new Color(1f, 0.3f, 0.3f); break;
                case ElementType.Water: typeColor = new Color(0.3f, 0.5f, 1f); break;
                case ElementType.Electric: typeColor = new Color(1f, 0.9f, 0.2f); break;
                case ElementType.Rock: typeColor = new Color(0.6f, 0.6f, 0.5f); break;
                case ElementType.Grass: typeColor = new Color(0.3f, 0.8f, 0.3f); break;
            }
            
            typeIcon.color = typeColor;
        }
        
        public void PlayDamageAnimation()
        {
            StartCoroutine(DamageAnimationCoroutine());
        }
        
        private IEnumerator DamageAnimationCoroutine()
        {
            Vector3 originalScale = transform.localScale;
            Vector3 punchScale = new Vector3(1.1f, 0.9f, 1f);
    
            transform.localScale = punchScale;
            yield return new WaitForSeconds(0.1f);
    
            transform.localScale = originalScale;
        }
        
        public void ShowDamageNumber(int amount)
        {
        }
    }
}