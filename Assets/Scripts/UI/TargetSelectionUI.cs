using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data;
using Entity;
using GameSystem;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class TargetSelectionUI : MonoBehaviour
    {
        [Header("Visual Feedback")]
        [SerializeField] private GameObject _targetIndicatorPrefab;
        [SerializeField] private Color _validTargetColor = new Color(0.2f, 1f, 0.2f, 0.8f);
        [SerializeField] private Color _selectedTargetColor = new Color(1f, 1f, 0.2f, 1f);
        [SerializeField] private Color _hoverTargetColor = new Color(0.5f, 1f, 0.5f, 0.9f);
        [SerializeField] private float _indicatorScale = 1.5f;
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _pulseAmount = 0.2f;
        
        private UIManager _uiManager;
        private BattleEntity[] _allEntities;
        private List<int> _validTargetIndices = new List<int>();
        private int _currentTargetIndex = 0;
        private Dictionary<int, GameObject> _targetIndicators = new Dictionary<int, GameObject>();
        private Dictionary<int, Button> _targetButtons = new Dictionary<int, Button>();
        
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        
        private Action<EntityType> _onTargetSelected;
        private Action _onCancelled;
        
        private bool _isActive = false;
        private Coroutine _pulseCoroutine;

        private void Awake()
        {
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas == null)
            {
                Debug.LogError("TargetSelectionUI must be a child of a Canvas!");
                return;
            }
            
            _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            
            if (_targetIndicatorPrefab == null)
            {
                CreateDefaultIndicatorPrefab();
            }
        }

        private void CreateDefaultIndicatorPrefab()
        {
            var tempGO = new GameObject("TargetIndicator");
            tempGO.transform.SetParent(transform, false);
            
            var rectTransform = tempGO.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(120, 120);
            
            var canvasGroup = tempGO.AddComponent<CanvasGroup>();
            
            var bgImage = tempGO.AddComponent<Image>();
            bgImage.sprite = CreateCircleSprite();
            bgImage.color = _validTargetColor;
            bgImage.raycastTarget = true;
            
            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(tempGO.transform, false);
            var arrowRect = arrowGO.AddComponent<RectTransform>();
            arrowRect.anchoredPosition = new Vector2(0, -70);
            arrowRect.sizeDelta = new Vector2(40, 40);
            
            var arrowImage = arrowGO.AddComponent<Image>();
            arrowImage.sprite = CreateArrowSprite();
            arrowImage.color = Color.white;
            
            var button = tempGO.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bgImage;
            
            tempGO.SetActive(false);
            _targetIndicatorPrefab = tempGO;
        }

        private Sprite CreateCircleSprite()
        {
            var texture = new Texture2D(128, 128);
            var center = new Vector2(64, 64);
            
            for (int x = 0; x < 128; x++)
            {
                for (int y = 0; y < 128; y++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    
                    if (distance < 62 && distance > 58)
                    {
                        texture.SetPixel(x, y, Color.white);
                    }
                    else if (distance < 58)
                    {
                        float alpha = 0.1f;
                        texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateArrowSprite()
        {
            var texture = new Texture2D(64, 64);
            
            for (int x = 0; x < 64; x++)
            {
                for (int y = 0; y < 64; y++)
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
            
            for (int i = 0; i < 32; i++)
            {
                int width = 32 - i;
                for (int j = -width/2; j <= width/2; j++)
                {
                    if (32 + j >= 0 && 32 + j < 64 && i + 16 < 64)
                    {
                        texture.SetPixel(32 + j, i + 16, Color.white);
                    }
                }
            }
            
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }

        public void Setup(UIManager uiManager, BattleEntity[] entities)
        {
            _uiManager = uiManager;
            _allEntities = entities;
        }

        public void StartTargetSelection(
            EntityType caster, 
            TargetSide allowedSide,
            MoveCategory category,
            Action<EntityType> onSelected, 
            Action onCancelled)
        {
            Debug.Log($"[TargetSelection] Started - Caster: {caster}, AllowedSide: {allowedSide}, Category: {category}");
            
            _onTargetSelected = onSelected;
            _onCancelled = onCancelled;
            _isActive = true;

            FindValidTargets(caster, allowedSide, category);

            Debug.Log($"[TargetSelection] Found {_validTargetIndices.Count} valid targets");

            if (_validTargetIndices.Count == 0)
            {
                Debug.LogWarning("No valid targets found!");
                Cancel();
                return;
            }

            _currentTargetIndex = 0;
            ShowTargetIndicators();
            UpdateTargetHighlight();
            StartCoroutine(HandleTargetSelectionInput());
        }

        private void FindValidTargets(EntityType caster, TargetSide allowedSide, MoveCategory category)
        {
            _validTargetIndices.Clear();
            int casterIndex = (int)caster;
            
            if (category == MoveCategory.AOE)
            {
                _onTargetSelected?.Invoke(EntityType.Boss);
                return;
            }
            
            for (int i = 0; i < _allEntities.Length; i++)
            {
                var entity = _allEntities[i];
                if (entity == null || entity.CurrentHP <= 0) continue;
                
                bool isValidTarget = false;
                
                switch (allowedSide)
                {
                    case TargetSide.Self:
                        isValidTarget = (i == casterIndex);
                        break;
                        
                    case TargetSide.Ally:
                    case TargetSide.Allies:
                        if (casterIndex < 4)
                            isValidTarget = (i < 4 && i != casterIndex);
                        else
                            isValidTarget = (i == casterIndex);
                        break;
                        
                    case TargetSide.Enemy:
                    case TargetSide.Enemies:
                        if (casterIndex < 4)
                            isValidTarget = (i == 4);
                        else
                            isValidTarget = (i < 4);
                        break;
                        
                    case TargetSide.Any:
                        isValidTarget = true;
                        break;
                }
                
                if (isValidTarget)
                {
                    _validTargetIndices.Add(i);
                }
            }
        }

        private void ShowTargetIndicators()
        {
            Debug.Log($"[TargetSelection] ShowTargetIndicators - Valid targets: {_validTargetIndices.Count}");
            ClearIndicators();
            
            foreach (int index in _validTargetIndices)
            {
                if (index < 0 || index >= _allEntities.Length)
                {
                    Debug.LogError($"Invalid target index: {index}");
                    continue;
                }
                
                var entity = _allEntities[index];
                if (entity == null) 
                {
                    Debug.LogError($"Entity at index {index} is null!");
                    continue;
                }
                
                var indicator = Instantiate(_targetIndicatorPrefab, transform);
                if (indicator == null)
                {
                    Debug.LogError("Failed to instantiate indicator!");
                    continue;
                }
                
                indicator.SetActive(true);
                
                var rectTransform = indicator.GetComponent<RectTransform>();
                rectTransform.localScale = Vector3.one * _indicatorScale;
                
                var button = indicator.GetComponent<Button>();
                if (button != null)
                {
                    int capturedIndex = index;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => OnIndicatorClicked(capturedIndex));
                    
                    var eventTrigger = indicator.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                    
                    var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
                    pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
                    pointerEnter.callback.AddListener((data) => { OnIndicatorHover(capturedIndex, true); });
                    eventTrigger.triggers.Add(pointerEnter);
                    
                    var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
                    pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
                    pointerExit.callback.AddListener((data) => { OnIndicatorHover(capturedIndex, false); });
                    eventTrigger.triggers.Add(pointerExit);
                }
                
                UpdateIndicatorPosition(indicator, entity);
                
                _targetIndicators[index] = indicator;
                _targetButtons[index] = button;
            }
        }

        private void OnIndicatorClicked(int entityIndex)
        {
            if (!_isActive) return;
            
            Debug.Log($"[TargetSelection] Indicator clicked for entity index: {entityIndex}");
            
            int validIndex = _validTargetIndices.IndexOf(entityIndex);
            if (validIndex >= 0)
            {
                _currentTargetIndex = validIndex;
                ConfirmTarget();
            }
        }

        private void OnIndicatorHover(int entityIndex, bool isHovering)
        {
            if (!_isActive) return;
            if (!_targetIndicators.ContainsKey(entityIndex)) return;
            
            var indicator = _targetIndicators[entityIndex];
            if (indicator.TryGetComponent<Image>(out var image))
            {
                if (entityIndex != _validTargetIndices[_currentTargetIndex])
                {
                    image.color = isHovering ? _hoverTargetColor : _validTargetColor;
                }
            }
        }

        private void UpdateIndicatorPosition(GameObject indicator, BattleEntity entity)
        {
            if (entity == null || indicator == null) return;
            
            Vector3 worldPos = entity.transform.position + Vector3.up * 2f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            
            if (screenPos.z < 0)
            {
                indicator.SetActive(false);
                return;
            }
            
            indicator.SetActive(true);
            
            if (_parentCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRectTransform,
                    screenPos,
                    _parentCanvas.worldCamera,
                    out localPoint
                );
                
                indicator.GetComponent<RectTransform>().anchoredPosition = localPoint;
            }
            else if (_parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                indicator.GetComponent<RectTransform>().position = screenPos;
            }
        }

        private void UpdateTargetHighlight()
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
            }
            
            foreach (var kvp in _targetIndicators)
            {
                if (kvp.Value.TryGetComponent<Image>(out var image))
                {
                    bool isSelected = (kvp.Key == _validTargetIndices[_currentTargetIndex]);
                    image.color = isSelected ? _selectedTargetColor : _validTargetColor;
                    
                    var canvasGroup = kvp.Value.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = isSelected ? 1f : 0.7f;
                    }
                    
                    if (isSelected)
                    {
                        _pulseCoroutine = StartCoroutine(PulseIndicator(kvp.Value));
                    }
                }
            }
        }

        private IEnumerator PulseIndicator(GameObject indicator)
        {
            var rectTransform = indicator.GetComponent<RectTransform>();
            float baseScale = _indicatorScale;
            
            while (_isActive && indicator != null)
            {
                float scale = baseScale + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmount;
                rectTransform.localScale = Vector3.one * scale;
                
                var arrow = indicator.transform.Find("Arrow");
                if (arrow != null)
                {
                    float bounce = Mathf.Sin(Time.time * _pulseSpeed * 2) * 10f;
                    arrow.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -70 + bounce);
                }
                
                yield return null;
            }
        }

        private IEnumerator HandleTargetSelectionInput()
        {
            while (_isActive)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                {
                    SelectPreviousTarget();
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    SelectNextTarget();
                }
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    SelectPreviousTarget();
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    SelectNextTarget();
                }
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    ConfirmTarget();
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Cancel();
                }
                
                yield return null;
            }
        }

        private void SelectNextTarget()
        {
            if (_validTargetIndices.Count <= 1) return;
            
            _currentTargetIndex = (_currentTargetIndex + 1) % _validTargetIndices.Count;
            UpdateTargetHighlight();
        }

        private void SelectPreviousTarget()
        {
            if (_validTargetIndices.Count <= 1) return;
            
            _currentTargetIndex--;
            if (_currentTargetIndex < 0) _currentTargetIndex = _validTargetIndices.Count - 1;
            UpdateTargetHighlight();
        }

        private void ConfirmTarget()
        {
            if (_validTargetIndices.Count > 0 && _currentTargetIndex < _validTargetIndices.Count)
            {
                int selectedEntityIndex = _validTargetIndices[_currentTargetIndex];
                EntityType selectedTarget = (EntityType)selectedEntityIndex;
                
                Debug.Log($"[TargetSelection] Target confirmed: {selectedTarget}");
                
                _isActive = false;
                ClearIndicators();
                
                _onTargetSelected?.Invoke(selectedTarget);
            }
        }

        private void Cancel()
        {
            Debug.Log("[TargetSelection] Selection cancelled");
            _isActive = false;
            ClearIndicators();
            _onCancelled?.Invoke();
        }

        private void ClearIndicators()
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }
            
            foreach (var indicator in _targetIndicators.Values)
            {
                if (indicator != null)
                    Destroy(indicator);
            }
            _targetIndicators.Clear();
            _targetButtons.Clear();
        }

        private void Update()
        {
            if (_isActive)
            {
                foreach (var kvp in _targetIndicators)
                {
                    if (kvp.Key < _allEntities.Length && _allEntities[kvp.Key] != null)
                    {
                        UpdateIndicatorPosition(kvp.Value, _allEntities[kvp.Key]);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            ClearIndicators();
            if (_targetIndicatorPrefab != null && _targetIndicatorPrefab.transform.parent == transform)
            {
                Destroy(_targetIndicatorPrefab);
            }
        }
    }
}