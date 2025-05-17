using System;
using GameSystem;
using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;

namespace Entity
{
    public class BattleEntity : MonoBehaviour
    {
        [Header("기본 정보")]
        public string EntityName;
        public ElementType ElementType;
        // public EntityType entityType;
        public Sprite EntitySprite;
    
        [Header("스탯")]
        public int MaxHP = 100;
        public int CurrentHP;
        public int Attack = 50;
        public int Defense = 50;

        [Header("기술")] 
        public List<MoveData> MoveSet = new List<MoveData>();

        private MoveInstance[] _moveInstances;
        public ReadOnlySpan<MoveInstance> MoveInstances => _moveInstances;
        
        public float atkBuffMultiplier { get; private set; } = 1f;
        public float defBuffMultiplier { get; private set; } = 1f;
        
        void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            CurrentHP = MaxHP;

            _moveInstances = MoveSet
                .Select(so => new MoveInstance(so))
                .ToArray();
        }

        public ref MoveInstance GetMoveInstance(int index)
            => ref _moveInstances[index];

        protected float GetDefenseFactor() => 100f / (100f + Defense);
        protected virtual float GetWeaknessFactor(ElementType moveType) => 1f; // Player character doesn't have weakness type

        public MoveData GetMoveData(int moveIndex)
        {
            if (moveIndex < 0 || moveIndex >= MoveSet.Count)
            {
                return null;
            }
            
            return MoveSet[moveIndex];
        }

        public virtual float PreviewMitigate(float rawDamage, MoveData move)
        {
            float damage = rawDamage * GetDefenseFactor() * GetWeaknessFactor(move.Type);
            return damage;
        }

        public void TakeDamage(ElementType moveType, RawHit context)
        {
            int finalDamage = Mathf.RoundToInt(context.Damage * GetDefenseFactor() * GetWeaknessFactor(moveType));
            finalDamage = Mathf.Max(finalDamage, 1);

            CurrentHP -= finalDamage;
            CurrentHP = Mathf.Clamp(CurrentHP, 0, MaxHP);
        }
    }
}
