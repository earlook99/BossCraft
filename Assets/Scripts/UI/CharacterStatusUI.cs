using Entity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace UI
{
    public class CharacterStatusUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Slider _hpBar;
        [SerializeField] private TextMeshProUGUI _hpText;

        [Header("Combined HP/Shield System")]
        [SerializeField] private RectTransform _fillContainer;
        [SerializeField] private Image _hpFillImage;
        [SerializeField] private Image _shieldFillImage;
        [SerializeField] private RectTransform _shieldSeparator;
        [SerializeField] private GameObject _dividerPrefab;
        
        [Header("Visual Settings")]
        [SerializeField] private Color _hpColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _shieldColor = new Color(0.3f, 0.6f, 1f, 0.9f);
        [SerializeField] private float _transitionDuration = 0.5f;
        [SerializeField] private bool _hideHPTextForBoss = true;
        
        private BattleEntity _linkedEntity;
        private BossEntity _linkedBoss;
        private Coroutine _transitionCoroutine;
        private List<GameObject> _stackDividers = new List<GameObject>();

        private const float HP_FLASH_DURATION = 0.2f;
        private const float SHIELD_ANIM_DURATION = 0.3f;
        private const float SHIELD_INITIAL_SCALE = 1.3f;
        private const float SHIELD_ALPHA = 0.9f;
        private const float DIVIDER_WIDTH = 4f;
        private const float DIVIDER_FADE_TIME = 0.1f;
        private const float DIVIDER_DELAY = 0.05f;

        public void Setup(BattleEntity entity, int index)
        {
            _linkedEntity = entity;
            _nameText.text = entity.EntityName;
            
            if (entity is BossEntity boss)
            {
                _linkedBoss = boss;
                SetupShieldSystem();
            }
            else
            {
                HideShieldElements();
            }
            
            UpdateHP(_linkedEntity.CurrentHP);
        }
        
        private void SetupShieldSystem()
        {
            if (_shieldFillImage != null)
            {
                _shieldFillImage.color = _shieldColor;
                _shieldFillImage.gameObject.SetActive(false);
                InitializeShieldFillAnchors();
            }
            
            if (_hpFillImage != null)
                _hpFillImage.color = _hpColor;
                
            if (_hideHPTextForBoss && _hpText != null)
            {
                _hpText.gameObject.SetActive(false);
            }
        }

        private void InitializeShieldFillAnchors()
        {
            _shieldFillImage.rectTransform.anchorMin = new Vector2(0, 0);
            _shieldFillImage.rectTransform.anchorMax = new Vector2(1, 1);
            _shieldFillImage.rectTransform.offsetMin = Vector2.zero;
            _shieldFillImage.rectTransform.offsetMax = Vector2.zero;
        }

        private void HideShieldElements()
        {
            if (_shieldFillImage != null)
                _shieldFillImage.gameObject.SetActive(false);
            if (_shieldSeparator != null)
                _shieldSeparator.gameObject.SetActive(false);
        }

        public void UpdateHP(int newHP)
        {
            if (_linkedEntity == null) return;

            _hpBar.maxValue = _linkedEntity.MaxHP;
            
            if (_linkedBoss != null && _linkedBoss.HasShield)
            {
                UpdateCombinedBar(newHP, _linkedBoss.ShieldHP, _linkedEntity.MaxHP);
            }
            else
            {
                UpdateRegularHP(newHP);
            }
        }

        private void UpdateRegularHP(int newHP)
        {
            _hpBar.value = newHP;
            
            if (_linkedBoss == null || !_hideHPTextForBoss)
            {
                _hpText.text = $"{newHP}/{_linkedEntity.MaxHP}";
            }
            else if (_hpText != null)
            {
                _hpText.gameObject.SetActive(false);
            }
            
            UpdateHPFillOnly(newHP, _linkedEntity.MaxHP);
        }
        
        private void UpdateHPFillOnly(int hp, int maxHP)
        {
            if (_hpFillImage == null) return;
            
            float hpRatio = (float)hp / maxHP;
            _hpFillImage.rectTransform.anchorMax = new Vector2(hpRatio, 1f);
            
            HideShieldElements();
            ClearStackDividers();
        }

        private void ClearStackDividers()
        {
            foreach (var divider in _stackDividers)
            {
                if (divider != null)
                    Destroy(divider);
            }
            _stackDividers.Clear();
        }
        
        private void UpdateCombinedBar(int hp, int shield, int maxHP)
        {
            if (_hpFillImage == null || _shieldFillImage == null) return;
            
            float hpRatio = (float)hp / maxHP;
            float shieldRatio = (float)shield / maxHP;
            float totalRatio = hpRatio + shieldRatio;
            
            _hpBar.value = hp + shield;
            
            UpdateHPAndShieldFills(hpRatio, totalRatio);
            
            if (_hideHPTextForBoss && _hpText != null)
            {
                _hpText.gameObject.SetActive(false);
            }
            
            UpdateShieldSeparator(hpRatio);
            UpdateShieldStackDividers(hpRatio, shieldRatio, _linkedBoss.ShieldStacks);
        }

        private void UpdateHPAndShieldFills(float hpRatio, float totalRatio)
        {
            _hpFillImage.rectTransform.anchorMax = new Vector2(hpRatio, 1f);
            
            _shieldFillImage.gameObject.SetActive(true);
            _shieldFillImage.rectTransform.anchorMin = new Vector2(hpRatio, 0f);
            _shieldFillImage.rectTransform.anchorMax = new Vector2(totalRatio, 1f);
            _shieldFillImage.rectTransform.offsetMin = Vector2.zero;
            _shieldFillImage.rectTransform.offsetMax = Vector2.zero;
        }

        private void UpdateShieldSeparator(float hpRatio)
        {
            if (_shieldSeparator == null) return;
            
            _shieldSeparator.gameObject.SetActive(true);
            float containerWidth = _fillContainer.rect.width;
            float separatorX = containerWidth * hpRatio;
            
            _shieldSeparator.SetParent(_fillContainer, false);
            _shieldSeparator.anchorMin = new Vector2(0, 0.5f);
            _shieldSeparator.anchorMax = new Vector2(0, 0.5f);
            _shieldSeparator.pivot = new Vector2(0.5f, 0.5f);
            _shieldSeparator.anchoredPosition = new Vector2(separatorX, 0);
        }
        
        private void UpdateShieldStackDividers(float hpRatio, float shieldRatio, int currentStacks)
        {
            ClearStackDividers();
            
            if (currentStacks <= 1 || _dividerPrefab == null || _fillContainer == null)
                return;

            float containerWidth = GetContainerWidth();
            float shieldStartX = containerWidth * hpRatio;
            float shieldWidth = containerWidth * shieldRatio;
            
            for (int i = 1; i < currentStacks; i++)
            {
                CreateStackDivider(shieldStartX, shieldWidth, i, currentStacks);
            }
        }

        private float GetContainerWidth()
        {
            float containerWidth = _fillContainer.rect.width;
            if (containerWidth <= 0) 
            {
                Canvas.ForceUpdateCanvases();
                containerWidth = _fillContainer.rect.width;
            }
            return containerWidth;
        }

        private void CreateStackDivider(float shieldStartX, float shieldWidth, int index, int totalStacks)
        {
            GameObject divider = Instantiate(_dividerPrefab, _fillContainer);
            var rectTransform = divider.GetComponent<RectTransform>();
            var image = divider.GetComponent<Image>();

            rectTransform.sizeDelta = new Vector2(DIVIDER_WIDTH, _fillContainer.rect.height);
            image.color = new Color(0, 0, 0, 0);

            float dividerX = shieldStartX + (shieldWidth * index / totalStacks);
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(0, 0.5f);
            rectTransform.anchoredPosition = new Vector2(dividerX, 0);

            divider.AddComponent<Mask>().showMaskGraphic = false;
            _stackDividers.Add(divider);
        }
        
        public void AnimateShieldConversion(int hpBefore, int hpAfter, int shieldAmount)
        {
            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);
                
            _transitionCoroutine = StartCoroutine(ShieldConversionAnimation(hpBefore, hpAfter, shieldAmount));
        }
        
        private IEnumerator ShieldConversionAnimation(int hpBefore, int hpAfter, int shieldAmount)
        {
            if (_shieldFillImage == null || _hpFillImage == null) yield break;
            
            SetupShieldAnimation(hpAfter, shieldAmount);
            
            yield return AnimateHPFlash();
            yield return AnimateShieldAppear();
            
            UpdateCombinedBar(hpAfter, shieldAmount, _linkedEntity.MaxHP);
            
            if (_linkedBoss.ShieldStacks > 1)
            {
                yield return FadeInDividersSequentially();
            }
        }

        private void SetupShieldAnimation(int hpAfter, int shieldAmount)
        {
            float endHPRatio = (float)hpAfter / _linkedEntity.MaxHP;
            _hpFillImage.rectTransform.anchorMax = new Vector2(endHPRatio, 1f);
            
            _shieldFillImage.gameObject.SetActive(true);
            float shieldRatio = (float)shieldAmount / _linkedEntity.MaxHP;
            float totalRatio = endHPRatio + shieldRatio;
            
            _shieldFillImage.rectTransform.anchorMin = new Vector2(endHPRatio, 0f);
            _shieldFillImage.rectTransform.anchorMax = new Vector2(totalRatio, 1f);
            
            _shieldFillImage.color = new Color(_shieldColor.r, _shieldColor.g, _shieldColor.b, 0f);
            _shieldFillImage.transform.localScale = new Vector3(SHIELD_INITIAL_SCALE, SHIELD_INITIAL_SCALE, 1f);
        }

        private IEnumerator AnimateHPFlash()
        {
            float elapsed = 0f;
            
            while (elapsed < HP_FLASH_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / HP_FLASH_DURATION;
                _hpFillImage.color = Color.Lerp(_hpColor, Color.white, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            _hpFillImage.color = _hpColor;
        }

        private IEnumerator AnimateShieldAppear()
        {
            float elapsed = 0f;
            
            while (elapsed < SHIELD_ANIM_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / SHIELD_ANIM_DURATION;
                
                float alpha = Mathf.Pow(t, 0.5f);
                _shieldFillImage.color = new Color(_shieldColor.r, _shieldColor.g, _shieldColor.b, alpha * SHIELD_ALPHA);
                
                float scale = 1f + (0.3f * Mathf.Pow(1f - t, 3f));
                _shieldFillImage.transform.localScale = new Vector3(scale, scale, 1f);
                
                yield return null;
            }
            
            _shieldFillImage.color = _shieldColor;
            _shieldFillImage.transform.localScale = Vector3.one;
        }
        
        private IEnumerator FadeInDividersSequentially()
        {
            foreach (var divider in _stackDividers)
            {
                if (divider == null) continue;
                
                var image = divider.GetComponent<Image>();
                if (image == null) continue;
                
                yield return FadeDivider(image);
                yield return new WaitForSeconds(DIVIDER_DELAY);
            }
        }

        private IEnumerator FadeDivider(Image image)
        {
            Color originalColor = image.color;
            image.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
            
            float elapsed = 0f;
            while (elapsed < DIVIDER_FADE_TIME)
            {
                elapsed += Time.deltaTime;
                float alpha = elapsed / DIVIDER_FADE_TIME;
                image.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }
        }
        
        public void UpdateShield(int currentStacks, int maxStacks, int shieldHP)
        {
            if (_linkedBoss == null) return;
            UpdateCombinedBar(_linkedBoss.CurrentHP, shieldHP, _linkedBoss.MaxHP);
        }
    }
}