using GameSystem;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// Defines the category of a move, determining its targeting and primary effect type.
    /// </summary>
    public enum MoveCategory
    {
        /// <summary>Targets a single entity.</summary>
        Single,
        /// <summary>Targets all entities on the opposing side (Area of Effect).</summary>
        AOE,
        /// <summary>Hits one or more entities at random, multiple times.</summary>
        MultiRandom,
        /// <summary>A move that requires a charging period or has a delayed effect.</summary>
        Charge,
        /// <summary>Applies a positive status effect (buff) to the user or allies.</summary>
        Buff,
        /// <summary>Applies a negative status effect (debuff) to opponents.</summary>
        Debuff,
        /// <summary>Removes buffs from the opponent(s).</summary>
        ClearOppBuff,
        /// <summary>Removes debuffs from the user or allies.</summary>
        ClearSelfDebuff,
        /// <summary>Has a chance to stun the target, preventing action.</summary>
        Stun
    }
    
    /// <summary>
    /// ScriptableObject representing the static data for a battle move/skill.
    /// </summary>
    [CreateAssetMenu(fileName = "MoveData", menuName = "Scriptable Objects/MoveData")]
    public class MoveData : ScriptableObject
    {
        /// <summary>The display name of the move.</summary>
        public string Name;
        /// <summary>The category of the move, determining its targeting and primary effect type.</summary>
        public MoveCategory Category;
        /// <summary>The elemental type of the move.</summary>
        public ElementType Type;
        
        /// <summary>The base power of the move, used in damage calculation.</summary>
        public int Power;
        [SerializeField] private int _accuracy = 100; // Internal accuracy stored as 0-100 integer
        /// <summary>The chance (0.0 to 1.0) of this move landing a critical hit.</summary>
        public float CriticalChance = 0.05f;

        /// <summary>The number of turns this move is on cooldown after being used.</summary>
        public int Cooldown = 2;
        /// <summary>The maximum number of times this move can be used in a battle.</summary>
        public int UsageLimit = 99;

        /// <summary>
        /// Gets or sets the accuracy of the move, as a float value between 0.0 (0%) and 1.0 (100%).
        /// </summary>
        public float Accuracy
        {
            get => _accuracy / 100f;
            set => _accuracy = Mathf.Clamp(Mathf.RoundToInt(value * 100f), 0, 100); // Store as 0-100
        }
    }

    /// <summary>
    /// Represents an instance of a move during a battle, including its current cooldown and usage status.
    /// </summary>
    [System.Serializable]
    public class MoveInstance
    {
        /// <summary>The static <see cref="MoveData"/> for this move instance.</summary>
        public MoveData Data;
        /// <summary>The number of turns remaining before this move can be used again.</summary>
        public int CooldownLeft;
        /// <summary>The number of times this move can still be used in the current battle.</summary>
        public int UsageLeft;

        /// <summary>
        /// Initializes a new instance of the <see cref="MoveInstance"/> class
        /// based on the provided <see cref="MoveData"/>.
        /// </summary>
        /// <param name="soData">The ScriptableObject data for this move.</param>
        public MoveInstance(MoveData soData)
        {
            Data = soData;
            CooldownLeft = 0;
            UsageLeft = soData.UsageLimit;
        }
    }
}
