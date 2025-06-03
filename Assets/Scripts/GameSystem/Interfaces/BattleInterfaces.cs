using Data;

namespace GameSystem.Interfaces
{
    public interface ITargetable
    {
        string Name { get; }
        bool IsAlive { get; }
        bool CanBeTargeted { get; }
        EntityType EntityType { get; }
    }
    
    public interface IDamageable
    {
        int CurrentHP { get; }
        int MaxHP { get; }
        void TakeDamage(ElementType damageType, int damage);
        float GetDamageMultiplier(ElementType damageType);
    }
    
    public interface IHealable
    {
        void Heal(int amount);
        bool CanBeHealed { get; }
    }
    
    public interface IEffectReceiver
    {
        void ApplyBuff(BuffsType buffType, float multiplier);
        void ApplyDebuff(BuffsType buffType, float multiplier);
        void ApplyStatusEffect(MoveEffectType effectType, int duration);
        void ClearBuffs();
        void ClearDebuffs();
    }
    
    public interface ITurnTaker
    {
        bool IsStunned { get; }
        bool IsCharging { get; }
        void StartTurn();
        void EndTurn();
        bool CanTakeTurn();
    }
    
    public interface IShieldable
    {
        bool HasShield { get; }
        int ShieldHP { get; }
        void ActivateShield(int shieldAmount);
        void DamageShield(int damage);
        void BreakShield();
    }
    
    public interface IActionExecutor
    {
        bool CanExecuteAction(ActionType actionType);
        void ExecuteAction(ActionData action);
    }
    
    public interface IBattleEntity : ITargetable, IDamageable, IHealable, IEffectReceiver, ITurnTaker
    {
        int Attack { get; }
        int Defense { get; }
        float AttackMultiplier { get; }
        float DefenseMultiplier { get; }
    }
}