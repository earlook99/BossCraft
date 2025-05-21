using System;
using GameSystem;
using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;

namespace Entity
{
    /// <summary>
    /// Represents an entity participating in a battle.
    /// This class manages the entity's stats, moves, and battle-related actions.
    /// </summary>
    public class BattleEntity : MonoBehaviour
    {
        [Header("Basic Information")]
        /// <summary>The display name of the entity.</summary>
        public string EntityName;
        /// <summary>The elemental type of the entity.</summary>
        public ElementType ElementType;
        /// <summary>The sprite used to represent the entity in the UI.</summary>
        public Sprite EntitySprite;
    
        [Header("Stats")]
        /// <summary>The maximum hit points of the entity.</summary>
        public int MaxHP = 100;
        /// <summary>The current hit points of the entity.</summary>
        public int CurrentHP;
        /// <summary>The attack stat of the entity.</summary>
        public int Attack = 50;
        /// <summary>The defense stat of the entity.</summary>
        public int Defense = 50;

        [Header("Moves")] 
        /// <summary>A list of <see cref="MoveData"/> scriptable objects representing the entity's available moves.</summary>
        public List<MoveData> MoveSet = new List<MoveData>();

        private MoveInstance[] _moveInstances;
        /// <summary>
        /// Gets a read-only span of the entity's current move instances, including cooldown and usage status.
        /// </summary>
        public ReadOnlySpan<MoveInstance> MoveInstances => _moveInstances;
        
        /// <summary>
        /// Gets the current attack buff multiplier for this entity. Defaults to 1f (no buff).
        /// </summary>
        public float AtkBuffMultiplier { get; private set; } = 1f;
        /// <summary>
        /// Gets the current defense buff multiplier for this entity. Defaults to 1f (no buff).
        /// </summary>
        public float DefBuffMultiplier { get; private set; } = 1f;
        
        /// <summary>
        /// Called when the script instance is being loaded. Calls Initialize.
        /// </summary>
        void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Initializes the entity's state, setting current HP to max HP and creating move instances.
        /// </summary>
        public void Initialize()
        {
            CurrentHP = MaxHP;

            _moveInstances = MoveSet
                .Select(so => new MoveInstance(so))
                .ToArray();
        }

        /// <summary>
        /// Gets a reference to a specific <see cref="MoveInstance"/> by its index in the moveset.
        /// </summary>
        /// <param name="index">The index of the move.</param>
        /// <returns>A reference to the <see cref="MoveInstance"/>.</returns>
        public ref MoveInstance GetMoveInstance(int index)
            => ref _moveInstances[index];

        /// <summary>
        /// Calculates the defense factor based on the entity's Defense stat.
        /// Formula: 100 / (100 + Defense). Higher defense results in a lower factor (less damage taken).
        /// </summary>
        /// <returns>The calculated defense factor as a float.</returns>
        protected float GetDefenseFactor() => 100f / (100f + Defense); // Defense formula: 100 / (100 + Defense)
        
        /// <summary>
        /// Calculates the elemental weakness factor for a given move type.
        /// Player characters typically don't have weaknesses, so this returns 1f.
        /// Can be overridden by subclasses (e.g., BossEntity).
        /// </summary>
        /// <param name="moveType">The elemental type of the incoming move.</param>
        /// <returns>The elemental weakness multiplier (1f for neutral).</returns>
        protected virtual float GetWeaknessFactor(ElementType moveType) => 1f; // Player character doesn't have weakness type

        /// <summary>
        /// Retrieves the <see cref="MoveData"/> for a move at the specified index in the MoveSet.
        /// </summary>
        /// <param name="moveIndex">The index of the move in the MoveSet list.</param>
        /// <returns>The <see cref="MoveData"/> if found; otherwise, null.</returns>
        public MoveData GetMoveData(int moveIndex)
        {
            if (moveIndex < 0 || moveIndex >= MoveSet.Count)
            {
                return null;
            }
            
            return MoveSet[moveIndex];
        }

        /// <summary>
        /// Previews the damage mitigation for a raw damage amount based on the entity's defense and elemental type.
        /// </summary>
        /// <param name="rawDamage">The raw damage amount before mitigation.</param>
        /// <param name="move">The move data, used to determine the move's elemental type.</param>
        /// <returns>The estimated damage after mitigation.</returns>
        public virtual float PreviewMitigate(float rawDamage, MoveData move)
        {
            float damage = rawDamage * GetDefenseFactor() * GetWeaknessFactor(move.Type);
            return damage;
        }

        /// <summary>
        /// Applies damage to the entity after calculating mitigation.
        /// Ensures damage is at least 1 and clamps HP between 0 and MaxHP.
        /// </summary>
        /// <param name="moveType">The elemental type of the incoming move.</param>
        /// <param name="context">The <see cref="RawHit"/> context containing initial damage and hit status.</param>
        public void TakeDamage(ElementType moveType, RawHit context)
        {
            int finalDamage = Mathf.RoundToInt(context.Damage * GetDefenseFactor() * GetWeaknessFactor(moveType));
            finalDamage = Mathf.Max(finalDamage, 1);

            CurrentHP -= finalDamage;
            CurrentHP = Mathf.Clamp(CurrentHP, 0, MaxHP);
        }
    }
}
