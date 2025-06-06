using UnityEngine;

namespace Data
{
    public enum MoveCategory
    {
        Single,
        AOE,
        MultiRandom,
        MultiHealing
    }

    public enum TargetSide
    {
        Self,
        Ally,
        Allies,
        Enemy,
        Enemies,
        Any
    }

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