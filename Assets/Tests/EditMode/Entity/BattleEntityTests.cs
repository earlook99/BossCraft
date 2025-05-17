using NUnit.Framework;
using UnityEngine;
using GameSystem;
using Entity;
using Data;

namespace Tests.Entity
{
    public class BattleEntityTests
    {
        private BattleEntity _entity;
        private GameObject _entityObject;
        private MoveData _moveData;
        
        [SetUp]
        public void Setup()
        {
            // 테스트용 엔티티 생성
            _entityObject = new GameObject("TestEntity");
            _entity = _entityObject.AddComponent<BattleEntity>();
            _entity.MaxHP = 100;
            _entity.CurrentHP = 100;
            _entity.Attack = 50;
            _entity.Defense = 25;
            _entity.ElementType = ElementType.Fire;
            
            // 테스트용 기술 생성
            _moveData = ScriptableObject.CreateInstance<MoveData>();
        }
        
        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_entityObject);
            Object.DestroyImmediate(_moveData);
        }
        
        [TestCase(ElementType.Water, 50, Description = "Water type attack")]
        [TestCase(ElementType.Fire, 50, Description = "Fire type attack")]
        [TestCase(ElementType.Grass, 50, Description = "Grass type attack")]
        public void TakeDamage_Ignores_Type_Effectiveness(ElementType attackType, int damage)
        {
            // 초기 HP 저장
            int initialHP = _entity.CurrentHP;
            
            // 실행
            _entity.TakeDamage(attackType, new RawHit(damage, true, false));
            
            // 검증
            float defenseFactor = 100f / (100f + _entity.Defense);
            // 타입 상성 없이 1.0f로 계산 (플레이어 엔티티는 타입 상성 무시)
            float effectiveness = 1.0f;
            int expectedDamage = Mathf.Max(1, Mathf.RoundToInt(damage * defenseFactor * effectiveness));
            int expectedHP = initialHP - expectedDamage;
            expectedHP = Mathf.Max(0, expectedHP); // HP는 0 이하로 내려가지 않음
            
            Assert.AreEqual(expectedHP, _entity.CurrentHP, 
                $"{attackType} attack with {damage} damage should reduce HP to {expectedHP}");
        }
        
        [Test]
        public void TakeDamage_MinimumDamage_Is_One()
        {
            // 설정 - 매우 낮은 데미지 (방어력으로 인해 1보다 작아질 수 있는)
            int veryLowDamage = 1;
            ElementType attackType = ElementType.Fire;
            
            // 초기 HP 저장
            int initialHP = _entity.CurrentHP;
            
            // 실행
            _entity.TakeDamage(attackType, new RawHit(veryLowDamage, true, false));
            
            // 검증 - 최소 1 데미지는 들어가야 함
            Assert.AreEqual(initialHP - 1, _entity.CurrentHP, 
                "Even with high defense, minimum damage should be 1");
        }
        
        [Test]
        public void PreviewMitigate_Ignores_Type_Effectiveness()
        {
            // 설정
            _moveData.Type = ElementType.Water;
            float rawDamage = 100f;
            
            // 실행
            float mitigated = _entity.PreviewMitigate(rawDamage, _moveData);
            
            // 검증
            float defenseFactor = 100f / (100f + _entity.Defense);
            // 타입 상성 없이 1.0f로 계산 (플레이어 엔티티는 타입 상성 무시)
            float effectiveness = 1.0f;
            float expected = rawDamage * defenseFactor * effectiveness;
            
            Assert.AreEqual(expected, mitigated, 0.01f, 
                "Damage mitigation should ignore type effectiveness");
        }
    }
}