using Entity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;

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
        private Coroutine _hpAnimationCoroutine;
        private Coroutine _transitionCoroutine;
        private List<GameObject> _stackDividers = new List<GameObject>();
        
        private static readonly StringBuilder _hpTextBuilder = new StringBuilder(16);
        private static readonly Color COLOR_WHITE = Color.white;
        private static readonly Color COLOR_BLACK = Color.black;
        private static readonly Color COLOR_BLACK_TRANSPARENT = new Color(0, 0, 0, 0);
        
        private RectTransform _hpFillRectTransform;
        private RectTransform _shieldFillRectTransform;
        private Image[] _dividerImages;
        
        private float _currentDisplayHP;
        private float _targetHP;
        private bool _isAnimatingHP;

        private const float HP_ANIMATION_SPEED = 2f;
        private const float HP_FLASH_DURATION = 0.2f;
        private const float SHIELD_ANIM_DURATION = 0.3f;
        private const float SHIELD_INITIAL_SCALE = 1.3f;
        private const float SHIELD_ALPHA = 0.9f;
        private const float DIVIDER_WIDTH = 4f;
        private const float DIVIDER_FADE_TIME = 0.1f;
        private const float DIVIDER_DELAY = 0.05f;

        private void Awake()
        {
            CacheComponents();
        }

        private void CacheComponents()
        {
            if (_hpFillImage != null)
                _hpFillRectTransform = _hpFillImage.rectTransform;
            if (_shieldFillImage != null)
                _shieldFillRectTransform = _shieldFillImage.rectTransform;
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

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
            
            _currentDisplayHP = _linkedEntity.CurrentHP;
            _targetHP = _linkedEntity.CurrentHP;
            UpdateHPImmediate(_linkedEntity.CurrentHP);
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
            _shieldFillRectTransform.anchorMin = Vector2.zero;
            _shieldFillRectTransform.anchorMax = Vector2.one;
            _shieldFillRectTransform.offsetMin = Vector2.zero;
            _shieldFillRectTransform.offsetMax = Vector2.zero;
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
            
            _targetHP = newHP;
            
            if (!_isAnimatingHP && Mathf.Abs(_targetHP - _currentDisplayHP) > 0.1f)
            {
                if (_hpAnimationCoroutine != null)
                    StopCoroutine(_hpAnimationCoroutine);
                    
                _hpAnimationCoroutine = StartCoroutine(AnimateHP());
            }
        }
        
        private IEnumerator AnimateHP()
        {
            _isAnimatingHP = true;
            
            while (Mathf.Abs(_targetHP - _currentDisplayHP) > 0.1f)
            {
                _currentDisplayHP = Mathf.Lerp(_currentDisplayHP, _targetHP, Time.deltaTime * HP_ANIMATION_SPEED);
                UpdateHPDisplay(_currentDisplayHP);
                yield return null;
            }
            
            _currentDisplayHP = _targetHP;
            UpdateHPDisplay(_currentDisplayHP);
            
            _isAnimatingHP = false;
            _hpAnimationCoroutine = null;
        }
        
        private void UpdateHPDisplay(float displayHP)
        {
            _hpBar.maxValue = _linkedEntity.MaxHP;
            
            if (_linkedBoss != null && _linkedBoss.HasShield)
            {
                UpdateCombinedBarDisplay(displayHP, _linkedBoss.ShieldHP, _linkedEntity.MaxHP);
            }
            else
            {
                UpdateRegularHPDisplay(displayHP);
            }
        }
        
        private void UpdateHPImmediate(int hp)
        {
            _currentDisplayHP = hp;
            _targetHP = hp;
            UpdateHPDisplay(hp);
        }

        private void UpdateRegularHPDisplay(float displayHP)
        {
            _hpBar.value = displayHP;
            
            if (_linkedBoss == null || !_hideHPTextForBoss)
            {
                _hpTextBuilder.Clear();
                _hpTextBuilder.Append(Mathf.RoundToInt(displayHP)).Append('/').Append(_linkedEntity.MaxHP);
                _hpText.text = _hpTextBuilder.ToString();
            }
            
            UpdateHPFillOnly(displayHP, _linkedEntity.MaxHP);
        }
        
        private void UpdateHPFillOnly(float hp, int maxHP)
        {
            if (_hpFillRectTransform == null) return;
            
            float hpRatio = hp / maxHP;
            _hpFillRectTransform.anchorMax = new Vector2(hpRatio, 1f);
            
            HideShieldElements();
            ClearStackDividers();
        }

        private void ClearStackDividers()
        {
            for (int i = 0; i < _stackDividers.Count; i++)
            {
                if (_stackDividers[i] != null)
                    Destroy(_stackDividers[i]);
            }
            _stackDividers.Clear();
            _dividerImages = null;
        }
        
        private void UpdateCombinedBarDisplay(float hp, int shield, int maxHP)
        {
            if (_hpFillRectTransform == null || _shieldFillRectTransform == null) return;
            
            float hpRatio = hp / maxHP;
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
            _hpFillRectTransform.anchorMax = new Vector2(hpRatio, 1f);
            
            _shieldFillImage.gameObject.SetActive(true);
            _shieldFillRectTransform.anchorMin = new Vector2(hpRatio, 0f);
            _shieldFillRectTransform.anchorMax = new Vector2(totalRatio, 1f);
            _shieldFillRectTransform.offsetMin = Vector2.zero;
            _shieldFillRectTransform.offsetMax = Vector2.zero;
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
            
            int dividerCount = currentStacks - 1;
            _dividerImages = new Image[dividerCount];
            
            for (int i = 0; i < dividerCount; i++)
            {
                CreateStackDivider(shieldStartX, shieldWidth, i + 1, currentStacks, i);
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

        private void CreateStackDivider(float shieldStartX, float shieldWidth, int stackIndex, int totalStacks, int arrayIndex)
        {
            GameObject divider = Instantiate(_dividerPrefab, _fillContainer);
            var rectTransform = divider.GetComponent<RectTransform>();
            var image = divider.GetComponent<Image>();

            rectTransform.sizeDelta = new Vector2(DIVIDER_WIDTH, _fillContainer.rect.height);
            image.color = COLOR_BLACK_TRANSPARENT;

            float dividerX = shieldStartX + (shieldWidth * stackIndex / totalStacks);
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(0, 0.5f);
            rectTransform.anchoredPosition = new Vector2(dividerX, 0);

            divider.AddComponent<Mask>().showMaskGraphic = false;
            
            _stackDividers.Add(divider);
            _dividerImages[arrayIndex] = image;
        }
        
        public void AnimateShieldConversion(int hpBefore, int hpAfter, int shieldAmount)
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }
            
            if (_hpAnimationCoroutine != null)
            {
                StopCoroutine(_hpAnimationCoroutine);
                _isAnimatingHP = false;
            }
            
            _currentDisplayHP = hpAfter;
            _targetHP = hpAfter;
            
            _transitionCoroutine = StartCoroutine(ShieldConversionAnimation(hpBefore, hpAfter, shieldAmount));
        }
        
        private IEnumerator ShieldConversionAnimation(int hpBefore, int hpAfter, int shieldAmount)
        {
            if (_shieldFillImage == null || _hpFillImage == null) yield break;
            
            SetupShieldAnimation(hpAfter, shieldAmount);
            
            yield return AnimateHPFlash();
            yield return AnimateShieldAppear();
            
            UpdateCombinedBarDisplay(hpAfter, shieldAmount, _linkedEntity.MaxHP);
            
            if (_linkedBoss.ShieldStacks > 1)
            {
                yield return FadeInDividersSequentially();
            }
        }

        private void SetupShieldAnimation(int hpAfter, int shieldAmount)
        {
            float endHPRatio = (float)hpAfter / _linkedEntity.MaxHP;
            _hpFillRectTransform.anchorMax = new Vector2(endHPRatio, 1f);
            
            _shieldFillImage.gameObject.SetActive(true);
            float shieldRatio = (float)shieldAmount / _linkedEntity.MaxHP;
            float totalRatio = endHPRatio + shieldRatio;
            
            _shieldFillRectTransform.anchorMin = new Vector2(endHPRatio, 0f);
            _shieldFillRectTransform.anchorMax = new Vector2(totalRatio, 1f);
            
            Color shieldColorTransparent = _shieldColor;
            shieldColorTransparent.a = 0f;
            _shieldFillImage.color = shieldColorTransparent;
            _shieldFillImage.transform.localScale = new Vector3(SHIELD_INITIAL_SCALE, SHIELD_INITIAL_SCALE, 1f);
        }

        private IEnumerator AnimateHPFlash()
        {
            float elapsed = 0f;
            Color originalColor = _hpColor;
            
            while (elapsed < HP_FLASH_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / HP_FLASH_DURATION;
                _hpFillImage.color = Color.Lerp(originalColor, COLOR_WHITE, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            
            _hpFillImage.color = originalColor;
        }

        private IEnumerator AnimateShieldAppear()
        {
            float elapsed = 0f;
            Color targetColor = _shieldColor;
            
            while (elapsed < SHIELD_ANIM_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / SHIELD_ANIM_DURATION;
                
                float alpha = Mathf.Pow(t, 0.5f) * SHIELD_ALPHA;
                _shieldFillImage.color = new Color(targetColor.r, targetColor.g, targetColor.b, alpha);
                
                float scale = 1f + (0.3f * Mathf.Pow(1f - t, 3f));
                _shieldFillImage.transform.localScale = new Vector3(scale, scale, 1f);
                
                yield return null;
            }
            
            _shieldFillImage.color = targetColor;
            _shieldFillImage.transform.localScale = Vector3.one;
        }
        
        private IEnumerator FadeInDividersSequentially()
        {
            if (_dividerImages == null) yield break;
            
            Color targetColor = COLOR_BLACK;
            
            for (int i = 0; i < _dividerImages.Length; i++)
            {
                var image = _dividerImages[i];
                if (image == null) continue;
                
                yield return FadeDivider(image, targetColor);
                yield return new WaitForSeconds(DIVIDER_DELAY);
            }
        }

        private IEnumerator FadeDivider(Image image, Color targetColor)
        {
            Color startColor = targetColor;
            startColor.a = 0f;
            image.color = startColor;
            
            float elapsed = 0f;
            while (elapsed < DIVIDER_FADE_TIME)
            {
                elapsed += Time.deltaTime;
                float alpha = elapsed / DIVIDER_FADE_TIME;
                targetColor.a = alpha;
                image.color = targetColor;
                yield return null;
            }
        }
        
        public void UpdateShield(int currentStacks, int maxStacks, int shieldHP)
        {
            if (_linkedBoss == null) return;
            UpdateCombinedBarDisplay(_linkedBoss.CurrentHP, shieldHP, _linkedBoss.MaxHP);
        }
    }
}