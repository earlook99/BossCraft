using System;
using Entity;
using Data;

namespace GameSystem.Events
{
    public static class BattleEvents
    {
        public static event Action<BattleStartedEventArgs> OnBattleStarted;
        public static event Action<BattleEndedEventArgs> OnBattleEnded;
        
        public static event Action<TurnStartedEventArgs> OnTurnStarted;
        public static event Action<TurnEndedEventArgs> OnTurnEnded;
        
        public static event Action<ActionExecutedEventArgs> OnActionExecuted;
        public static event Action<DamageDealtEventArgs> OnDamageDealt;
        public static event Action<HealingReceivedEventArgs> OnHealingReceived;
        public static event Action<StatusEffectAppliedEventArgs> OnStatusEffectApplied;
        
        public static event Action<EntityDefeatedEventArgs> OnEntityDefeated;
        public static event Action<ShieldActivatedEventArgs> OnShieldActivated;
        public static event Action<ShieldBrokenEventArgs> OnShieldBroken;
        
        public static event Action<BuffStackChangedEventArgs> OnBuffStackChanged;

        public static void RaiseBattleStarted(BattleEntity[] entities)
        {
            OnBattleStarted?.Invoke(new BattleStartedEventArgs { Entities = entities });
        }

        public static void RaiseBattleEnded(bool playerWon)
        {
            OnBattleEnded?.Invoke(new BattleEndedEventArgs { PlayerWon = playerWon });
        }

        public static void RaiseTurnStarted(BattleEntity entity, int turnNumber)
        {
            OnTurnStarted?.Invoke(new TurnStartedEventArgs 
            { 
                Entity = entity, 
                TurnNumber = turnNumber 
            });
        }

        public static void RaiseTurnEnded(BattleEntity entity)
        {
            OnTurnEnded?.Invoke(new TurnEndedEventArgs { Entity = entity });
        }

        public static void RaiseActionExecuted(BattleEntity source, ActionData action)
        {
            OnActionExecuted?.Invoke(new ActionExecutedEventArgs 
            { 
                Source = source, 
                Action = action 
            });
        }

        public static void RaiseDamageDealt(BattleEntity source, BattleEntity target, int damage, ElementType element)
        {
            OnDamageDealt?.Invoke(new DamageDealtEventArgs 
            { 
                Source = source, 
                Target = target, 
                Damage = damage, 
                Element = element 
            });
        }

        public static void RaiseHealingReceived(BattleEntity source, BattleEntity target, int amount)
        {
            OnHealingReceived?.Invoke(new HealingReceivedEventArgs 
            { 
                Source = source, 
                Target = target, 
                Amount = amount 
            });
        }

        public static void RaiseStatusEffectApplied(BattleEntity source, BattleEntity target, MoveEffectType effectType)
        {
            OnStatusEffectApplied?.Invoke(new StatusEffectAppliedEventArgs 
            { 
                Source = source, 
                Target = target, 
                EffectType = effectType 
            });
        }

        public static void RaiseEntityDefeated(BattleEntity entity)
        {
            OnEntityDefeated?.Invoke(new EntityDefeatedEventArgs { Entity = entity });
        }

        public static void RaiseShieldActivated(BossEntity boss, int shieldHP)
        {
            OnShieldActivated?.Invoke(new ShieldActivatedEventArgs 
            { 
                Boss = boss, 
                ShieldHP = shieldHP 
            });
        }

        public static void RaiseShieldBroken(BossEntity boss)
        {
            OnShieldBroken?.Invoke(new ShieldBrokenEventArgs { Boss = boss });
        }
        
        public static void RaiseBuffStackChanged(BattleEntity entity, BuffsType buffType, int newStackCount)
        {
            OnBuffStackChanged?.Invoke(new BuffStackChangedEventArgs 
            { 
                Entity = entity, 
                BuffType = buffType, 
                NewStackCount = newStackCount 
            });
        }

        public static void ClearAllListeners()
        {
            OnBattleStarted = null;
            OnBattleEnded = null;
            OnTurnStarted = null;
            OnTurnEnded = null;
            OnActionExecuted = null;
            OnDamageDealt = null;
            OnHealingReceived = null;
            OnStatusEffectApplied = null;
            OnEntityDefeated = null;
            OnShieldActivated = null;
            OnShieldBroken = null;
        }
    }

    public class BattleStartedEventArgs : EventArgs
    {
        public BattleEntity[] Entities { get; set; }
    }

    public class BattleEndedEventArgs : EventArgs
    {
        public bool PlayerWon { get; set; }
    }

    public class TurnStartedEventArgs : EventArgs
    {
        public BattleEntity Entity { get; set; }
        public int TurnNumber { get; set; }
    }

    public class TurnEndedEventArgs : EventArgs
    {
        public BattleEntity Entity { get; set; }
    }

    public class ActionExecutedEventArgs : EventArgs
    {
        public BattleEntity Source { get; set; }
        public ActionData Action { get; set; }
    }

    public class DamageDealtEventArgs : EventArgs
    {
        public BattleEntity Source { get; set; }
        public BattleEntity Target { get; set; }
        public int Damage { get; set; }
        public ElementType Element { get; set; }
    }

    public class HealingReceivedEventArgs : EventArgs
    {
        public BattleEntity Source { get; set; }
        public BattleEntity Target { get; set; }
        public int Amount { get; set; }
    }

    public class StatusEffectAppliedEventArgs : EventArgs
    {
        public BattleEntity Source { get; set; }
        public BattleEntity Target { get; set; }
        public MoveEffectType EffectType { get; set; }
    }

    public class EntityDefeatedEventArgs : EventArgs
    {
        public BattleEntity Entity { get; set; }
    }

    public class ShieldActivatedEventArgs : EventArgs
    {
        public BossEntity Boss { get; set; }
        public int ShieldHP { get; set; }
    }

    public class ShieldBrokenEventArgs : EventArgs
    {
        public BossEntity Boss { get; set; }
    }
    
    public class BuffStackChangedEventArgs : EventArgs
    {
        public BattleEntity Entity { get; set; }
        public BuffsType BuffType { get; set; }
        public int NewStackCount { get; set; }
    }
}