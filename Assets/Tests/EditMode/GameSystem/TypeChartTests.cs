using NUnit.Framework;
using GameSystem;

namespace Tests.GameSystem
{
    public class TypeChartTests
    {
        [TestCase(ElementType.Water, ElementType.Fire, 2.0f, Description = "Water is super effective against Fire")]
        [TestCase(ElementType.Fire, ElementType.Water, 0.5f, Description = "Fire is not very effective against Water")]
        [TestCase(ElementType.Fire, ElementType.Fire, 1.0f, Description = "Same type has normal effectiveness")]
        [TestCase(ElementType.Water, ElementType.Water, 1.0f, Description = "Same type has normal effectiveness")]
        [TestCase(ElementType.Electric, ElementType.Water, 2.0f, Description = "Electric is super effective against Water")]
        [TestCase(ElementType.Rock, ElementType.Electric, 2.0f, Description = "Rock is super effective against Electric")]
        [TestCase(ElementType.Grass, ElementType.Rock, 2.0f, Description = "Grass is super effective against Rock")]
        public void TypeEffectiveness_Returns_Correct_Value(ElementType attackType, ElementType defenseType, float expected)
        {
            // 실행
            float effectiveness = TypeChart.GetEffectiveness(attackType, defenseType);
            
            // 검증
            Assert.AreEqual(expected, effectiveness, 
                $"{attackType} vs {defenseType} should have {expected}x effectiveness");
        }
        
        [Test]
        public void TypeChart_All_Types_Are_Covered()
        {
            // 모든 타입 쌍의 상성이 설정되어 있는지 확인
            ElementType[] allTypes = (ElementType[])System.Enum.GetValues(typeof(ElementType));
            
            foreach (var attackType in allTypes)
            {
                foreach (var defenseType in allTypes)
                {
                    float effectiveness = TypeChart.GetEffectiveness(attackType, defenseType);
                    
                    // 유효한 상성 값인지 확인
                    bool validValue = effectiveness == 0.5f || 
                                      effectiveness == 1.0f || 
                                      effectiveness == 2.0f;
                    
                    Assert.IsTrue(validValue, 
                        $"{attackType} vs {defenseType} has invalid effectiveness {effectiveness}");
                }
            }
        }
    }
}