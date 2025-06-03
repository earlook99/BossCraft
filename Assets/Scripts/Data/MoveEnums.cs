using UnityEngine;

namespace Data
{
    /// <summary>
    /// Determines how the move is targeted (single target, AOE, etc.).
    /// </summary>
    public enum MoveCategory
    {
        /// <summary>Attacks or affects a single target (based on AllowedTargetSide).</summary>
        Single,
        
        /// <summary>Attacks or affects all valid targets (based on AllowedTargetSide).</summary>
        AOE,
        
        /// <summary>Hits multiple random targets within the valid side.</summary>
        MultiRandom,
        
        /// <summary>Healing multiple or special categories, if needed.</summary>
        MultiHealing
        // ... 더 확장 가능
    }

    /// <summary>
    /// Defines which side(s) can be targeted by this move.
    /// </summary>
    public enum TargetSide
    {
        Self,       // Only the caster
        Ally,
        Allies,     // Allies (could include self if you want)
        Enemy,
        Enemies,    // The opposing side
        Any         // Could be self, ally, or enemy
    }

    /// <summary>
    /// Defines various types of effects a move can have.
    /// One move can have multiple effects.
    /// </summary>
    public enum MoveEffectType
    {
        Damage,
        Heal,
        Buff,
        Debuff,
        ClearOppBuff,
        ClearSelfDebuff,
        Stun,
        Shield,
        Stealth,
        Counter,
        Taunt
    }

    public enum BuffsType
    {
        None,
        Attack,
        Defense
    }
}