using System;
using System.Collections;
using System.Collections.Generic;
using Data;
using GameSystem;
using UI;
using UnityEngine;

namespace Entity
{
    public class BattleEntity : MonoBehaviour
    {
        [Header("Basic Information")]
        public string EntityName;
        public ElementType ElementType;

        [Header("Stats")]
        [SerializeField] private int maxHP = 100;
        [SerializeField] private int currentHP;
        [SerializeField] private int attack = 50;
        [SerializeField] private int defense = 50;
        
        private int attackBuffStacks = 0;
        private int defenseBuffStacks = 0;
        private const int MAX_BUFF_STACKS = 2;
        private const float BUFF_PER_STACK = 0.2f;
        
        public int MaxHP 
        { 
            get => maxHP; 
            set => maxHP = value; 
        }
        
        public int CurrentHP 
        { 
            get => currentHP; 
            set => currentHP = Mathf.Clamp(value, 0, maxHP); 
        }
        
        public int Attack 
        { 
            get => attack; 
            set => attack = value; 
        }
        
        public int Defense 
        { 
            get => defense; 
            set => defense = value; 
        }

        [Header("Moves")] 
        public List<MoveData> MoveSet = new List<MoveData>();

        protected SpriteRenderer spriteRenderer;
        private SpriteOutlineToggle outlineToggle;
        private Collider2D entityCollider;
        private MoveInstance[] moveInstances;
        
        private BattleManager battleManager;
        private BattleUIController uiController;
        
        private bool isCharging = false;
        private bool isGuarding = false;
        private int chargingMoveIndex;
        private EntityType chargingMoveTarget;
        
        private bool isStealthed = false;
        private int stealthTurnsLeft = 0;
        
        public bool IsStealthed => isStealthed;
        public int StealthTurnsLeft => stealthTurnsLeft;
        
        private bool hasCounter = false;
        private int counterTurnsLeft = 0;
        private bool isTaunting = false;
        private int tauntTurnsLeft = 0;
        
        public bool HasCounter => hasCounter;
        public bool IsTaunting => isTaunting;
        
        private const float DEFAULT_SPRITE_ALPHA = 0.1f;
        private const float GUARD_DEFENSE_MULTIPLIER = 2f;
        private const int DEFENSE_FORMULA_BASE = 100;
        private const float STEALTH_ALPHA = 0.3f;

        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public SpriteOutlineToggle OutlineToggle => outlineToggle;
        public Collider2D EntityCollider => entityCollider;
        public ReadOnlySpan<MoveInstance> MoveInstances => moveInstances;
        public bool IsStunned { get; private set; }
        public int StunTurnsLeft { get; private set; }
        public bool IsCharging => isCharging;
        public bool IsGuarding => isGuarding;
        public int ChargingMoveIndex => chargingMoveIndex;
        public EntityType ChargingMoveTarget => chargingMoveTarget;
        public bool HasBuffs => attackBuffStacks > 0 || defenseBuffStacks > 0;
        
        private float atkBuffMultiplier = 1f;
        public float AtkBuffMultiplier 
        { 
            get => atkBuffMultiplier * (1f + (attackBuffStacks * BUFF_PER_STACK));
            set => atkBuffMultiplier = value;
        }
        public float DefBuffMultiplier { get; set; } = 1f;
        
        public int AttackBuffStacks => attackBuffStacks;
        public int DefenseBuffStacks => defenseBuffStacks;
        
        public string Name => EntityName;
        public bool IsAlive => currentHP > 0;
        public bool CanBeTargeted => IsAlive && !IsStunned;
        public float AttackMultiplier => AtkBuffMultiplier;
        public float DefenseMultiplier => DefBuffMultiplier * (1f + (defenseBuffStacks * BUFF_PER_STACK));
        public bool CanBeHealed => IsAlive && currentHP < maxHP;
        public bool CanTakeTurn() => IsAlive && !IsStunned;
        
        protected virtual void Awake()
        {
            CacheComponents();
        }
        
        protected virtual void Start()
        {
            Initialize();
            battleManager = FindAnyObjectByType<BattleManager>();
            uiController = FindAnyObjectByType<BattleUIController>();
        }
        
        private void CacheComponents()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            outlineToggle = GetComponentInChildren<SpriteOutlineToggle>();
            entityCollider = GetComponent<Collider2D>();
        }
        
        public void Initialize()
        {
            currentHP = maxHP;
            
            if (MoveSet != null && MoveSet.Count > 0)
            {
                moveInstances = new MoveInstance[MoveSet.Count];
                for (int i = 0; i < MoveSet.Count; i++)
                {
                    moveInstances[i] = new MoveInstance(MoveSet[i]);
                }
            }
            else
            {
                moveInstances = Array.Empty<MoveInstance>();
            }
    
            if (spriteRenderer != null && this is not BossEntity)
            {
                var color = spriteRenderer.color;
                color.a = GameConstants.UI.INACTIVE_SPRITE_ALPHA;
                spriteRenderer.color = color;
            }
        }
        
