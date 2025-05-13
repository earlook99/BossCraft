using System;
using System.Collections.Generic;
using Data;
using UnityEngine;

namespace Entity
{
    public class CombatEntity : MonoBehaviour
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
        
        void Start()
        {
            CurrentHP = MaxHP;
        }

        void Update()
        {
        
        }

        public virtual void TakeDamage(ElementType moveType, int damage)
        {
            // float effectiveness = TypeChart.GetEffectiveness(moveType, this.ElementType);
            float defenseModifier = 100f / (100f + Defense);

            int finalDamage = Mathf.RoundToInt(damage * defenseModifier);
            finalDamage = Mathf.Max(finalDamage, 1);

            CurrentHP -= finalDamage;
        }
    }
}
