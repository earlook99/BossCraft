using Entity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using static GameSystem.GameConstants.UI; 

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
        private static readonly Color COLOR_SHIELD_DIVIDER = new Color(1f, 1f, 1f, 0.5f); // 반투명 흰색
        
        private RectTransform _hpFillRectTransform;
        private RectTransform _shieldFillRectTransform;
        private Image[] _dividerImages;
        
        private float _currentDisplayHP;
        private float _targetHP;
        private bool _isAnimatingHP;
        
        private float _currentDisplayShieldHP;
        private float _targetShieldHP;
        private Coroutine _shieldAnimationCoroutine;

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
            
            if (_linkedBoss != null)
            {
                _currentDisplayShieldHP = _linkedBoss.ShieldHP;
                _targetShieldHP = _linkedBoss.ShieldHP;
            }
            
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
            
            float startHP = _currentDisplayHP;
            float distance = _targetHP - startHP;
            float duration = Mathf.Min(Mathf.Abs(distance) / (_linkedEntity.MaxHP * 0.5f), 1.5f); // 최대 1.5초
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // EaseOutCubic 곡선 사용 - 시작은 빠르고 끝에서 약간만 감속
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                
                _currentDisplayHP = startHP + (distance * easedT);
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
                UpdateCombinedBarDisplay(displayHP, Mathf.RoundToInt(_currentDisplayShieldHP), _linkedEntity.MaxHP);
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
            
            // 실드 칸 구분선은 최대 실드 HP 기준으로 고정 위치, 현재 스택 수만큼만 표시
            if (_linkedBoss != null && _linkedBoss.HasShield)
            {
                float maxShieldRatio = (float)_linkedBoss.MaxShieldHP / maxHP;
                UpdateShieldStackDividers(hpRatio, maxShieldRatio, _linkedBoss.ShieldStacks);
            }
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
            
            // 구분선은 최대 스택 기준으로 위치를 계산하되, 현재 스택 수만큼만 표시
            int maxStacks = _linkedBoss != null ? _linkedBoss.MaxShieldStacks : currentStacks;
            int dividerCount = currentStacks - 1;
            _dividerImages = new Image[dividerCount];
            
            for (int i = 0; i < dividerCount; i++)
            {
                CreateStackDivider(shieldStartX, shieldWidth, i + 1, maxStacks, i);
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
            image.color = COLOR_SHIELD_DIVIDER;  // 반투명 흰색으로 표시

            float dividerX = shieldStartX + (shieldWidth * stackIndex / totalStacks);
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(0, 0.5f);
            rectTransform.anchoredPosition = new Vector2(dividerX, 0);
            rectTransform.localPosition = new Vector3(rectTransform.localPosition.x, rectTransform.localPosition.y, -1f); // Z축으로 앞으로

            // 구분선을 맨 앞으로 이동
            rectTransform.SetAsLastSibling();
            
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
            
            // 실드 HP 초기화
            _currentDisplayShieldHP = shieldAmount;
            _targetShieldHP = shieldAmount;
            
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
            Color originalColor = _hpColor;
            Color flashColor = new Color(1f, 0.9f, 0.8f, 1f); // 밝은 주황빛 플래시
            
            // 즉시 밝게 번쩍
            _hpFillImage.color = flashColor;
            yield return new WaitForSeconds(0.08f);
            
            // 원래 색으로 빠르게 페이드
            float elapsed = 0f;
            float fadeTime = HP_FLASH_DURATION - 0.08f;
            
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeTime;
                // EaseOutQuad로 자연스럽게
                float easedT = t * (2f - t);
                _hpFillImage.color = Color.Lerp(flashColor, originalColor, easedT);
                yield return null;
            }
            
            _hpFillImage.color = originalColor;
        }

        private IEnumerator AnimateShieldAppear()
        {
            float elapsed = 0f;
            Color targetColor = _shieldColor;
            Color flashColor = new Color(0.8f, 0.9f, 1f, 1f); // 밝은 하늘색 플래시
            
            // 실드 영역 전체를 밝게 번쩍
            _shieldFillImage.color = flashColor;
            yield return new WaitForSeconds(0.05f);
            
            // 페이드 인과 스케일 애니메이션
            while (elapsed < SHIELD_ANIM_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / SHIELD_ANIM_DURATION;
                
                // 플래시에서 원래 색으로 전환
                Color currentColor = Color.Lerp(flashColor, targetColor, t);
                
                // 추가 반짝임 효과 (펄스)
                float pulse = Mathf.Sin(t * Mathf.PI * 3f) * 0.2f * (1f - t); // 점점 약해지는 펄스
                currentColor = new Color(
                    currentColor.r + pulse,
                    currentColor.g + pulse,
                    currentColor.b + pulse,
                    currentColor.a
                );
                
                _shieldFillImage.color = currentColor;
                
                // 스케일 애니메이션 (1.3 -> 1.0으로 부드럽게)
                float scale = Mathf.Lerp(SHIELD_INITIAL_SCALE, 1f, t);
                _shieldFillImage.transform.localScale = new Vector3(scale, scale, 1f);
                
                yield return null;
            }
            
            _shieldFillImage.color = targetColor;
            _shieldFillImage.transform.localScale = Vector3.one;
        }
        
        private IEnumerator FadeInDividersSequentially()
        {
            if (_dividerImages == null) yield break;
            
            Color targetColor = COLOR_SHIELD_DIVIDER;
            
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
        
        public Vector3 GetShieldStackWorldPosition(int stackIndex, int totalStacks)
        {
            if (_fillContainer == null || _linkedBoss == null) 
                return transform.position;
            
            float containerWidth = GetContainerWidth();
            float hpRatio = (float)_linkedBoss.CurrentHP / _linkedBoss.MaxHP;
            float maxShieldRatio = (float)_linkedBoss.MaxShieldHP / _linkedBoss.MaxHP;
            
            float shieldStartX = containerWidth * hpRatio;
            float shieldWidth = containerWidth * maxShieldRatio;
            
            // 스택의 중앙 위치 계산
            float stackPosition = shieldStartX + (shieldWidth * (stackIndex + 0.5f) / totalStacks);
            
            // 로컬 좌표를 월드 좌표로 변환
            Vector3 localPos = new Vector3(stackPosition - containerWidth * 0.5f, 0, 0);
            Vector3 worldPos = _fillContainer.TransformPoint(localPos);
            
            // Canvas가 Screen Space Overlay인 경우 처리
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // RectTransform의 스크린 좌표를 가져와서 월드 좌표로 변환
                Camera cam = Camera.main;
                Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);
                screenPos.z = 2f; // 카메라로부터의 거리 (더 가깝게)
                worldPos = cam.ScreenToWorldPoint(screenPos);
            }
            
            return worldPos;
        }
        
        public Vector3 GetShieldBarCenterWorldPosition()
        {
            if (_fillContainer == null || _linkedBoss == null)
                return transform.position;
                
            float containerWidth = GetContainerWidth();
            float hpRatio = (float)_linkedBoss.CurrentHP / _linkedBoss.MaxHP;
            float shieldRatio = (float)_linkedBoss.ShieldHP / _linkedBoss.MaxHP;
            
            float shieldCenterX = containerWidth * (hpRatio + shieldRatio * 0.5f);
            
            Vector3 localPos = new Vector3(shieldCenterX - containerWidth * 0.5f, 0, 0);
            Vector3 worldPos = _fillContainer.TransformPoint(localPos);
            
            // Canvas가 Screen Space Overlay인 경우 처리
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                Camera cam = Camera.main;
                Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);
                screenPos.z = 2f; // 카메라로부터의 거리 (더 가깝게)
                worldPos = cam.ScreenToWorldPoint(screenPos);
            }
            
            return worldPos;
        }
        
        public void UpdateShield(int currentStacks, int maxStacks, int shieldHP)
        {
            if (_linkedBoss == null) return;
            
            _targetShieldHP = shieldHP;
            
            // 애니메이션으로 부드럽게 감소
            if (_shieldAnimationCoroutine != null)
                StopCoroutine(_shieldAnimationCoroutine);
                
            _shieldAnimationCoroutine = StartCoroutine(AnimateShieldHP());
        }
        
        public void UpdateShieldImmediate(int currentStacks, int maxStacks, int shieldHP)
        {
            if (_linkedBoss == null) return;
            
            _targetShieldHP = shieldHP;
            _currentDisplayShieldHP = shieldHP;
            
            // 즉시 업데이트 (애니메이션 없이)
            UpdateCombinedBarDisplay(_linkedBoss.CurrentHP, shieldHP, _linkedBoss.MaxHP);
        }
        
        public IEnumerator FlashShieldBar()
        {
            if (_shieldFillImage == null) yield break;
            
            Color originalColor = _shieldFillImage.color;
            Color flashColor = Color.white;
            
            // 흰색으로 번쩍
            _shieldFillImage.color = flashColor;
            yield return new WaitForSeconds(0.1f);
            
            // 원래 색으로 복귀
            _shieldFillImage.color = originalColor;
            yield return new WaitForSeconds(0.1f);
        }
        
        private IEnumerator AnimateShieldHP()
        {
            float startShieldHP = _currentDisplayShieldHP;
            float distance = _targetShieldHP - startShieldHP;
            float duration = Mathf.Min(Mathf.Abs(distance) / (_linkedBoss.MaxShieldHP * 0.5f), 1.0f); // 최대 1초
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // EaseOutCubic 곡선 사용
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                
                _currentDisplayShieldHP = startShieldHP + (distance * easedT);
                UpdateCombinedBarDisplay(_linkedBoss.CurrentHP, Mathf.RoundToInt(_currentDisplayShieldHP), _linkedBoss.MaxHP);
                yield return null;
            }
            
            _currentDisplayShieldHP = _targetShieldHP;
            UpdateCombinedBarDisplay(_linkedBoss.CurrentHP, Mathf.RoundToInt(_currentDisplayShieldHP), _linkedBoss.MaxHP);
            
            _shieldAnimationCoroutine = null;
        }
    }
}