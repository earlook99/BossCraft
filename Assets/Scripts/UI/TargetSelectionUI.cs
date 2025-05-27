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
    /// <summary>
    /// 스킬 사용 시 타겟을 선택하는 UI 시스템
    /// </summary>
    public class TargetSelectionUI : MonoBehaviour
    {
        [Header("Visual Feedback")]
        [SerializeField] private GameObject _targetIndicatorPrefab; // 타겟 가능한 엔티티 위에 표시할 인디케이터
        [SerializeField] private Color _validTargetColor = Color.green;
        [SerializeField] private Color _selectedTargetColor = Color.yellow;
        
        private UIManager _uiManager;
        private BattleEntity[] _allEntities;
        private List<int> _validTargetIndices = new List<int>();
        private int _currentTargetIndex = 0;
        private Dictionary<int, GameObject> _targetIndicators = new Dictionary<int, GameObject>();
        
        // 타겟 선택 완료 시 호출될 콜백
        private Action<EntityType> _onTargetSelected;
        private Action _onCancelled;
        
        private bool _isActive = false;

        private void Awake()
        {
            // 필수 컴포넌트 체크
            if (Camera.main == null)
            {
                Debug.LogError("Main Camera not found! Please tag your main camera as 'MainCamera'");
            }
    
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("TargetSelectionUI must be child of a Canvas!");
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            {
                Debug.LogError("Canvas is in Screen Space - Camera mode but no camera assigned!");
            }
        }

        private void CreateDefaultIndicatorPrefab()
        {
            // 임시 GameObject 생성
            var tempGO = new GameObject("TempIndicator");
            tempGO.transform.SetParent(transform, false);
            
            // RectTransform 추가
            var rectTransform = tempGO.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(60, 60);
            
            // Image 컴포넌트 추가
            var image = tempGO.AddComponent<Image>();
            
            // 간단한 원형 스프라이트 생성
            var texture = new Texture2D(64, 64);
            var center = new Vector2(32, 32);
            for (int x = 0; x < 64; x++)
            {
                for (int y = 0; y < 64; y++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance < 30 && distance > 25)
                    {
                        texture.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }
            texture.Apply();
            
            image.sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            image.color = _validTargetColor;
            
            // 프리팹으로 저장
            tempGO.SetActive(false);
            _targetIndicatorPrefab = tempGO;
        }

        public void Setup(UIManager uiManager, BattleEntity[] entities)
        {
            _uiManager = uiManager;
            _allEntities = entities;
        }

        /// <summary>
        /// 타겟 선택 모드를 시작합니다
        /// </summary>
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
    
            // 유효한 타겟 찾기
            FindValidTargets(caster, allowedSide, category);
    
            Debug.Log($"[TargetSelection] Found {_validTargetIndices.Count} valid targets");
    
            if (_validTargetIndices.Count == 0)
            {
                Debug.LogWarning("No valid targets found!");
                Cancel();
                return;
            }
    
            // 첫 번째 유효한 타겟 선택
            _currentTargetIndex = 0;
    
            // 타겟 인디케이터 표시
            ShowTargetIndicators();
            UpdateTargetHighlight();
    
            StartCoroutine(HandleTargetSelectionInput());
        }

        /// <summary>
        /// 스킬의 조건에 맞는 유효한 타겟을 찾습니다
        /// </summary>
        private void FindValidTargets(EntityType caster, TargetSide allowedSide, MoveCategory category)
        {
            _validTargetIndices.Clear();
            int casterIndex = (int)caster;
            
            // AOE는 타겟 선택이 필요 없음 (자동으로 전체 적용)
            if (category == MoveCategory.AOE)
            {
                // AOE는 타겟 선택 스킵하고 바로 실행
                _onTargetSelected?.Invoke(EntityType.Boss); // 더미 값
                return;
            }
            
            // Single 또는 MultiRandom의 경우
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
                        // 플레이어끼리는 아군, 보스는 자기만 아군
                        if (casterIndex < 4) // 플레이어가 시전자
                            isValidTarget = (i < 4 && i != casterIndex);
                        else // 보스가 시전자
                            isValidTarget = (i == casterIndex);
                        break;
                        
                    case TargetSide.Enemy:
                    case TargetSide.Enemies:
                        // 플레이어는 보스가 적, 보스는 플레이어들이 적
                        if (casterIndex < 4) // 플레이어가 시전자
                            isValidTarget = (i == 4);
                        else // 보스가 시전자
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

        /// <summary>
        /// 타겟 인디케이터를 표시합니다
        /// </summary>
        private void ShowTargetIndicators()
        {
            Debug.Log($"[TargetSelection] ShowTargetIndicators - Valid targets: {_validTargetIndices.Count}");
            ClearIndicators();
    
            // 프리팹 체크
            if (_targetIndicatorPrefab == null)
            {
                Debug.LogWarning("Target Indicator Prefab is null! Creating default...");
                CreateDefaultIndicatorPrefab();
            }
    
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
        
                // 인디케이터 생성
                var indicator = Instantiate(_targetIndicatorPrefab, transform);
                if (indicator == null)
                {
                    Debug.LogError("Failed to instantiate indicator!");
                    continue;
                }
        
                indicator.SetActive(true);
        
                // RectTransform 확인
                if (!indicator.TryGetComponent<RectTransform>(out var rectTransform))
                {
                    rectTransform = indicator.AddComponent<RectTransform>();
                }
        
                UpdateIndicatorPosition(indicator, entity);
                _targetIndicators[index] = indicator;
            }
        }

        /// <summary>
        /// 인디케이터 위치를 업데이트합니다
        /// </summary>
        private void UpdateIndicatorPosition(GameObject indicator, BattleEntity entity)
        {
            if (entity == null || indicator == null) 
            {
                Debug.LogError("Entity or Indicator is null!");
                return;
            }
    
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("Canvas not found!");
                return;
            }
    
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("Main Camera not found!");
                return;
            }
    
            Camera uiCamera = canvas.worldCamera;
            if (uiCamera == null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                Debug.LogError("UI Camera not assigned to Canvas!");
                return;
            }
    
            RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
            if (indicatorRect == null)
            {
                Debug.LogError("Indicator doesn't have RectTransform!");
                return;
            }
    
            // 이제 안전하게 위치 계산
            Vector3 worldPos = entity.transform.position + Vector3.up * 2f;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
    
            if (screenPos.z < 0)
            {
                indicator.SetActive(false);
                return;
            }
    
            indicator.SetActive(true);
    
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                screenPos,
                uiCamera,
                out localPoint
            );
    
            indicatorRect.anchoredPosition = localPoint;
        }

        /// <summary>
        /// 현재 선택된 타겟을 하이라이트합니다
        /// </summary>
        private void UpdateTargetHighlight()
        {
            foreach (var kvp in _targetIndicators)
            {
                if (kvp.Value.TryGetComponent<Image>(out var image))
                {
                    image.color = (kvp.Key == _validTargetIndices[_currentTargetIndex]) 
                        ? _selectedTargetColor 
                        : _validTargetColor;
                }
            }
        }

        /// <summary>
        /// 타겟 선택 입력을 처리합니다
        /// </summary>
        private IEnumerator HandleTargetSelectionInput()
        {
            while (_isActive)
            {
                // 좌우 화살표로 타겟 변경
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                {
                    Debug.Log("[TargetSelection] Left arrow pressed");
                    SelectPreviousTarget();
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    SelectNextTarget();
                }
                // 위아래 화살표로도 타겟 변경 (플레이어 파티의 경우)
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    SelectPreviousTarget();
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    SelectNextTarget();
                }
                // Enter 또는 Space로 확정
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    ConfirmTarget();
                }
                // ESC로 취소
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Cancel();
                }
                
                // 마우스 클릭 처리
                if (Input.GetMouseButtonDown(0))
                {
                    HandleMouseClick();
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

        private void HandleMouseClick()
        {
            // 마우스 위치에서 레이캐스트
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // 클릭한 오브젝트가 유효한 타겟인지 확인
                var entity = hit.collider.GetComponent<BattleEntity>();
                if (entity != null)
                {
                    int entityIndex = System.Array.IndexOf(_allEntities, entity);
                    int validIndex = _validTargetIndices.IndexOf(entityIndex);
                    
                    if (validIndex >= 0)
                    {
                        _currentTargetIndex = validIndex;
                        ConfirmTarget();
                    }
                }
            }
        }

        private void ConfirmTarget()
        {
            if (_validTargetIndices.Count > 0 && _currentTargetIndex < _validTargetIndices.Count)
            {
                int selectedEntityIndex = _validTargetIndices[_currentTargetIndex];
                EntityType selectedTarget = (EntityType)selectedEntityIndex;
                
                _isActive = false;
                ClearIndicators();
                
                _onTargetSelected?.Invoke(selectedTarget);
            }
        }

        private void Cancel()
        {
            _isActive = false;
            ClearIndicators();
            _onCancelled?.Invoke();
        }

        private void ClearIndicators()
        {
            foreach (var indicator in _targetIndicators.Values)
            {
                if (indicator != null)
                    Destroy(indicator);
            }
            _targetIndicators.Clear();
        }

        private void Update()
        {
            // 인디케이터 위치 지속적으로 업데이트 (카메라 이동 대응)
            if (_isActive)
            {
                foreach (var kvp in _targetIndicators)
                {
                    if (kvp.Key < _allEntities.Length)
                    {
                        UpdateIndicatorPosition(kvp.Value, _allEntities[kvp.Key]);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            ClearIndicators();
            if (_targetIndicatorPrefab != null && _targetIndicatorPrefab.transform.parent == null)
            {
                Destroy(_targetIndicatorPrefab);
            }
        }
    }
}