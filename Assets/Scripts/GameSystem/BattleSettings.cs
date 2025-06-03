using UnityEngine;

namespace GameSystem
{
    [CreateAssetMenu(fileName = "BattleSettings", menuName = "Scriptable Objects/BattleSettings")]
    public class BattleSettings : ScriptableObject
    {
        [Header("Turn Settings")]
        [Tooltip("Initial delay before first turn")]
        public float TurnStartDelay = 0.5f;
        
        [Tooltip("Delay between turns")]
        public float TurnTransitionDelay = 0.3f;
        
        [Header("Combat Settings")]
        [Tooltip("Base critical hit multiplier")]
        [Range(1.2f, 2.0f)]
        public float CriticalMultiplier = 1.5f;
        
        [Tooltip("Minimum damage that can be dealt")]
        [Range(1, 10)]
        public int MinimumDamage = 1;
        
        [Tooltip("Shield damage reduction factor")]
        [Range(0.1f, 0.9f)]
        public float ShieldDamageReduction = 0.5f;
        
        [Header("Status Effect Settings")]
        [Tooltip("Default stun duration in turns")]
        [Range(1, 3)]
        public int DefaultStunDuration = 1;
        
        [Tooltip("Shield break stun duration")]
        [Range(1, 3)]
        public int ShieldBreakStunDuration = 1;
        
        [Header("Stat Modifier Limits")]
        [Tooltip("Minimum stat multiplier from buffs/debuffs")]
        [Range(0.1f, 0.9f)]
        public float MinStatMultiplier = 0.6f;
        
        [Tooltip("Maximum stat multiplier from buffs/debuffs")]
        [Range(1.1f, 3.0f)]
        public float MaxStatMultiplier = 1.4f;
        
        [Header("Multi-Hit Settings")]
        [Tooltip("Minimum hits for multi-random attacks")]
        [Range(1, 5)]
        public int MinMultiHits = 2;
        
        [Tooltip("Maximum hits for multi-random attacks")]
        [Range(2, 10)]
        public int MaxMultiHits = 5;
        
        [Tooltip("Delay between multi-hits")]
        [Range(0.1f, 1.0f)]
        public float MultiHitDelay = 0.5f;
        
        [Header("Guard Settings")]
        [Tooltip("Defense multiplier when guarding")]
        [Range(1.5f, 3.0f)]
        public float GuardDefenseMultiplier = 2.0f;
        
        [Header("Effect Settings")]
        [Tooltip("Default visual effect duration")]
        [Range(0.5f, 3.0f)]
        public float EffectDuration = 2.0f;
        
        [Tooltip("Default message display duration")]
        [Range(0.5f, 3.0f)]
        public float MessageDuration = 1.0f;
        
        [Header("Formula Settings")]
        [Tooltip("Base value for defense calculation")]
        [Range(50, 200)]
        public int DefenseFormulaBase = 100;
    }
}