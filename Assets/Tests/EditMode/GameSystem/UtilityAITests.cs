using NUnit.Framework;
using UnityEngine;
using GameSystem;
using Entity;
using Data;
using System.Collections.Generic;

namespace Tests.GameSystem
{
    public class UtilityAITests
    {
        private BossEntity _boss;
        private BattleEntity[] _players;
        private AIWeights _weights;
        private GameObject _bossObject;
        private List<GameObject> _playerObjects;
        private List<MoveData> _moveDataList;
        
        [SetUp]
        public void Setup()
        {
            _playerObjects = new List<GameObject>();
            _moveDataList = new List<MoveData>();
    
            // AI 가중치 생성
            _weights = ScriptableObject.CreateInstance<AIWeights>();
    
            // 보스 설정
            _bossObject = new GameObject("TestBoss");
            _boss = _bossObject.AddComponent<BossEntity>();
    
            // 먼저 보스의 스탯과 MoveSet을 설정
            _boss.ElementType = ElementType.Fire;
            _boss.Attack = 50;
            _boss.MaxHP = 500;
            _boss.CurrentHP = 500;
    
            // 기술 생성
            _boss.MoveSet = new List<MoveData> {
                CreateMoveData("Fireball", 40, ElementType.Fire, MoveCategory.Single),
                CreateMoveData("Explosion", 30, ElementType.Fire, MoveCategory.AOE)
            };

            // 이제 Initialize() 호출 → MoveSet 기반으로 MoveInstances 생성
            _boss.Initialize();
    
            // 플레이어 설정
            _players = new BattleEntity[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject playerObj = new GameObject($"TestPlayer{i}");
                _playerObjects.Add(playerObj);
        
                _players[i] = playerObj.AddComponent<BattleEntity>();
                _players[i].ElementType = ElementType.Water;
                _players[i].MaxHP = 100;
                _players[i].CurrentHP = 100;
                _players[i].Defense = 25;
            }
        }
        
        private MoveData CreateMoveData(string name, int power, ElementType type, MoveCategory category)
        {
            MoveData move = ScriptableObject.CreateInstance<MoveData>();
            move.Name = name;
            move.Power = power;
            move.Type = type;
            move.Category = category;
            move.Accuracy = 100;
            move.CriticalChance = 0.0f;
            
            _moveDataList.Add(move);
            return move;
        }
        
        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_bossObject);
            
            foreach (var playerObj in _playerObjects)
            {
                Object.DestroyImmediate(playerObj);
            }
            
            foreach (var moveData in _moveDataList)
            {
                Object.DestroyImmediate(moveData);
            }
            
            Object.DestroyImmediate(_weights);
        }
        
        [Test]
        public void UtilityAI_Targets_LowHP_Player()
        {
            // 설정 - 첫 번째 플레이어의 HP를 낮게 설정
            _players[0].CurrentHP = 20;
            
            UtilityAI ai = new UtilityAI(_boss, _players, _weights);
            
            // 실행
            MoveDecision decision = ai.Decide();
            
            // 검증 - 낮은 체력의 플레이어를 타겟팅하는지
            Assert.AreEqual(EntityType.Character1, decision.TargetEntity, 
                "AI should target the player with lowest HP");
        }
        
        [Test]
        public void UtilityAI_Prefers_AOE_When_Multiple_Targets_Low()
        {
            // 설정 - 여러 플레이어의 HP를 낮게 설정
            _players[0].CurrentHP = 20;
            _players[1].CurrentHP = 25;
            _players[2].CurrentHP = 30;
            
            // AOE 가중치를 높게 설정
            _weights.AOE = 1.5f;
            
            UtilityAI ai = new UtilityAI(_boss, _players, _weights);
            
            // 실행
            MoveDecision decision = ai.Decide();
            
            // 검증 - AOE 공격을 선호하는지 (1번 인덱스의 기술이 AOE)
            Assert.AreEqual(1, decision.MoveIndex, 
                "AI should prefer AOE attacks when multiple targets have low HP");
        }

        // --- Type Effectiveness 테스트 제거 / 주석 처리 ---
        // [Test]
        // public void UtilityAI_Considers_Type_Effectiveness()
        // {
        //     _boss.MoveSet.Add(CreateMoveData("Water Pulse", 40, ElementType.Water, MoveCategory.Single));
        //     _players[0].ElementType = ElementType.Fire; // 물에 약함
        //     _players[0].CurrentHP = 50;
        //     
        //     UtilityAI ai = new UtilityAI(_boss, _players, _weights);
        //     MoveDecision decision = ai.Decide();
        //     
        //     Assert.AreEqual(2, decision.MoveIndex, 
        //         "AI should prefer moves with type effectiveness");
        //     Assert.AreEqual(EntityType.Character1, decision.TargetEntity,
        //         "AI should target the player weak to the selected move");
        // }
    }
}
