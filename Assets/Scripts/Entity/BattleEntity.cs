using System;
using GameSystem;
using GameSystem.Interfaces;
using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;

namespace Entity
{
    public class BattleEntity : MonoBehaviour, IBattleEntity
    {
        [Header("Basic Information")]
        public string EntityName;
        public ElementType ElementType;

        [Header("Stats")]
        [SerializeField] private int _maxHP = 100;
        [SerializeField] private int _currentHP;
        [SerializeField] private int _attack = 50;
        [SerializeField] private int _defense = 50;
        
        // Public properties for backward compatibility
        public int MaxHP 
        { 
            get => _maxHP; 
            set => _maxHP = value; 
        }
        
        public int CurrentHP 
        { 
            get => _currentHP; 
            set => _currentHP = Mathf.Clamp(value, 0, _maxHP); 
        }
        
        public int Attack 
        { 
            get => _attack; 
            set => _attack = value; 
        }
        
        public int Defense 
        { 
            get => _defense; 
            set => _defense = value; 
        }

        [Header("Moves")] 
        public List<MoveData> MoveSet = new List<MoveData>();

        private SpriteRenderer _spriteRenderer;
        private MoveInstance[] _moveInstances;
        
        private bool _isCharging = false;
        private bool _isGuarding = false;
        private int _chargingMoveIndex;
        private EntityType _chargingMoveTarget;
        
        private const float DEFAULT_SPRITE_ALPHA = 0.1f;
        private const float GUARD_DEFENSE_MULTIPLIER = 2f;
        private const int DEFENSE_FORMULA_BASE = 100;

        public SpriteRenderer SpriteRenderer => _spriteRenderer;
        public ReadOnlySpan<MoveInstance> MoveInstances => _moveInstances;
        public bool IsStunned { get; private set; }
        public int StunTurnsLeft { get; private set; }
        public bool IsCharging => _isCharging;
        public bool IsGuarding => _isGuarding;
        public int ChargingMoveIndex => _chargingMoveIndex;
        public EntityType ChargingMoveTarget => _chargingMoveTarget;
        public float AtkBuffMultiplier { get; set; } = 1f;
        public float DefBuffMultiplier { get; set; } = 1f;
        
        // IBattleEntity Implementation
        public string Name => EntityName;
        public bool IsAlive => _currentHP > 0;
        public bool CanBeTargeted => IsAlive && !IsStunned;
        public EntityType EntityType => (EntityType)Array.IndexOf(GameObject.FindObjectsOfType<BattleEntity>(), this);
        public float AttackMultiplier => AtkBuffMultiplier;
        public float DefenseMultiplier => DefBuffMultiplier;
        public bool CanBeHealed => IsAlive && _currentHP < _maxHP;
        
        // Explicit interface properties (already defined above as public properties)
        int GameSystem.Interfaces.IBattleEntity.Attack => _attack;
        int GameSystem.Interfaces.IBattleEntity.Defense => _defense;
        int GameSystem.Interfaces.IDamageable.CurrentHP => _currentHP;
        int GameSystem.Interfaces.IDamageable.MaxHP => _maxHP;
        
        protected virtual void Start()
        {
            Initialize();
        }
        
        public void Initialize()
        {
            _currentHP = _maxHP;
            _moveInstances = MoveSet.Select(moveData => new MoveInstance(moveData)).ToArray();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    
            if (_spriteRenderer != null && this is not BossEntity)
            {
                var color = _spriteRenderer.color;
                color.a = GameConstants.UI.INACTIVE_SPRITE_ALPHA;
                _spriteRenderer.color = color;
            }
        }
        
        public ref MoveInstance GetMoveInstance(int index)
            => ref _moveInstances[index];
        
        protected float GetDefenseFactor() 
            => GameConstants.Battle.DEFENSE_FORMULA_BASE / (GameConstants.Battle.DEFENSE_FORMULA_BASE + _defense);
        
        protected virtual float GetWeaknessFactor(ElementType moveType) => 1f;
        
        public MoveData GetMoveData(int moveIndex)
        {
            if (moveIndex < 0 || moveIndex >= MoveSet.Count)
                return null;
            
            return MoveSet[moveIndex];
        }

        public virtual float PreviewMitigate(float rawDamage, MoveData move)
        {
            return rawDamage * GetDefenseFactor() * GetWeaknessFactor(move.Type);
        }
        
        public virtual void TakeDamage(ElementType moveType, int damage)
        {
            int finalDamage = Mathf.RoundToInt(damage * GetDefenseFactor() * GetWeaknessFactor(moveType));
            finalDamage = Mathf.Max(finalDamage, 1);

            CurrentHP = Mathf.Clamp(CurrentHP - finalDamage, 0, MaxHP);
        }
        
        public float GetDamageMultiplier(ElementType damageType)
        {
            return GetDefenseFactor() * GetWeaknessFactor(damageType);
        }
        
        public void Heal(int amount)
        {
            _currentHP = Mathf.Min(_currentHP + amount, _maxHP);
        }
        
        public void ApplyBuff(BuffsType buffType, float multiplier)
        {
            switch (buffType)
            {
                case BuffsType.Attack:
                    AtkBuffMultiplier = Mathf.Clamp(AtkBuffMultiplier + multiplier, 
                        GameConstants.Battle.MIN_STAT_MULTIPLIER, 
                        GameConstants.Battle.MAX_STAT_MULTIPLIER);
                    break;
                case BuffsType.Defense:
                    DefBuffMultiplier = Mathf.Clamp(DefBuffMultiplier + multiplier, 
                        GameConstants.Battle.MIN_STAT_MULTIPLIER, 
                        GameConstants.Battle.MAX_STAT_MULTIPLIER);
                    break;
            }
        }
        
        public void ApplyDebuff(BuffsType buffType, float multiplier)
        {
            ApplyBuff(buffType, -multiplier);
        }
        
        public void ApplyStatusEffect(MoveEffectType effectType, int duration)
        {
            switch (effectType)
            {
                case MoveEffectType.Stun:
                    ApplyStun(duration);
                    break;
            }
        }
        
        public void ClearBuffs()
        {
            AtkBuffMultiplier = 1f;
            DefBuffMultiplier = 1f;
        }
        
        public void ClearDebuffs()
        {
            ClearBuffs();
        }
        
        public void StartTurn()
        {
            if (IsStunned)
            {
                ReduceStunDuration();
            }
        }
        
        public void EndTurn()
        {
            // Update cooldowns
            for (int i = 0; i < _moveInstances.Length; i++)
            {
                if (_moveInstances[i].CooldownLeft > 0)
                {
                    _moveInstances[i].CooldownLeft--;
                }
            }
        }
        
        public bool CanTakeTurn()
        {
            return IsAlive && !IsStunned;
        }

        public void ApplyStun(int turns)
        {
            IsStunned = true;
            StunTurnsLeft = turns;
        }
        
        public void SetGuardState(bool guarding)
        {
            _isGuarding = guarding;
            DefBuffMultiplier = guarding 
                ? DefBuffMultiplier * GameConstants.Battle.GUARD_DEFENSE_MULTIPLIER 
                : DefBuffMultiplier / GameConstants.Battle.GUARD_DEFENSE_MULTIPLIER;
        }

        public void ReduceStunDuration()
        {
            if (StunTurnsLeft > 0)
            {
                StunTurnsLeft--;
                if (StunTurnsLeft <= 0) 
                    IsStunned = false;
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