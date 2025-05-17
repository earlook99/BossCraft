using NUnit.Framework;
using UnityEngine;
using GameSystem;
using Entity;
using Data;

namespace Tests.GameSystem
{
    public class DamageFormulaTests
    {
        private BattleEntity _attacker;
        private GameObject _attackerObject;
        private MoveData _moveData;
        
        [SetUp]
        public void Setup()
        {
            // 테스트 전 환경 설정
            _attackerObject = new GameObject("TestAttacker");
            _attacker = _attackerObject.AddComponent<BattleEntity>();
            _attacker.Attack = 50;
            
            _moveData = ScriptableObject.CreateInstance<MoveData>();
            _moveData.Power = 40;
            _moveData.Accuracy = 100;
            _moveData.CriticalChance = 0.0f;
            _moveData.Type = ElementType.Fire;
        }
        
        [TearDown]
        public void Cleanup()
        {
            // 테스트 후 정리
            Object.DestroyImmediate(_attackerObject);
            Object.DestroyImmediate(_moveData);
        }
        
        [Test]
        public void GetRawHit_With_Perfect_Accuracy_Always_Hits()
        {
            // 설정
            _moveData.Accuracy = 100;
            MoveInstance moveInstance = new MoveInstance(_moveData);
            
            // 실행
            RawHit hit = DamageFormula.GetRawHit(_attacker, moveInstance);
            
            // 검증
            Assert.IsTrue(hit.IsHit, "Attack should hit with 100% accuracy");
            Assert.AreEqual(_moveData.Power * _attacker.Attack, hit.Damage);
        }
        
        [Test]
        public void GetRawHit_With_Zero_Accuracy_Always_Misses()
        {
            // 설정
            _moveData.Accuracy = 0;
            MoveInstance moveInstance = new MoveInstance(_moveData);
            
            // 실행
            RawHit hit = DamageFormula.GetRawHit(_attacker, moveInstance);
            
            // 검증
            Assert.IsFalse(hit.IsHit, "Attack should miss with 0% accuracy");
            Assert.AreEqual(0, hit.Damage, "Missed attacks should deal no damage");
        }
        
        [TestCase(50, 40, 0.0f, 80, 1600f, Description = "Base damage calculation")]
        [TestCase(50, 40, 0.5f, 100, 2500f, Description = "With 50% critical chance")]
        [TestCase(50, 40, 0.0f, 50, 1000f, Description = "With 50% accuracy")]
        public void GetExpectedRawDamage_Calculates_Correctly(
            int attack, int power, float critChance, int accuracy, float expected)
        {
            // 설정
            _attacker.Attack = attack;
            _moveData.Power = power;
            _moveData.CriticalChance = critChance;
            _moveData.Accuracy = accuracy;
            
            // 실행
            float actualDamage = DamageFormula.GetExpectedRawDamage(_attacker, _moveData);
            
            // 검증
            Assert.AreEqual(expected, actualDamage, 0.1f, 
                "Expected damage calculation is incorrect");
        }
    }
}