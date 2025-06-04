// BattleEntity.cs 수정
using System;
using GameSystem;
using GameSystem.Interfaces;
using System.Collections.Generic;
using Data;
using UI;
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
        
        // 버프 스택 추가
        private int _attackBuffStacks = 0;
        private int _defenseBuffStacks = 0;
        private const int MAX_BUFF_STACKS = 2;
        private const float BUFF_PER_STACK = 0.2f; // 스택당 20% 증가
        
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
        private SpriteOutlineToggle _outlineToggle;
        private Collider2D _collider2D;
        private MoveInstance[] _moveInstances;
        
        private bool _isCharging = false;
        private bool _isGuarding = false;
        private int _chargingMoveIndex;
        private EntityType _chargingMoveTarget;
        
        private bool _isStealthed = false;
        private int _stealthTurnsLeft = 0;
        
        public bool IsStealthed => _isStealthed;
        public int StealthTurnsLeft => _stealthTurnsLeft;
        
        private bool _hasCounter = false;
        private int _counterTurnsLeft = 0;
        private bool _isTaunting = false;
        private int _tauntTurnsLeft = 0;
        
        public bool HasCounter => _hasCounter;
        public bool IsTaunting => _isTaunting;
        
        private const float DEFAULT_SPRITE_ALPHA = 0.1f;
        private const float GUARD_DEFENSE_MULTIPLIER = 2f;
        private const int DEFENSE_FORMULA_BASE = 100;
        private const float STEALTH_ALPHA = 0.3f;

        public SpriteRenderer SpriteRenderer => _spriteRenderer;
        public SpriteOutlineToggle OutlineToggle => _outlineToggle;
        public Collider2D EntityCollider => _collider2D;
        public ReadOnlySpan<MoveInstance> MoveInstances => _moveInstances;
        public bool IsStunned { get; private set; }
        public int StunTurnsLeft { get; private set; }
        public bool IsCharging => _isCharging;
        public bool IsGuarding => _isGuarding;
        public int ChargingMoveIndex => _chargingMoveIndex;
        public EntityType ChargingMoveTarget => _chargingMoveTarget;
        public bool HasBuffs => _attackBuffStacks > 0 || _defenseBuffStacks > 0;
        
        private float _atkBuffMultiplier = 1f;
        public float AtkBuffMultiplier 
        { 
            get => _atkBuffMultiplier * (1f + (_attackBuffStacks * BUFF_PER_STACK));
            set => _atkBuffMultiplier = value;
        }
        public float DefBuffMultiplier { get; set; } = 1f;
        
        public int AttackBuffStacks => _attackBuffStacks;
        public int DefenseBuffStacks => _defenseBuffStacks;
        
        public string Name => EntityName;
        public bool IsAlive => _currentHP > 0;
        public bool CanBeTargeted => IsAlive && !IsStunned;
        public EntityType EntityType => (EntityType)Array.IndexOf(BattleContext.Instance?.Entities ?? Array.Empty<BattleEntity>(), this);
        public float AttackMultiplier => AtkBuffMultiplier;
        public float DefenseMultiplier => DefBuffMultiplier * (1f + (_defenseBuffStacks * BUFF_PER_STACK));
        public bool CanBeHealed => IsAlive && _currentHP < _maxHP;
        
        int GameSystem.Interfaces.IBattleEntity.Attack => _attack;
        int GameSystem.Interfaces.IBattleEntity.Defense => _defense;
        int GameSystem.Interfaces.IDamageable.CurrentHP => _currentHP;
        int GameSystem.Interfaces.IDamageable.MaxHP => _maxHP;
        
        protected virtual void Awake()
        {
            CacheComponents();
        }
        
        protected virtual void Start()
        {
            Initialize();
        }
        
        private void CacheComponents()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _outlineToggle = GetComponentInChildren<SpriteOutlineToggle>();
            _collider2D = GetComponent<Collider2D>();
        }
        
        public void Initialize()
        {
            _currentHP = _maxHP;
            
            if (MoveSet != null && MoveSet.Count > 0)
            {
                _moveInstances = new MoveInstance[MoveSet.Count];
                for (int i = 0; i < MoveSet.Count; i++)
                {
                    _moveInstances[i] = new MoveInstance(MoveSet[i]);
                }
            }
            else
            {
                _moveInstances = Array.Empty<MoveInstance>();
            }
    
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
            
            if (_hasCounter && CurrentHP > 0)
            {
                var battleManager = FindAnyObjectByType<BattleManager>();
                if (battleManager != null)
                {
                    battleManager.QueueCounterAttack(this);
                }
            }
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
                    if (_attackBuffStacks < MAX_BUFF_STACKS)
                    {
                        _attackBuffStacks++;
                        GameSystem.Events.BattleEvents.RaiseBuffStackChanged(this, buffType, _attackBuffStacks);
                    }
                    break;
                case BuffsType.Defense:
                    if (_defenseBuffStacks < MAX_BUFF_STACKS)
                    {
                        _defenseBuffStacks++;
                        GameSystem.Events.BattleEvents.RaiseBuffStackChanged(this, buffType, _defenseBuffStacks);
                    }
                    break;
            }
        }
        
        public void ApplyDebuff(BuffsType buffType, float multiplier)
        {
            switch (buffType)
            {
                case BuffsType.Attack:
                    if (_attackBuffStacks > 0)
                    {
                        _attackBuffStacks--;
                        GameSystem.Events.BattleEvents.RaiseBuffStackChanged(this, buffType, _attackBuffStacks);
                    }
                    break;
                case BuffsType.Defense:
                    if (_defenseBuffStacks > 0)
                    {
                        _defenseBuffStacks--;
                        GameSystem.Events.BattleEvents.RaiseBuffStackChanged(this, buffType, _defenseBuffStacks);
                    }
                    break;
            }
        }
        
        public void ApplyStatusEffect(MoveEffectType effectType, int duration)
        {
            switch (effectType)
            {
                case MoveEffectType.Stun:
                    ApplyStun(duration);
                    break;
                case MoveEffectType.Stealth:
                    ApplyStealth(duration);
                    break;
                case MoveEffectType.Counter:
                    ApplyCounter(duration);
                    break;
                case MoveEffectType.Taunt:
                    ApplyTaunt(duration);
                    break;
            }
        }
        
        public void ClearBuffs()
        {
            if (_attackBuffStacks > 0)
            {
                _attackBuffStacks = 0;
                GameSystem.Events.BattleEvents.RaiseBuffStackChanged(this, BuffsType.Attack, 0);
            }
            if (_defenseBuffStacks > 0)
            {
                _defenseBuffStacks = 0;
                GameSystem.Events.BattleEvents.RaiseBuffStackChanged(this, BuffsType.Defense, 0);
            }
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
            for (int i = 0; i < _moveInstances.Length; i++)
            {
                if (_moveInstances[i].CooldownLeft > 0)
                {
                    _moveInstances[i].CooldownLeft--;
                }
            }
            
            if (_stealthTurnsLeft > 0)
            {
                _stealthTurnsLeft--;
                if (_stealthTurnsLeft <= 0)
                {
                    _isStealthed = false;
                    UpdateStealthVisual();
                }
            }
            
            if (_counterTurnsLeft > 0)
            {
                _counterTurnsLeft--;
                if (_counterTurnsLeft <= 0) _hasCounter = false;
            }
    
            if (_tauntTurnsLeft > 0)
            {
                _tauntTurnsLeft--;
                if (_tauntTurnsLeft <= 0) _isTaunting = false;
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
        
        public void ApplyStealth(int turns)
        {
            _isStealthed = true;
            _stealthTurnsLeft = turns;
            UpdateStealthVisual();
        }
        
        private void ApplyCounter(int turns)
        {
            _hasCounter = true;
            _counterTurnsLeft = turns;
        }

        private void ApplyTaunt(int turns)
        {
            _isTaunting = true;
            _tauntTurnsLeft = turns;
        }
        
        private void UpdateStealthVisual()
        {
            if (_spriteRenderer != null)
            {
                var color = _spriteRenderer.color;
                color.a = _isStealthed ? STEALTH_ALPHA : 1f;
                _spriteRenderer.color = color;
            }
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
            ClearBuffs();
        }

        public void SetChargingState(bool newState, int moveIndex = -1, EntityType target = EntityType.Boss)
        {
            _isCharging = newState;
            _chargingMoveIndex = moveIndex;
            _chargingMoveTarget = target;
        }
    }
}