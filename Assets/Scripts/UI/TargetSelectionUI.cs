using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Data;
using Entity;
using GameSystem;
using GameSystem.UI;

namespace UI
{
    public class TargetSelectionUI : MonoBehaviour
    {
        private BattleUIController _uiController;
        private BattleEntity[] _allEntities;
        private List<int> _validTargetIndices = new List<int>();
        private int _currentTargetIndex = 0;

        private Action<EntityType> _onTargetSelected;
        private Action _onCancelled;

        private bool _isActive = false;
        private BattleEntity _currentHoverEntity = null;
        
        private Camera _mainCamera;
        private Vector3 _lastMousePosition;
        private Dictionary<Collider2D, BattleEntity> _colliderToEntityCache = new Dictionary<Collider2D, BattleEntity>();
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject _targetingOverlay;
        [SerializeField] private Color _validTargetColor = new Color(0, 1, 0, 0.3f);
        [SerializeField] private Color _invalidTargetColor = new Color(1, 0, 0, 0.3f);

        private const int PLAYER_COUNT = 4;
        private const int BOSS_INDEX = 4;
        private const float MOUSE_MOVE_THRESHOLD = 0.1f;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        public void Setup(BattleUIController uiController, BattleEntity[] entities)
        {
            _uiController = uiController;
            _allEntities = entities;
            
            CacheEntityColliders();
        }

        private void CacheEntityColliders()
        {
            _colliderToEntityCache.Clear();
            
            foreach (var entity in _allEntities)
            {
                if (entity != null)
                {
                    var collider = entity.GetComponent<Collider2D>();
                    if (collider != null)
                    {
                        _colliderToEntityCache[collider] = entity;
                    }
                }
            }
        }

        public void StartTargetSelection(
            EntityType caster,
            TargetSide allowedSide,
            MoveCategory category,
            Action<EntityType> onSelected,
            Action onCancelled)
        {
            _onTargetSelected = onSelected;
            _onCancelled = onCancelled;
            _isActive = true;

            FindValidTargets(caster, allowedSide, category);

            if (_validTargetIndices.Count == 0)
            {
                Cancel();
            }
            else
            {
                _currentTargetIndex = 0;
                _lastMousePosition = Input.mousePosition;
                ShowTargetingMode(true);
                HighlightValidTargets();
            }
        }

        private void FindValidTargets(EntityType caster, TargetSide allowedSide, MoveCategory category)
        {
            _validTargetIndices.Clear();
            int casterIndex = (int)caster;

            if (category == MoveCategory.AOE)
            {
                _onTargetSelected?.Invoke(EntityType.Boss);
                _isActive = false;
                return;
            }

            for (int i = 0; i < _allEntities.Length; i++)
            {
                if (IsValidTarget(i, casterIndex, allowedSide))
                {
                    _validTargetIndices.Add(i);
                }
            }
        }

