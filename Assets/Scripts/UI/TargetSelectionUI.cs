using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Data;
using Entity;
using GameSystem;

namespace UI
{
    public class TargetSelectionUI : MonoBehaviour
    {
        // -------------------------------------------------
        // 기존 필드들 (일부만 남기고, UI 관련은 더미 처리)
        // -------------------------------------------------
        
        private UIManager _uiManager;
        private BattleEntity[] _allEntities;
        private List<int> _validTargetIndices = new List<int>();
        private int _currentTargetIndex = 0;

        // 기존에 사용하던 Indicators 관련은 더미(혹은 제거)
        // (외부 스크립트가 참고할 가능성이 없다면 과감히 제거해도 됨)
        
        private Action<EntityType> _onTargetSelected;
        private Action _onCancelled;

        private bool _isActive = false;

        // 하이라이팅용
        // (현재 마우스 오버 중인 BattleEntity)
        private BattleEntity _currentHoverEntity = null;

        // -------------------------------------------------
        // Setup, StartTargetSelection 등 기존 메서드
        // -------------------------------------------------
        
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

            // 유효 타겟 찾기
            FindValidTargets(caster, allowedSide, category);

            Debug.Log($"[TargetSelection] Found {_validTargetIndices.Count} valid targets");
            if (_validTargetIndices.Count == 0)
            {
                Debug.LogWarning("No valid targets found!");
                Cancel();
            }
            else
            {
                _currentTargetIndex = 0;
                // 기존 코드에서 Indicators 띄우던 부분은 주석/더미 처리
                // ShowTargetIndicators(); (더미)
                // UpdateTargetHighlight(); (더미)

                // 굳이 Coroutine을 쓰지 않고 Update에서 처리해도 됨
                // StartCoroutine(HandleTargetSelectionInput()); (더미 or 제거)
            }
        }

        private void FindValidTargets(EntityType caster, TargetSide allowedSide, MoveCategory category)
        {
            _validTargetIndices.Clear();
            int casterIndex = (int)caster;

            if (category == MoveCategory.AOE)
            {
                // 자동 선택 처리
                _onTargetSelected?.Invoke(EntityType.Boss);
                _isActive = false;
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

        // -------------------------------------------------
        // 하이라이팅 & 클릭 (새 로직)
        // -------------------------------------------------

        private void Update()
        {
            if (!_isActive) return;

            // 1) 마우스 입력: ESC => Cancel
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            // 2) 레이캐스트로 마우스가 어떤 BattleEntity 위에 있는지 체크
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            // 2D 게임이라면 Physics2D.GetRayIntersection(ray), 3D라면 Physics.Raycast
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
            BattleEntity hoverEntity = null;

            if (hit.collider != null)
            {
                // collider에 BattleEntity가 붙어 있는지 확인
                hoverEntity = hit.collider.GetComponent<BattleEntity>();
                if (hoverEntity != null)
                {
                    // 유효 타겟인지 검사
                    int idx = System.Array.IndexOf(_allEntities, hoverEntity);
                    if (idx < 0 || !_validTargetIndices.Contains(idx))
                    {
                        hoverEntity = null; // 유효 타겟 아니면 null 처리
                    }
                }
            }

            // 3) 하이라이트 대상이 바뀌었으면 업데이트
            if (_currentHoverEntity != hoverEntity)
            {
                ClearHighlight(_currentHoverEntity);
                SetHighlight(hoverEntity);
                _currentHoverEntity = hoverEntity;
            }

            // 4) 왼클릭 => ConfirmTarget
            if (Input.GetMouseButtonDown(0) && hoverEntity != null)
            {
                int entityIndex = System.Array.IndexOf(_allEntities, hoverEntity);
                ConfirmTarget(entityIndex);
            }
        }

        private void SetHighlight(BattleEntity entity)
        {
            if (entity == null) return;
            // BattleEntity 하위(자식) 중 SpriteOutlineToggle을 찾아서 활성
            var outlineToggle = entity.GetComponentInChildren<SpriteOutlineToggle>();
            if (outlineToggle != null)
            {
                Debug.LogWarning("asdfasdfadsfadsfadsasfas");
                outlineToggle.SetOutline(true);
            }
        }

        private void ClearHighlight(BattleEntity entity)
        {
            if (entity == null) return;
            var outlineToggle = entity.GetComponentInChildren<SpriteOutlineToggle>();
            if (outlineToggle != null)
            {
                outlineToggle.SetOutline(false);
            }
        }

        // -------------------------------------------------
        // ConfirmTarget & Cancel (기존 메서드 유지)
        // -------------------------------------------------

        private void ConfirmTarget(int entityIndex)
        {
            if (!_validTargetIndices.Contains(entityIndex)) return;

            Debug.Log($"[TargetSelection] Target confirmed: {entityIndex}");

            _isActive = false;

            // 하이라이트 초기화
            ClearHighlight(_currentHoverEntity);
            _currentHoverEntity = null;

            // 기존 코드: ClearIndicators 등
            ClearIndicators(); // 더미

            _onTargetSelected?.Invoke((EntityType)entityIndex);
        }

        private void Cancel()
        {
            Debug.Log("[TargetSelection] Selection cancelled");
            _isActive = false;

            // 하이라이트 해제
            ClearHighlight(_currentHoverEntity);
            _currentHoverEntity = null;

            ClearIndicators(); // 더미
            _onCancelled?.Invoke();
        }

        // -------------------------------------------------
        // 기존 UI Indicator 메서드를 더미 처리 (호환성)
        // -------------------------------------------------

        private void ClearIndicators() { /* Do nothing */ }

        // 아래는 전부 빈 메서드 or 더미
        // 혹시 외부에서 호출할 일이 없으면 전부 제거해도 되지만,
        // "기존 코드가 호출할 수도 있다"면 빈 메서드로 남겨둡니다.
        
        private void ShowTargetIndicators() { }
        private void UpdateTargetHighlight() { }
        private void SelectNextTarget() { }
        private void SelectPreviousTarget() { }

        // 만약 Coroutine을 쓰지 않는다면 HandleTargetSelectionInput()도 더미 처리
        private IEnumerator HandleTargetSelectionInput()
        {
            yield break;
        }
    }
}
