using GameSystem;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// ScriptableObject representing the static data for a battle move/skill.
    /// Can hold multiple effects (Damage, Buff, etc.)
    /// and also define how it targets (Single, AOE) and whom it targets (Allies, Enemies, etc.).
    /// </summary>
    [CreateAssetMenu(fileName = "MoveData", menuName = "Scriptable Objects/MoveData")]
    public class MoveData : ScriptableObject
    {
        /// <summary>The display name of the move.</summary>
        public string Name;

        /// <summary>The targeting category (Single, AOE, etc.)</summary>
        public MoveCategory Category;

        /// <summary>Which side (Self, Allies, Enemies, etc.) can this move target?</summary>
        public TargetSide AllowedTargetSide;

        /// <summary>The elemental type of the move (optional enum for your system).</summary>
        public ElementType Type;

        /// <summary>Number of turns this move is on cooldown after being used.</summary>
        public int Cooldown = 2;

        /// <summary>Maximum number of times this move can be used in a battle.</summary>
        public int UsageLimit = 99;

        /// <summary>If true, this move requires a 'charge-up' turn before dealing effects.</summary>
        public bool RequiresCharge = false;

        /// <summary>Multiple effects that this move applies, e.g. [Damage(80), Buff(+20 ATK), Heal(30)].</summary>
        public MoveEffect[] Effects;
    }

    /// <summary>
    /// Represents an instance of a move during a battle, including cooldown usage, etc.
    /// This does NOT store target info; that should go in ActionData or similar runtime logic.
    /// </summary>
    [System.Serializable]
    public class MoveInstance
    {
        public MoveData Data;
        public int CooldownLeft;
        public int UsageLeft;

        public MoveInstance(MoveData soData)
        {
            Data = soData;
            CooldownLeft = 0;
            UsageLeft = soData.UsageLimit;
        }
    }
}
