using NUnit.Framework;
using UnityEngine;
using Data;
using GameSystem;

namespace Tests.Data
{
    public class MoveDataTests
    {
        private MoveData _moveData;
        
        [SetUp]
        public void Setup()
        {
            _moveData = ScriptableObject.CreateInstance<MoveData>();
            _moveData.Name = "Test Move";
            _moveData.Power = 40;
            _moveData.Accuracy = 90;
            _moveData.CriticalChance = 0.05f;
            _moveData.Type = ElementType.Fire;
            _moveData.Category = MoveCategory.Single;
            _moveData.Cooldown = 2;
            _moveData.UsageLimit = 10;
        }
        
        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_moveData);
        }
        
        [Test]
        public void MoveInstance_Initializes_Correctly()
        {
            // 실행
            MoveInstance instance = new MoveInstance(_moveData);
            
            // 검증
            Assert.AreEqual(_moveData, instance.Data, "Move instance should reference the provided move data");
            Assert.AreEqual(0, instance.CooldownLeft, "Initial cooldown should be 0");
            Assert.AreEqual(_moveData.UsageLimit, instance.UsageLeft, "Usage left should equal the limit");
        }
        
        [TestCase(MoveCategory.Single, Description = "Single target move")]
        [TestCase(MoveCategory.AOE, Description = "Area of effect move")]
        [TestCase(MoveCategory.MultiRandom, Description = "Multi-hit random targets move")]
        [TestCase(MoveCategory.Charge, Description = "Charge-up move")]
        [TestCase(MoveCategory.Buff, Description = "Buff move")]
        [TestCase(MoveCategory.Debuff, Description = "Debuff move")]
        public void MoveData_Supports_Different_Categories(MoveCategory category)
        {
            // 설정
            _moveData.Category = category;
            
            // 실행 - 단순히 데이터가 올바르게 설정되었는지 확인
            MoveInstance instance = new MoveInstance(_moveData);
            
            // 검증
            Assert.AreEqual(category, instance.Data.Category, 
                $"Move instance should have {category} category");
        }
    }
}