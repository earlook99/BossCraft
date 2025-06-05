using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Entity;

namespace GameSystem
{
    public class TurnManager : MonoBehaviour
    {
        private List<BattleEntity> _turnOrder;
        private int _currentTurnIndex;
        private int _turnCount;
        
        private BattleManager _battleManager;
        
        public BattleEntity CurrentEntity => _currentTurnIndex < _turnOrder.Count ? _turnOrder[_currentTurnIndex] : null;
        public int TurnCount => _turnCount;
        public bool IsPlayerTurn => CurrentEntity != null && !(CurrentEntity is BossEntity);
        public bool IsBossTurn => CurrentEntity is BossEntity;
        
        private const int PLAYER_COUNT = 4;
        
        public void SetBattleManager(BattleManager battleManager)
        {
            _battleManager = battleManager;
        }
        
        public void Initialize(BattleEntity[] entities)
        {
            _turnOrder = new List<BattleEntity>();
            
            for (int i = 0; i < PLAYER_COUNT; i++)
            {
                if (entities[i] != null && entities[i].CurrentHP > 0)
                {
                    _turnOrder.Add(entities[i]);
                }
            }
            
            if (entities[4] != null)
            {
                _turnOrder.Add(entities[4]);
            }
            
            _currentTurnIndex = 0;
            _turnCount = 0;
        }
        
        public void StartNewRound()
        {
            _currentTurnIndex = 0;
            _turnCount++;
            RemoveDefeatedEntities();
        }
        
        public bool NextTurn()
        {
            _currentTurnIndex++;
            SkipDeadEntities();
            
            if (_currentTurnIndex >= _turnOrder.Count)
            {
                return false;
            }
            
            if (_battleManager != null && CurrentEntity != null)
            {
                _battleManager.OnTurnStarted(CurrentEntity, _turnCount);
            }
            
            return true;
        }
        
        private void SkipDeadEntities()
        {
            while (_currentTurnIndex < _turnOrder.Count && _turnOrder[_currentTurnIndex].CurrentHP <= 0)
            {
                _currentTurnIndex++;
            }
        }
        
        private void RemoveDefeatedEntities()
        {
            _turnOrder.RemoveAll(entity => entity.CurrentHP <= 0);
        }
        
        public List<BattleEntity> GetRemainingPlayers()
        {
            return _turnOrder.Where(e => !(e is BossEntity) && e.CurrentHP > 0).ToList();
        }
        
        public BossEntity GetBoss()
        {
            return _turnOrder.FirstOrDefault(e => e is BossEntity) as BossEntity;
        }
        
        public bool IsRoundComplete()
        {
            return _currentTurnIndex >= _turnOrder.Count;
        }
        
        public bool ShouldGoToBossTurn()
        {
            for (int i = _currentTurnIndex; i < _turnOrder.Count; i++)
            {
                if (_turnOrder[i] is BossEntity && _turnOrder[i].CurrentHP > 0)
                {
                    return true;
                }
            }
            return false;
        }
        
        public void EndCurrentTurn()
        {
            if (CurrentEntity != null)
            {
                CurrentEntity.EndTurn();
            }
        }
        
        public int GetCurrentPlayerIndex()
        {
            if (CurrentEntity is BossEntity)
                return -1;
                
            for (int i = 0; i < PLAYER_COUNT; i++)
            {
                if (_turnOrder.Contains(CurrentEntity) && _turnOrder.IndexOf(CurrentEntity) == i)
                {
                    return i;
                }
            }
            
            return -1;
        }
    }
}