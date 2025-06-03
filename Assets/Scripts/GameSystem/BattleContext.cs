using System.Collections.Generic;
using UnityEngine;
using Entity;

namespace GameSystem
{
    public class BattleContext : MonoBehaviour
    {
        private static BattleContext _instance;
        public static BattleContext Instance => _instance;
        
        [Header("Settings")]
        [SerializeField] private BattleSettings _battleSettings;
        
        private BattleEntity[] _entities;
        private Dictionary<EntityType, BattleEntity> _entityLookup;
        private BattleStateMachine _stateMachine;
        private TurnManager _turnManager;
        
        public BattleSettings Settings => _battleSettings;
        public BattleEntity[] Entities => _entities;
        public BattleStateMachine StateMachine => _stateMachine;
        public TurnManager TurnManager => _turnManager;
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            _entityLookup = new Dictionary<EntityType, BattleEntity>();
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        public void Initialize(BattleEntity[] entities, BattleStateMachine stateMachine, TurnManager turnManager)
        {
            _entities = entities;
            _stateMachine = stateMachine;
            _turnManager = turnManager;
            
            BuildEntityLookup();
        }
        
        private void BuildEntityLookup()
        {
            _entityLookup.Clear();
            
            for (int i = 0; i < _entities.Length; i++)
            {
                if (_entities[i] != null)
                {
                    _entityLookup[(EntityType)i] = _entities[i];
                }
            }
        }
        
        public BattleEntity GetEntity(EntityType type)
        {
            return _entityLookup.TryGetValue(type, out var entity) ? entity : null;
        }
        
        public BattleEntity GetEntity(int index)
        {
            return index >= 0 && index < _entities.Length ? _entities[index] : null;
        }
        
        public List<BattleEntity> GetAliveEntities()
        {
            var aliveEntities = new List<BattleEntity>();
            
            foreach (var entity in _entities)
            {
                if (entity != null && entity.CurrentHP > 0)
                {
                    aliveEntities.Add(entity);
                }
            }
            
            return aliveEntities;
        }
        
        public List<BattleEntity> GetAlivePlayers()
        {
            var alivePlayers = new List<BattleEntity>();
            
            for (int i = 0; i < GameConstants.Battle.PLAYER_COUNT; i++)
            {
                if (_entities[i] != null && _entities[i].CurrentHP > 0)
                {
                    alivePlayers.Add(_entities[i]);
                }
            }
            
            return alivePlayers;
        }
        
        public BossEntity GetBoss()
        {
            return _entities[GameConstants.Battle.BOSS_INDEX] as BossEntity;
        }
        
        public bool IsPlayerTurn()
        {
            return _turnManager != null && _turnManager.IsPlayerTurn;
        }
        
        public bool IsBossTurn()
        {
            return _turnManager != null && _turnManager.IsBossTurn;
        }
        
        public int GetCurrentTurn()
        {
            return _turnManager != null ? _turnManager.TurnCount : 0;
        }
        
        public BattleState GetCurrentState()
        {
            return _stateMachine != null ? _stateMachine.CurrentState : BattleState.Initializing;
        }
        
        public bool CanTransitionToState(BattleState targetState)
        {
            return _stateMachine != null && _stateMachine.CanTransitionTo(targetState);
        }
        
        public void TransitionToState(BattleState newState)
        {
            if (_stateMachine != null)
            {
                _stateMachine.TransitionTo(newState);
            }
        }
    }
}