        private bool IsValidTarget(int targetIndex, int casterIndex, TargetSide allowedSide)
        {
            var entity = _allEntities[targetIndex];
            if (entity == null || entity.CurrentHP <= 0) return false;

            bool isPlayerCaster = casterIndex < PLAYER_COUNT;
            bool isPlayerTarget = targetIndex < PLAYER_COUNT;

            switch (allowedSide)
            {
                case TargetSide.Self:
                    return targetIndex == casterIndex;
                    
                case TargetSide.Ally:
                case TargetSide.Allies:
                    if (isPlayerCaster)
                        return isPlayerTarget && targetIndex != casterIndex;
                    else
                        return targetIndex == casterIndex;
                        
                case TargetSide.Enemy:
                case TargetSide.Enemies:
                    return isPlayerCaster ? !isPlayerTarget : isPlayerTarget;
                    
                case TargetSide.Any:
                    return true;
                    
                default:
                    return false;
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            Vector3 currentMousePos = Input.mousePosition;
            
            if (Vector3.Distance(currentMousePos, _lastMousePosition) > MOUSE_MOVE_THRESHOLD)
            {
                _lastMousePosition = currentMousePos;
                HandleMouseTargeting();
            }

            if (Input.GetMouseButtonDown(0))
            {
                HandleMouseClick();
            }
        }

        private void HandleMouseTargeting()
        {
            Ray ray = _mainCamera.ScreenPointToRay(_lastMousePosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
            BattleEntity hoverEntity = null;

            if (hit.collider != null && _colliderToEntityCache.TryGetValue(hit.collider, out BattleEntity entity))
            {
                if (IsValidHoverTarget(entity))
                {
                    hoverEntity = entity;
                }
            }

            if (_currentHoverEntity != hoverEntity)
            {
                UpdateHighlight(hoverEntity);
            }
        }
        
        private void HandleMouseClick()
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
            
            if (hit.collider == null)
            {
                Cancel();
                return;
            }
            
            if (_colliderToEntityCache.TryGetValue(hit.collider, out BattleEntity entity))
            {
                if (IsValidHoverTarget(entity))
                {
                    int entityIndex = System.Array.IndexOf(_allEntities, entity);
                    ConfirmTarget(entityIndex);
                }
                else
                {
                    Cancel();
                }
            }
            else
            {
                Cancel();
            }
        }

        private bool IsValidHoverTarget(BattleEntity entity)
        {
            int idx = System.Array.IndexOf(_allEntities, entity);
            return idx >= 0 && _validTargetIndices.Contains(idx);
        }

        private void UpdateHighlight(BattleEntity newHoverEntity)
        {
            if (_currentHoverEntity != newHoverEntity)
            {
                SetHighlight(_currentHoverEntity, false);
                SetHighlight(newHoverEntity, true);
                _currentHoverEntity = newHoverEntity;
            }
        }

        private void SetHighlight(BattleEntity entity, bool enabled)
        {
            if (entity == null) return;
            
            var outlineToggle = entity.GetComponentInChildren<SpriteOutlineToggle>();
            if (outlineToggle != null)
            {
                outlineToggle.SetOutline(enabled);
            }
        }

        private void ConfirmTarget(int entityIndex)
        {
            if (!_validTargetIndices.Contains(entityIndex)) return;

            _isActive = false;
            ClearHighlight();
            ShowTargetingMode(false);
            ClearValidTargetHighlights();
            _onTargetSelected?.Invoke((EntityType)entityIndex);
        }

        private void Cancel()
        {
            _isActive = false;
            ClearHighlight();
            ShowTargetingMode(false);
            ClearValidTargetHighlights();
            _onCancelled?.Invoke();
        }

        private void ClearHighlight()
        {
            SetHighlight(_currentHoverEntity, false);
            _currentHoverEntity = null;
        }
        
        private void ShowTargetingMode(bool show)
        {
            if (_targetingOverlay != null)
            {
                _targetingOverlay.SetActive(show);
            }
            
            SetCursorForTargeting(show);
        }
        
        private void SetCursorForTargeting(bool isTargeting)
        {
            Cursor.visible = true;
        }
        
        private void HighlightValidTargets()
        {
            foreach (int index in _validTargetIndices)
            {
                if (index < _allEntities.Length && _allEntities[index] != null)
                {
                    var spriteRenderer = _allEntities[index].SpriteRenderer;
                    if (spriteRenderer != null)
                    {
                        Color color = spriteRenderer.color;
                        color.a = 1f;
                        spriteRenderer.color = color;
                    }
                }
            }
            
            for (int i = 0; i < _allEntities.Length; i++)
            {
                if (!_validTargetIndices.Contains(i) && _allEntities[i] != null)
                {
                    var spriteRenderer = _allEntities[i].SpriteRenderer;
                    if (spriteRenderer != null)
                    {
                        Color color = spriteRenderer.color;
                        color.a = 0.3f;
                        spriteRenderer.color = color;
                    }
                }
            }
        }
        
        private void ClearValidTargetHighlights()
        {
            foreach (var entity in _allEntities)
            {
                if (entity != null && entity.SpriteRenderer != null)
                {
                    Color color = entity.SpriteRenderer.color;
                    color.a = entity is BossEntity ? 1f : GameConstants.UI.INACTIVE_SPRITE_ALPHA;
                    entity.SpriteRenderer.color = color;
                }
            }
        }
        
        private void OnDisable()
        {
            if (_isActive)
            {
                Cancel();
            }
        }
    }
}