        public ref MoveInstance GetMoveInstance(int index)
            => ref moveInstances[index];
        
        protected float GetDefenseFactor() 
            => GameConstants.Battle.DEFENSE_FORMULA_BASE / (GameConstants.Battle.DEFENSE_FORMULA_BASE + defense);
        
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
            
            if (hasCounter && CurrentHP > 0 && battleManager != null)
            {
                battleManager.QueueCounterAttack(this);
            }
        }
        
        public float GetDamageMultiplier(ElementType damageType)
        {
            return GetDefenseFactor() * GetWeaknessFactor(damageType);
        }
        
        public void Heal(int amount)
        {
            currentHP = Mathf.Min(currentHP + amount, maxHP);
        }
        
        public void ApplyBuff(BuffsType buffType, float multiplier)
        {
            switch (buffType)
            {
                case BuffsType.Attack:
                    if (attackBuffStacks < MAX_BUFF_STACKS)
                    {
                        attackBuffStacks++;
                        NotifyBuffStackChanged(buffType, attackBuffStacks);
                    }
                    break;
                case BuffsType.Defense:
                    if (defenseBuffStacks < MAX_BUFF_STACKS)
                    {
                        defenseBuffStacks++;
                        NotifyBuffStackChanged(buffType, defenseBuffStacks);
                    }
                    break;
            }
        }
        
        public void ApplyDebuff(BuffsType buffType, float multiplier)
        {
            switch (buffType)
            {
                case BuffsType.Attack:
                    if (attackBuffStacks > 0)
                    {
                        attackBuffStacks--;
                        NotifyBuffStackChanged(buffType, attackBuffStacks);
                    }
                    break;
                case BuffsType.Defense:
                    if (defenseBuffStacks > 0)
                    {
                        defenseBuffStacks--;
                        NotifyBuffStackChanged(buffType, defenseBuffStacks);
                    }
                    break;
            }
        }
        
        private void NotifyBuffStackChanged(BuffsType buffType, int newStackCount)
        {
            if (uiController != null)
            {
                uiController.UpdateBuffIcon(this, buffType);
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
            if (attackBuffStacks > 0)
            {
                attackBuffStacks = 0;
                NotifyBuffStackChanged(BuffsType.Attack, 0);
            }
            if (defenseBuffStacks > 0)
            {
                defenseBuffStacks = 0;
                NotifyBuffStackChanged(BuffsType.Defense, 0);
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
            for (int i = 0; i < moveInstances.Length; i++)
            {
                if (moveInstances[i].CooldownLeft > 0)
                {
                    moveInstances[i].CooldownLeft--;
                }
            }
            
            if (stealthTurnsLeft > 0)
            {
                stealthTurnsLeft--;
                if (stealthTurnsLeft <= 0)
                {
                    isStealthed = false;
                    UpdateStealthVisual();
                }
            }
            
            if (counterTurnsLeft > 0)
            {
                counterTurnsLeft--;
                if (counterTurnsLeft <= 0) hasCounter = false;
            }
    
            if (tauntTurnsLeft > 0)
            {
                tauntTurnsLeft--;
                if (tauntTurnsLeft <= 0) isTaunting = false;
            }
        }

        public void ApplyStun(int turns)
        {
            IsStunned = true;
            StunTurnsLeft = turns;
        }
        
        public void ApplyStealth(int turns)
        {
            isStealthed = true;
            stealthTurnsLeft = turns;
            UpdateStealthVisual();
        }
        
        private void ApplyCounter(int turns)
        {
            hasCounter = true;
            counterTurnsLeft = turns;
        }

        private void ApplyTaunt(int turns)
        {
            isTaunting = true;
            tauntTurnsLeft = turns;
        }
        
        private void UpdateStealthVisual()
        {
            if (spriteRenderer != null)
            {
                var color = spriteRenderer.color;
                color.a = isStealthed ? STEALTH_ALPHA : 1f;
                spriteRenderer.color = color;
            }
        }
        
        public void SetGuardState(bool guarding)
        {
            isGuarding = guarding;
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
            isCharging = newState;
            chargingMoveIndex = moveIndex;
            chargingMoveTarget = target;
        }
        
        public virtual IEnumerator PlayDamageFlash(ElementType attackType, float duration)
        {
            if (spriteRenderer == null) yield break;

            Color originalColor = spriteRenderer.color;
            float flashInterval = 0.15f;
            int flashCount = Mathf.FloorToInt(duration / (flashInterval * 2));

            for (int i = 0; i < flashCount; i++)
            {
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
                yield return new WaitForSeconds(flashInterval);
        
                spriteRenderer.color = originalColor;
                yield return new WaitForSeconds(flashInterval);
            }
    
            spriteRenderer.color = originalColor;
        }
    }
}