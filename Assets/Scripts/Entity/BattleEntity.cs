using System;
using GameSystem;
using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;

namespace Entity
{
    /// <summary>
    /// Represents an entity participating in a battle.
    /// This class manages the entity's stats, moves, and battle-related actions.
    /// </summary>
    public class BattleEntity : MonoBehaviour
    {
        [Header("Basic Information")]
        public string EntityName;
        public ElementType ElementType;

        private SpriteRenderer _spriteRenderer;
        public SpriteRenderer SpriteRenderer => _spriteRenderer;
    
        [Header("Stats")]
        public int MaxHP = 100;
        public int CurrentHP;
        public int Attack = 50;
        public int Defense = 50;

        [Header("Moves")] 
        public List<MoveData> MoveSet = new List<MoveData>();

        private MoveInstance[] _moveInstances;

        public ReadOnlySpan<MoveInstance> MoveInstances => _moveInstances;
        
        public bool IsStunned { get; private set; }
        public int StunTurnsLeft { get; private set; }

        private bool _isCharging = false;
        public bool IsCharging => _isCharging;

        private bool _isGuarding = false;
        public bool IsGuarding => _isGuarding;

        private int _chargingMoveIndex;
        public int ChargingMoveIndex => _chargingMoveIndex;

        private EntityType _chargingMoveTarget;
        public EntityType ChargingMoveTarget => _chargingMoveTarget;
        
        public float AtkBuffMultiplier = 1f;
        public float DefBuffMultiplier = 1f;
        
        protected virtual void Start()
        {
            Debug.Log($"=== [BattleEntity] Start - {EntityName} ===");
            Initialize();
        }
        
        public void Initialize()
        {
            Debug.Log($"=== [BattleEntity] Initialize BEGIN - {EntityName} ===");
            CurrentHP = MaxHP;

            _moveInstances = MoveSet
                .Select(so => new MoveInstance(so))
                .ToArray();

            // GetComponent가 아닌 GetComponentInChildren 사용!
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    
            // 디버그 로그 추가
            Debug.Log($"[{EntityName}] SpriteRenderer found: {_spriteRenderer != null}");
    
            if (_spriteRenderer && this is not BossEntity)
            {
                var newColor = _spriteRenderer.color;
                newColor.a = 0.1f;
                _spriteRenderer.color = newColor;
            }
    
            // BossEntity인 경우 스프라이트 확인
            if (_spriteRenderer && this is BossEntity)
            {
                Debug.Log($"[Boss] Sprite assigned: {_spriteRenderer.sprite != null}");
            }
            Debug.Log($"=== [BattleEntity] Initialize END - SpriteRenderer: {_spriteRenderer != null}, Sprite: {_spriteRenderer?.sprite != null} ===");
        }
        
        public ref MoveInstance GetMoveInstance(int index)
            => ref _moveInstances[index];
        
        protected float GetDefenseFactor() => 100f / (100f + Defense); // Defense formula: 100 / (100 + Defense)
        
        protected virtual float GetWeaknessFactor(ElementType moveType) => 1f; // Player character doesn't have weakness type
        
        public MoveData GetMoveData(int moveIndex)
        {
            if (moveIndex < 0 || moveIndex >= MoveSet.Count)
            {
                return null;
            }
            
            return MoveSet[moveIndex];
        }

        public virtual float PreviewMitigate(float rawDamage, MoveData move)
        {
            float damage = rawDamage * GetDefenseFactor() * GetWeaknessFactor(move.Type);
            return damage;
        }
        
        public virtual void TakeDamage(ElementType moveType, int damage)
        {
            int finalDamage = Mathf.RoundToInt(damage * GetDefenseFactor() * GetWeaknessFactor(moveType));
            finalDamage = Mathf.Max(finalDamage, 1);

            CurrentHP -= finalDamage;
            CurrentHP = Mathf.Clamp(CurrentHP, 0, MaxHP);
        }

        public void ApplyStun(int turns)
        {
            IsStunned = true;
            StunTurnsLeft = turns;
        }
        
        public void SetGuardState(bool guarding)
        {
            _isGuarding = guarding;
    
            if (guarding)
            {
                DefBuffMultiplier *= 2f;
            }
            else
            {
                DefBuffMultiplier /= 2f;
            }
        }

        public void ReduceStunDuration()
        {
            if (StunTurnsLeft > 0)
            {
                StunTurnsLeft--;
                if (StunTurnsLeft <= 0) IsStunned = false;
            }
        }

        public void ClearBuffandDebuffs()
        {
            AtkBuffMultiplier = 1f;
            DefBuffMultiplier = 1f;
        }

        public void SetChargingState(bool newState, int moveIndex = -1, EntityType target = EntityType.Boss)
        {
            _isCharging = newState;
            _chargingMoveIndex = moveIndex;
            _chargingMoveTarget = target;
        }
    }
}
