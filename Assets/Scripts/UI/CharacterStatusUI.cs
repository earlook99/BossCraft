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
        [SerializeField] private RectTransform _shieldSeparator; // HP와 Shield 경계선
        [SerializeField] private GameObject _dividerPrefab; // 방어막 칸 구분선 프리팹
        
        [Header("Visual Settings")]
        [SerializeField] private Color _hpColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _shieldColor = new Color(0.3f, 0.6f, 1f, 0.9f);
        [SerializeField] private float _transitionDuration = 0.5f;
        [SerializeField] private bool _hideHPTextForBoss = true; // 보스 HP 텍스트 숨김 옵션
        
        private BattleEntity _linkedEntity;
        private BossEntity _linkedBoss;
        private Coroutine _transitionCoroutine;
        private List<GameObject> _stackDividers = new List<GameObject>(); // 방어막 스택 구분선들
        
        private void Awake()
        {
            // 필요시 초기화 코드
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
                if (_shieldFillImage != null)
                    _shieldFillImage.gameObject.SetActive(false);
                if (_shieldSeparator != null)
                    _shieldSeparator.gameObject.SetActive(false);
            }
            UpdateHP(_linkedEntity.CurrentHP);
        }
        
        private void SetupShieldSystem()
        {
            if (_shieldFillImage != null)
            {
                _shieldFillImage.color = _shieldColor;
                _shieldFillImage.gameObject.SetActive(false);
                
                // Shield Fill의 초기 anchor 설정 (중요!)
                _shieldFillImage.rectTransform.anchorMin = new Vector2(0, 0);
                _shieldFillImage.rectTransform.anchorMax = new Vector2(1, 1);
                _shieldFillImage.rectTransform.offsetMin = Vector2.zero;
                _shieldFillImage.rectTransform.offsetMax = Vector2.zero;
            }
            
            if (_hpFillImage != null)
                _hpFillImage.color = _hpColor;
                
            // 보스 HP 텍스트 초기 설정
            if (_hideHPTextForBoss && _hpText != null)
            {
                _hpText.gameObject.SetActive(false);
            }
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
                _hpBar.value = newHP;
                
                // 보스가 아니거나, 보스여도 HP 표시 옵션이 켜져있을 때만 텍스트 표시
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
        }
        
        private void UpdateHPFillOnly(int hp, int maxHP)
        {
            if (_hpFillImage == null) return;
            
            float hpRatio = (float)hp / maxHP;
            _hpFillImage.rectTransform.anchorMax = new Vector2(hpRatio, 1f);
            
            if (_shieldFillImage != null)
                _shieldFillImage.gameObject.SetActive(false);
                
            if (_shieldSeparator != null)
                _shieldSeparator.gameObject.SetActive(false);
                
            // 모든 스택 구분선 숨기기
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
            
            // Slider는 전체 값 (HP + Shield)
            _hpBar.value = hp + shield;
            
            // HP Fill은 현재 HP만
            _hpFillImage.rectTransform.anchorMax = new Vector2(hpRatio, 1f);
            
            // Shield Fill은 HP 끝에서 시작해서 전체까지
            _shieldFillImage.gameObject.SetActive(true);
            _shieldFillImage.rectTransform.anchorMin = new Vector2(hpRatio, 0f);
            _shieldFillImage.rectTransform.anchorMax = new Vector2(totalRatio, 1f);
            _shieldFillImage.rectTransform.offsetMin = Vector2.zero;
            _shieldFillImage.rectTransform.offsetMax = Vector2.zero;
            
            // 보스 HP 텍스트 처리
            if (_hideHPTextForBoss && _hpText != null)
            {
                _hpText.gameObject.SetActive(false);
            }
            
            // HP와 Shield 경계선 표시
            if (_shieldSeparator != null)
            {
                _shieldSeparator.gameObject.SetActive(true);
                // FillContainer의 실제 너비 사용
                float containerWidth = _fillContainer.rect.width;
                float separatorX = containerWidth * hpRatio;
                
                // separator를 FillContainer의 자식으로 만들고 위치 설정
                _shieldSeparator.SetParent(_fillContainer, false);
                _shieldSeparator.anchorMin = new Vector2(0, 0.5f);
                _shieldSeparator.anchorMax = new Vector2(0, 0.5f);
                _shieldSeparator.pivot = new Vector2(0.5f, 0.5f);
                _shieldSeparator.anchoredPosition = new Vector2(separatorX, 0);
            }
            
            // 방어막 스택 구분선 업데이트
            UpdateShieldStackDividers(hpRatio, shieldRatio, _linkedBoss.ShieldStacks);
        }
        
        private void UpdateShieldStackDividers(float hpRatio, float shieldRatio, int currentStacks)
        {
            // 기존 구분선 모두 제거
            foreach (var divider in _stackDividers)
            {
                if (divider != null)
                    Destroy(divider);
            }
            _stackDividers.Clear();
            
            // 스택이 2개 이상일 때만 구분선 생성
            if (currentStacks > 1 && _dividerPrefab != null && _fillContainer != null)
            {
                // FillContainer의 실제 너비 가져오기
                float containerWidth = _fillContainer.rect.width;
                if (containerWidth <= 0) 
                {
                    // rect.width가 0이면 LayoutRebuilder 강제 실행
                    Canvas.ForceUpdateCanvases();
                    containerWidth = _fillContainer.rect.width;
                }
                
                float shieldStartX = containerWidth * hpRatio;
                float shieldWidth = containerWidth * shieldRatio;
                
                for (int i = 1; i < currentStacks; i++)
                {
                    GameObject divider = Instantiate(_dividerPrefab, _fillContainer);
        
                    var rectTransform = divider.GetComponent<RectTransform>();
                    var image = divider.GetComponent<Image>();
        
                    // 구분선을 더 굵게
                    rectTransform.sizeDelta = new Vector2(4, _fillContainer.rect.height);
        
                    // 투명한 검은색으로 (배경이 보이도록)
                    image.color = new Color(0, 0, 0, 0); // 완전 투명
        
                    // 또는 반투명 배경색
                    // image.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        
                    // Mask 효과를 위해 Mask 컴포넌트 추가
                    divider.AddComponent<Mask>().showMaskGraphic = false;
                }
            }
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
            
            // HP는 즉시 최종 상태로 설정
            float endHPRatio = (float)hpAfter / _linkedEntity.MaxHP;
            _hpFillImage.rectTransform.anchorMax = new Vector2(endHPRatio, 1f);
            
            // Shield 설정
            _shieldFillImage.gameObject.SetActive(true);
            float shieldRatio = (float)shieldAmount / _linkedEntity.MaxHP;
            float totalRatio = endHPRatio + shieldRatio;
            
            // Shield 위치 즉시 설정
            _shieldFillImage.rectTransform.anchorMin = new Vector2(endHPRatio, 0f);
            _shieldFillImage.rectTransform.anchorMax = new Vector2(totalRatio, 1f);
            
            // 초기 상태: 투명하고 약간 큰 크기
            _shieldFillImage.color = new Color(_shieldColor.r, _shieldColor.g, _shieldColor.b, 0f);
            _shieldFillImage.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            
            // HP 플래시
            float hpFlashDuration = 0.2f;
            float elapsed = 0f;
            
            while (elapsed < hpFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / hpFlashDuration;
                _hpFillImage.color = Color.Lerp(_hpColor, Color.white, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            _hpFillImage.color = _hpColor;
            
            // Shield "탁!" 애니메이션
            float shieldAnimDuration = 0.3f;
            elapsed = 0f;
            
            while (elapsed < shieldAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shieldAnimDuration;
                
                // 페이드인
                float alpha = Mathf.Pow(t, 0.5f); // 빠르게 시작, 천천히 끝
                _shieldFillImage.color = new Color(_shieldColor.r, _shieldColor.g, _shieldColor.b, alpha * 0.9f);
                
                // 크기 애니메이션 (오버슈트 효과)
                float scale = 1f + (0.3f * Mathf.Pow(1f - t, 3f));
                _shieldFillImage.transform.localScale = new Vector3(scale, scale, 1f);
                
                yield return null;
            }
            
            // 최종 상태
            _shieldFillImage.color = _shieldColor;
            _shieldFillImage.transform.localScale = Vector3.one;
            
            // 구분선 표시
            UpdateCombinedBar(hpAfter, shieldAmount, _linkedEntity.MaxHP);
            
            // 스택별로 구분선 순차 페이드인 (선택사항)
            if (_linkedBoss.ShieldStacks > 1)
            {
                yield return FadeInDividersSequentially();
            }
        }
        
        private IEnumerator FadeInDividersSequentially()
        {
            float fadeTime = 0.1f;
            
            foreach (var divider in _stackDividers)
            {
                if (divider == null) continue;
                
                var image = divider.GetComponent<Image>();
                if (image == null) continue;
                
                Color originalColor = image.color;
                image.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
                
                float elapsed = 0f;
                while (elapsed < fadeTime)
                {
                    elapsed += Time.deltaTime;
                    float alpha = elapsed / fadeTime;
                    image.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                    yield return null;
                }
                
                yield return new WaitForSeconds(0.05f); // 각 구분선 사이 약간의 딜레이
            }
        }
        
        public void UpdateShield(int currentStacks, int maxStacks, int shieldHP)
        {
            if (_linkedBoss == null) return;
            
            UpdateCombinedBar(_linkedBoss.CurrentHP, shieldHP, _linkedBoss.MaxHP);
        }
    }
}